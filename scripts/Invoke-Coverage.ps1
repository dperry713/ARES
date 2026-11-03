<#
.SYNOPSIS
    Measures line/branch coverage for the ARES test suites and enforces floors.

.DESCRIPTION
    Runs each test project with the Coverlet collector, parses the resulting
    Cobertura reports, prints a per-package table and fails when a covered
    package or the suite total falls below the configured floor.

    The two suites are measured separately and never merged: a merged number
    would hide which suite actually exercises a package. Treat the unit suite
    as the package-level gate and the API suite as end-to-end evidence.

    Coverage is a floor, not a target. A high percentage of trivial assertions
    proves nothing, which is why the security-critical scenarios are enumerated
    as explicit tests in the suites themselves.

.PARAMETER Configuration
    Build configuration. Defaults to Debug so it reuses a local build.

.PARAMETER ResultsDirectory
    Root for the raw Cobertura files. Defaults to <repo>/artifacts/coverage.

.PARAMETER MinLine
    Minimum line rate for gated packages. Default 0.90.

.PARAMETER MinBranch
    Minimum branch rate for gated packages. Default 0.85.

.PARAMETER GatedPackages
    Package names the floors apply to. Other packages are reported only.

.EXAMPLE
    pwsh -NoProfile -File scripts/Invoke-Coverage.ps1

.EXAMPLE
    pwsh -NoProfile -File scripts/Invoke-Coverage.ps1 -MinLine 0.95 -MinBranch 0.90
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter()]
    [string]$ResultsDirectory,

    [Parameter()]
    [ValidateRange(0.0, 1.0)]
    [double]$MinLine = 0.90,

    [Parameter()]
    [ValidateRange(0.0, 1.0)]
    [double]$MinBranch = 0.85,

    [Parameter()]
    [string[]]$GatedPackages = @(
        'Ares.Domain', 'Ares.Policy', 'Ares.Approvals',
        'Ares.Execution', 'Ares.Identity', 'Ares.Infrastructure'
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ResultsDirectory) { $ResultsDirectory = Join-Path $repoRoot 'artifacts/coverage' }

$suites = @(
    @{ Name = 'unit'; Project = Join-Path $repoRoot 'tests/unit/Ares.UnitTests/Ares.UnitTests.csproj' },
    @{ Name = 'api'; Project = Join-Path $repoRoot 'tests/unit/Ares.ApiTests/Ares.ApiTests.csproj' }
)

function Get-CoverageReport {
    param([string]$Path)

    [xml]$document = Get-Content -LiteralPath $Path
    $root = $document.coverage

    $packages = foreach ($package in $root.packages.package) {
        [pscustomobject]@{
            Package       = [string]$package.name
            LineRate      = [double]$package.'line-rate'
            BranchRate    = [double]$package.'branch-rate'
            LinesCovered  = [int]$package.'lines-covered'
            LinesValid    = [int]$package.'lines-valid'
            BranchesHit   = [int]$package.'branches-covered'
            BranchesValid = [int]$package.'branches-valid'
        }
    }

    [pscustomobject]@{
        LineRate      = [double]$root.'line-rate'
        BranchRate    = [double]$root.'branch-rate'
        LinesCovered  = [int]$root.'lines-covered'
        LinesValid    = [int]$root.'lines-valid'
        BranchesHit   = [int]$root.'branches-covered'
        BranchesValid = [int]$root.'branches-valid'
        Packages      = @($packages)
    }
}

if (Test-Path -LiteralPath $ResultsDirectory) {
    Remove-Item -LiteralPath $ResultsDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

$reports = @()
foreach ($suite in $suites) {
    $suiteDirectory = Join-Path $ResultsDirectory $suite.Name
    Write-Host "==> Coverage: $($suite.Name) suite"
    & dotnet test $suite.Project --configuration $Configuration --no-build `
        --collect:"XPlat Code Coverage" --results-directory $suiteDirectory
    if ($LASTEXITCODE -ne 0) { throw "Test run failed for the $($suite.Name) suite (exit $LASTEXITCODE)." }

    $cobertura = Get-ChildItem -LiteralPath $suiteDirectory -Recurse -Filter 'coverage.cobertura.xml' |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if (-not $cobertura) { throw "No Cobertura report was produced for the $($suite.Name) suite." }

    $reports += [pscustomobject]@{
        Suite = $suite.Name
        Path  = $cobertura.FullName
        Data  = Get-CoverageReport -Path $cobertura.FullName
    }
}

$failed = $false
foreach ($report in $reports) {
    Write-Host ''
    Write-Host ("{0} suite — total line {1:P1} ({2}/{3}), branch {4:P1} ({5}/{6})" -f `
        $report.Suite,
        $report.Data.LineRate, $report.Data.LinesCovered, $report.Data.LinesValid,
        $report.Data.BranchRate, $report.Data.BranchesHit, $report.Data.BranchesValid)
    Write-Host ("  {0,-26} {1,8} {2,8}  {3}" -f 'package', 'line', 'branch', 'gate')

    foreach ($package in ($report.Data.Packages | Sort-Object Package)) {
        $gated = $GatedPackages -contains $package.Package
        $status = if (-not $gated) { 'reported' }
        elseif ($package.LineRate -lt $MinLine -or $package.BranchRate -lt $MinBranch) { 'FAIL'; $failed = $true }
        else { 'pass' }
        Write-Host ("  {0,-26} {1,8:P1} {2,8:P1}  {3}" -f `
            $package.Package, $package.LineRate, $package.BranchRate, $status)
    }
}

$rawReports = $reports | ForEach-Object { [ordered]@{ suite = $_.Suite; path = $_.Path } }
[ordered]@{
    measuredAt    = [DateTimeOffset]::UtcNow.ToString('O')
    configuration = $Configuration
    floors        = [ordered]@{ line = $MinLine; branch = $MinBranch; packages = @($GatedPackages) }
    suites        = @(
        $reports | ForEach-Object {
            [ordered]@{
                suite       = $_.Suite
                lineRate    = [math]::Round($_.Data.LineRate, 4)
                branchRate  = [math]::Round($_.Data.BranchRate, 4)
                lines       = [ordered]@{ covered = $_.Data.LinesCovered; valid = $_.Data.LinesValid }
                branches    = [ordered]@{ covered = $_.Data.BranchesHit; valid = $_.Data.BranchesValid }
                packages    = @(
                    $_.Data.Packages | Sort-Object Package | ForEach-Object {
                        [ordered]@{
                            package     = $_.Package
                            lineRate    = [math]::Round($_.LineRate, 4)
                            branchRate  = [math]::Round($_.BranchRate, 4)
                            lines       = [ordered]@{ covered = $_.LinesCovered; valid = $_.LinesValid }
                            branches    = [ordered]@{ covered = $_.BranchesHit; valid = $_.BranchesValid }
                        }
                    }
                )
            }
        }
    )
    rawReports    = @($rawReports)
    result        = if ($failed) { 'failed' } else { 'passed' }
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'coverage-summary.json') -Encoding utf8

Write-Host ''
Write-Host "Summary: $(Join-Path $ResultsDirectory 'coverage-summary.json')"

if ($failed) {
    Write-Error "Coverage gate failed: a gated package is below line $MinLine / branch $MinBranch."
    exit 1
}

Write-Host 'Coverage gate passed.'
exit 0
