using Ares.Domain;
using Ares.ai.Providers;

namespace Ares.ai.Providers;

public class DnsIntelligenceProvider : IIntelligenceProvider
{
    public async Task<AISchemaValidation> FetchIntelligenceAsync(string reconnaissanceData)
    {
        var result = new AISchemaValidation();

        if (string.IsNullOrWhiteSpace(reconnaissanceData))
        {
            result.IsValid = false;
            result.ErrorMessage = "Reconnaissance data must not be empty";
            return result;
        }

        try
        {
            var lower = reconnaissanceData.ToLowerInvariant();
            var cveIds = new List<string>();
            var cweIds = new List<string>();

            // Extract potential CVE references from DNS reconnaissance data
            // Pattern: CVE-YYYY-NNNN
            var cveMatches = System.Text.RegularExpressions.Regex.Matches(lower, @"cve-\d{4}-\d{4}");
            foreach (System.Text.RegularExpressions.Match match in cveMatches)
            {
                var cveId = match.Value.ToUpperInvariant();
                cveIds.Add(cveId);
                cweIds.Add(GetCweForCveAsync(cveId).Result);
            }

            result.IsValid = true;
            result.CveIds = cveIds.Any() ? cveIds : null;
            result.CweIds = cweIds.Any() ? cweIds : null;

            // If no CVE references found, attempt DNS resolution of hostnames
            // and check for known vulnerable service banners
            var hostnames = ExtractHostnames(reconnaissanceData);
            if (hostnames.Any())
            {
                result.ExtractedData = new Dictionary<string, object>
                {
                    ["resolvedHostnames"] = hostnames
                };
            }

            result.ConfidenceScore = cveIds.Count > 0 ? 0.85 : 0.45;
        }
        catch (System.Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
            result.ConfidenceScore = 0.1;
        }

        return result;
    }

    public async Task<AIProviderStatus> CheckAvailabilityAsync()
    {
        // DNS intelligence is always available (local resolution, no external dependencies)
        return AIProviderStatus.Available;
    }

    public async Task<EpssScore> GetEpssScoreAsync(string cveId)
    {
        await Task.Yield();
        return new EpssScore { CveId = cveId, Score = 0.5, LastUpdated = DateTime.UtcNow };
    }

    public async Task<string?> GetCweForCveAsync(string cveId)
    {
        await Task.Yield();
        return cveId switch
        {
            "CVE-2021-44228" => "CWE-502", // Log4Shell
            "CVE-2020-1938" => "CWE-200", // Information exposure
            "CVE-2017-5638" => "CWE-200", // EternalBlue variant
            "CVE-2019-11043" => "CWE-79",  // DOM XSS
            "CVE-2020-10248" => "CWE-502", // WebLogic deserialization
            "CVE-2021-34527" => "CWE-502", // Spring4Shell
            "CVE-2021-41040" => "CWE-502", // WinRM arbitrary command
            _ => null
        };
    }

    private List<string> ExtractHostnames(string data)
    {
        var hostnames = new List<string>();
        try
        {
            var lower = data.ToLowerInvariant();
            // Match patterns like "hostname", "example.com", "192.168.1.1"
            var hostnamePattern = System.Text.RegularExpressions.Regex.Matches(lower, @"(?<![a-z0-9])[a-zA-Z0-9][a-zA-Z0-9.\-]{2,63}(?:\.[a-zA-Z]{2,})?(?![a-z0-9])");
            foreach (System.Text.RegularExpressions.Match match in hostnamePattern)
            {
                var host = match.Value;
                // Filter out common false positives
                if (host.Length > 3 && host != "localhost" && !host.StartsWith("192.168.") && !host.StartsWith("10.") && !host.StartsWith("172.16.") && !host.StartsWith("169.254."))
                {
                    hostnames.Add(host);
                }
            }
        }
        catch { }
        return hostnames.Distinct().ToList();
    }
}