<#
.SYNOPSIS
    Builds verifiable ARES release artifacts from a clean build.

.DESCRIPTION
    Produces, for each runtime identifier:
      * a framework-dependent publish output for the management-plane API
      * a deterministic .zip archive of that output
      * a SHA-256 checksum per file and per archive
    plus release-level evidence:
      * release-manifest.json   - version, SDK, RID, file hashes, timestamps
      * dependency-manifest.json - resolved NuGet graph (direct + transitive)

    Optionally smoke-tests the publish output by starting the API on a free
    loopback port, calling /health/live and /health/ready, and shutting it down.

    This script never publishes, pushes, tags, or signs anything. Distribution
    is an explicit, separate, authorized step.

.PARAMETER Version
    SemVer version stamped into the assemblies and the manifest.

.PARAMETER RuntimeIdentifiers
    RIDs to publish. Defaults to win-x64 and linux-x64.

.PARAMETER OutputDirectory
    Artifact root. Defaults to <repo>/artifacts/release.

.PARAMETER SkipTests
    Skip the unit and API test suites. The release is then marked
    "testsSkipped": true in the manifest, because an untested build is not a
    release candidate.

.PARAMETER SmokeTest
    Start each host-compatible publish output and verify health endpoints.

.EXAMPLE
    pwsh -NoProfile -File scripts/Build-Release.ps1 -Version 1.2.0

.EXAMPLE
    pwsh -NoProfile -File scripts/Build-Release.ps1 -Version 1.2.0 -RuntimeIdentifiers win-x64 -SmokeTest
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$')]
    [string]$Version = '1.0.0',

    [Parameter()]
    [ValidateSet('win-x64', 'linux-x64', 'linux-arm64', 'osx-arm64')]
    [string[]]$RuntimeIdentifiers = @('win-x64', 'linux-x64'),

    [Parameter()]
    [string]$OutputDirectory,

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$SmokeTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'Ares.Solution.slnx'
$apiProject = Join-Path $repoRoot 'src/backend/Ares.Api/Ares.Api.csproj'
$unitTests = Join-Path $repoRoot 'tests/unit/Ares.UnitTests/Ares.UnitTests.csproj'
$apiTests = Join-Path $repoRoot 'tests/unit/Ares.ApiTests/Ares.ApiTests.csproj'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/release' }

function Invoke-DotNet {
    param([string[]]$Arguments, [string]$Description)

    Write-Host "==> $Description"
    Write-Host "    dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE ($Description)."
    }
}

