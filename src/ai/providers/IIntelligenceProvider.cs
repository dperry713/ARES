using Ares.Domain;
using Ares.ai.Providers;

namespace Ares.ai.Providers;

public interface IIntelligenceProvider
{
    Task<AISchemaValidation> FetchIntelligenceAsync(string reconnaissanceData);
    Task<AIProviderStatus> CheckAvailabilityAsync();
    Task<EpssScore> GetEpssScoreAsync(string cveId);
    Task<string?> GetCweForCveAsync(string cveId);
}

public enum AIProviderStatus
{
    Available,
    Unavailable,
    Degraded
}

public class AISchemaValidation
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? ExtractedData { get; set; }
    public List<string>? CveIds { get; set; }
    public List<string>? CweIds { get; set; }
    public double? ConfidenceScore { get; set; }
}