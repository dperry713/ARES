using Ares.Domain;
using Ares.ai.Providers;

namespace Ares.ai.Providers;

public class HttpIntelligenceProvider : IIntelligenceProvider
{
    private readonly HttpClient _httpClient;

    public HttpIntelligenceProvider()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
            DefaultRequestHeaders = {
                { "User-Agent", "Ares-Recon/1.0" }
            }
        };
    }

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

            // Extract potential CVE references from HTTP reconnaissance data
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

            // Attempt HTTP service probes for resolved hostnames
            var hostnames = ExtractHostnames(lower);
            if (hostnames.Any())
            {
                foreach (var hostname in hostnames.Take(3)) // Limit to 3 hosts to avoid flooding
                {
                    try
                    {
                        var responses = new List<(string url, int statusCode, string banner)>();
                        foreach (var scheme in new[] { "http://", "https://" })
                        {
                            var url = $"{scheme}{hostname}";
                            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                            var response = await _httpClient.GetAsync(url, cts.Token);
                            responses.Add((url, (int)response.StatusCode, response.RequestMessage?.RequestUri?.ToString() ?? url));
                        }

                        result.ExtractedData = new Dictionary<string, object>
                        {
                            ["httpProbes"] = responses
                        };
                        break; // Use first successful probe set
                    }
                    catch
                    {
                        // Continue to next hostname
                    }
                }
            }

            result.ConfidenceScore = cveIds.Count > 0 ? 0.85 : 0.55;
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
        // HTTP intelligence requires network access; status depends on connectivity
        try
        {
            using var cts = new CancellationTokenSource();
            var _ = await _httpClient.GetAsync("https://httpbin.org/status/404", cts.Token);
            return AIProviderStatus.Available;
        }
        catch
        {
            return AIProviderStatus.Unavailable;
        }
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
            // Match patterns like "example.com", "hostname", "sub.domain.org"
            var hostnamePattern = System.Text.RegularExpressions.Regex.Matches(data, @"(?<![a-z0-9])[a-zA-Z0-9][a-zA-Z0-9.\-]{2,63}(?:\.[a-zA-Z]{2,})?(?![a-z0-9])");
            foreach (System.Text.RegularExpressions.Match match in hostnamePattern)
            {
                var host = match.Value;
                if (host.Length > 3 && host != "localhost")
                {
                    hostnames.Add(host);
                }
            }
        }
        catch { }
        return hostnames.Distinct().ToList();
    }
}