function New-DeterministicZip {
    <# Zip entries get a fixed timestamp so identical inputs produce identical
       archives. Compression level is fixed for the same reason. #>
    param([string]$SourceDirectory, [string]$DestinationPath, [string]$EntryPrefix)

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path -LiteralPath $DestinationPath) { Remove-Item -LiteralPath $DestinationPath -Force }
    $fixedTimestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)

    $stream = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::CreateNew)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            $files = Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File | Sort-Object FullName
            foreach ($file in $files) {
                $relative = [System.IO.Path]::GetRelativePath($SourceDirectory, $file.FullName)
                $entryName = "$EntryPrefix/$($relative -replace '\\', '/')"
                $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $fixedTimestamp
                $entryStream = $entry.Open()
                try {
                    $source = [System.IO.File]::OpenRead($file.FullName)
                    try { $source.CopyTo($entryStream) } finally { $source.Dispose() }
                }
                finally { $entryStream.Dispose() }
            }
        }
        finally { $archive.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Test-PublishOutput {
    <# Starts the published API on a free loopback port and verifies both health
       probes. Returns the observed responses so the caller can log real evidence. #>
    param([string]$PublishDirectory, [string]$RuntimeIdentifier)

    $entry = Join-Path $PublishDirectory 'Ares.Api.dll'
    if (-not (Test-Path -LiteralPath $entry)) { throw "Publish output is missing Ares.Api.dll: $PublishDirectory" }

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()

    $dataDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ares-smoke-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dataDir -Force | Out-Null

    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$port"
    $env:ARES_DATA_DIR = $dataDir
    $env:ARES_BOOTSTRAP_ADMIN_KEY = 'smoke-test-credential-0123456789abcdef'

    $process = $null
    try {
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
        $startInfo.ArgumentList.Add($entry)
        $startInfo.WorkingDirectory = $PublishDirectory
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $process = [System.Diagnostics.Process]::Start($startInfo)

        $live = $null
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($process.HasExited) {
                $stderr = $process.StandardError.ReadToEnd()
                throw "Published host exited with code $($process.ExitCode) during startup. stderr: $stderr"
            }
            try {
                $live = Invoke-RestMethod -Uri "http://127.0.0.1:$port/health/live" -TimeoutSec 3
                break
            }
            catch { Start-Sleep -Milliseconds 250 }
        }
        if ($null -eq $live) { throw 'Liveness probe did not answer within 45 seconds.' }

        $ready = Invoke-RestMethod -Uri "http://127.0.0.1:$port/health/ready" -TimeoutSec 10
        $info = Invoke-RestMethod -Uri "http://127.0.0.1:$port/api/v1/system/info" -TimeoutSec 10 `
            -Headers @{ 'X-Ares-Api-Key' = $env:ARES_BOOTSTRAP_ADMIN_KEY }

        if ($ready.status -ne 'Healthy') { throw "Readiness probe reported '$($ready.status)'." }
        if ($info.product -ne 'ARES') { throw "system/info did not identify the product: $($info | ConvertTo-Json -Compress)" }

        return [ordered]@{
            runtimeIdentifier    = $RuntimeIdentifier
            live                 = $live.status
            ready                = $ready.status
            authenticatedProduct = $info.product
            authenticatedVersion = $info.version
            journalChainIntact   = $info.journal.chainIntact
        }
    }
    finally {
        if ($process -and -not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit(5000) | Out-Null
        }
        if ($process) { $process.Dispose() }
        Remove-Item Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
        Remove-Item Env:ARES_DATA_DIR -ErrorAction SilentlyContinue
        Remove-Item Env:ARES_BOOTSTRAP_ADMIN_KEY -ErrorAction SilentlyContinue
        Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$startedAt = [DateTimeOffset]::UtcNow
$sdkVersion = (& dotnet --version)
if ($LASTEXITCODE -ne 0) { throw 'dotnet --version failed; no SDK on PATH.' }

Write-Host "ARES release build"
Write-Host "  repo root : $repoRoot"
Write-Host "  version   : $Version"
Write-Host "  SDK       : $sdkVersion"
Write-Host "  output    : $OutputDirectory"

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$env:CI = 'true'
try {
    Invoke-DotNet -Arguments @('restore', $solution) -Description 'Restore solution (locked to declared versions)'
    Invoke-DotNet -Arguments @('build', $solution, '--configuration', 'Release', '--no-restore', '--no-incremental',
        "-p:Version=$Version") -Description 'Build solution (warnings are errors)'

    $testsRun = $false
    if (-not $SkipTests) {
        Invoke-DotNet -Arguments @('test', $unitTests, '--configuration', 'Release', '--no-build',
            "-p:Version=$Version") -Description 'Unit test suite'
        Invoke-DotNet -Arguments @('test', $apiTests, '--configuration', 'Release', '--no-build',
            "-p:Version=$Version") -Description 'API test suite'
        $testsRun = $true
    }
    else {
        Write-Warning 'Tests skipped: the resulting manifest records testsSkipped=true and is not release evidence.'
    }

    $dependencyManifestPath = Join-Path $OutputDirectory 'dependency-manifest.json'
    Invoke-DotNet -Arguments @('list', $apiProject, 'package', '--include-transitive', '--format', 'json',
        '--output', $dependencyManifestPath) -Description 'Resolve dependency manifest (direct + transitive)'

    $artifacts = @()
    $smokeResults = @()

    foreach ($rid in $RuntimeIdentifiers) {
        $publishDir = Join-Path $OutputDirectory "publish/$rid"
        Invoke-DotNet -Arguments @('publish', $apiProject, '--configuration', 'Release', '--no-build',
            '--runtime', $rid, '--self-contained', 'false', '--output', $publishDir,
            "-p:Version=$Version", '-p:UseAppHost=false') -Description "Publish for $rid"

        $archiveName = "ares-api-$Version-$rid.zip"
        $archivePath = Join-Path $OutputDirectory $archiveName
        New-DeterministicZip -SourceDirectory $publishDir -DestinationPath $archivePath `
            -EntryPrefix "ares-api-$Version-$rid"

        $fileHashes = Get-ChildItem -LiteralPath $publishDir -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                [ordered]@{
                    path   = [System.IO.Path]::GetRelativePath($publishDir, $_.FullName) -replace '\\', '/'
                    bytes  = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                }
            }

        $artifacts += [ordered]@{
            runtimeIdentifier = $rid
            archive           = $archiveName
            archiveBytes      = (Get-Item -LiteralPath $archivePath).Length
            archiveSha256     = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
            files             = @($fileHashes)
        }

        if ($SmokeTest -and ($rid -eq 'win-x64') -eq $IsWindows) {
            $smokeResults += Test-PublishOutput -PublishDirectory $publishDir -RuntimeIdentifier $rid
        }
    }

    $checksumLines = foreach ($artifact in $artifacts) {
        "$($artifact.archiveSha256)  $($artifact.archive)"
    }
    $checksumLines | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding utf8

    $manifest = [ordered]@{
        product            = 'ARES'
        component          = 'management-plane'
        version            = $Version
        builtAtUtc         = $startedAt.ToString('O')
        completedAtUtc     = [DateTimeOffset]::UtcNow.ToString('O')
        sdkVersion         = $sdkVersion
        configuration      = 'Release'
        testsRun           = $testsRun
        testsSkipped       = -not $testsRun
        smokeTests         = @($smokeResults)
        runtimeIdentifiers = @($RuntimeIdentifiers)
        artifacts          = @($artifacts)
        dependencyManifest = 'dependency-manifest.json'
        checksums          = 'SHA256SUMS.txt'
        notes              = @(
            'Archives are byte-for-byte reproducible for identical inputs (fixed entry timestamps).',
            'Binaries are not code-signed. Signing requires an authorizing release step.'
        )
    }
    $manifestPath = Join-Path $OutputDirectory 'release-manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

    Write-Host ''
    Write-Host 'Release artifacts'
    Get-ChildItem -LiteralPath $OutputDirectory -File | ForEach-Object {
        Write-Host ("  {0,-32} {1,12:N0} bytes" -f $_.Name, $_.Length)
    }
    if ($smokeResults.Count -gt 0) {
        Write-Host ''
        Write-Host 'Smoke tests'
        foreach ($result in $smokeResults) {
            Write-Host ("  {0}: live={1} ready={2} product={3} version={4} chainIntact={5}" -f `
                $result.runtimeIdentifier, $result.live, $result.ready, $result.authenticatedProduct,
                $result.authenticatedVersion, $result.journalChainIntact)
        }
    }
    Write-Host ''
    Write-Host "Manifest: $manifestPath"
    Write-Host 'Release build complete.'
}
finally {
    Remove-Item Env:CI -ErrorAction SilentlyContinue
}
