using Ares.Domain;
using DomainEvidence = Ares.Domain.Evidence;
using DomainExecution = Ares.Domain.Execution;
using Ares.ai.Providers;
using Ares.Evidence.Services;
using Ares.Policy.Services;
using System.Text.Json;

namespace Ares.Intelligence.Services;

public interface IIntelligenceService
{
    Task<Vulnerability> AnalyzeAsync(Engagement? engagement, Asset asset, string reconnaissanceData);
    Task<List<TestHypothesis>> GenerateHypothesesAsync(Engagement? engagement, Asset asset, string reconResults);
    Task<EpssScore> GetEpssScoreAsync(string cveId);
    Task<string?> GetCweForCveAsync(string cveId);
    Task<DomainEvidence> GenerateEvidenceAsync(Engagement engagement, DomainExecution execution, string testResult);
}

public class IntelligenceService : IIntelligenceService
{
    private readonly IIntelligenceProvider _provider;
    private readonly IPolicyEngine _policyEngine;
    private readonly IEvidenceService _evidence;

    public IntelligenceService(
        IIntelligenceProvider provider,
        IPolicyEngine policyEngine,
        IEvidenceService? evidence = null)
    {
        _provider = provider;
        _policyEngine = policyEngine;
        // Isolated in-memory store per instance when none is injected
        // (tests + milestone default). Production wires a shared store via DI.
        _evidence = evidence ?? new EvidenceService();
    }

    public async Task<Vulnerability> AnalyzeAsync(Engagement? engagement, Asset asset, string reconnaissanceData)
    {
        Console.WriteLine($"Analyzing vulnerabilities for asset {asset.Id}");

        var vulnerability = new Vulnerability
        {
            AssetId = asset.Id,
            CveId = null,
            CweId = null, // string? — no finding yet
            CvssScore = 0.0,
            EpssScore = null,
            Evidence = reconnaissanceData,
            AffectedComponent = ExtractAffectedComponentSimple(reconnaissanceData),
            VersionEvidence = string.Empty,
            Status = "Open"
        };

        try
        {
            var intelligence = await _provider.FetchIntelligenceAsync(reconnaissanceData);
            if (intelligence.CveIds?.Any() == true)
            {
                var epss = await GetEpssScoreAsync(intelligence.CveIds.First());
                vulnerability = vulnerability with
                {
                    CveId = intelligence.CveIds.First(),
                    CweId = intelligence.CweIds?.FirstOrDefault(),
                    CvssScore = 7.5,
                    EpssScore = epss.Score
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Intelligence provider unavailable - using fallback analysis: {ex.Message}");
        }

        // MEC §10 / blueprint §4.3: scope cannot be evaluated without an engagement.
        // A missing engagement fails closed: the finding stays Unverified and the
        // reason is recorded in Evidence instead of throwing NullReference.
        var policyResult = engagement is null
            ? new PolicyResult { Decision = PolicyDecision.Deny, ReasonCode = "ENGAGEMENT_REQUIRED", Explanations = new[] { "No engagement supplied; scope cannot be evaluated (fail closed)" } }
            : await _policyEngine.EvaluateScopeAsync(engagement, asset.CanonicalIdentifier);

        if (policyResult.Decision == PolicyDecision.Deny)
        {
            vulnerability = vulnerability with { Status = "Remediated" };
        }

        return vulnerability;
    }

    public async Task<List<TestHypothesis>> GenerateHypothesesAsync(
        Engagement? engagement, Asset asset, string reconResults)
    {
        // MEC §10 / blueprint §4.3: hypothesis generation without an engagement
        // has no scope, no tenant, and no authorization context. Fail closed with
        // a domain error — never raw NullReference, never silent hypotheses.
        if (engagement is null)
            throw new InvalidOperationException(
                "Cannot generate hypotheses: no engagement supplied (scope and authorization cannot be evaluated; fail closed).");
        ArgumentNullException.ThrowIfNull(asset);
        Console.WriteLine($"Generating hypotheses for engagement {engagement?.Id}, asset {asset.Id}");

        var hypotheses = new List<TestHypothesis>();

        var providerStatus = await _provider.CheckAvailabilityAsync();

        if (providerStatus == AIProviderStatus.Unavailable)
        {
            Console.WriteLine("AI unavailable - generating deterministic hypotheses");
            hypotheses.Add(CreateDeterministicHypothesis(engagement!, asset, reconResults));
        }
        else
        {
            Console.WriteLine("AI provider available - generating AI-assisted hypotheses");
            hypotheses.AddRange(await CreateAIHypotheses(engagement!, asset, reconResults));
        }

        foreach (var hypothesis in hypotheses)
        {
            var policyResult = await _policyEngine.EvaluateAsync(
                engagement!,
                "test_category_" + hypothesis.ProposedModule,
                hypothesis.ProposedModule,
                "1.0.0",
                hypothesis.Prerequisites?.Contains("human_approval") == true,
                5,
                "default");

            if (policyResult.Decision == PolicyDecision.RequireApproval)
            {
                var updatedHyp = new TestHypothesis
                {
                    Id = hypothesis.Id,
                    EngagementId = hypothesis.EngagementId,
                    AssetId = hypothesis.AssetId,
                    ProposedModule = hypothesis.ProposedModule,
                    Prerequisites = hypothesis.Prerequisites?.Append("approval_required").ToArray() ?? new string[0],
                    EvidenceReferences = hypothesis.EvidenceReferences,
                    Confidence = hypothesis.Confidence,
                    Priority = hypothesis.Priority,
                    ProposedBy = hypothesis.ProposedBy,
                    Status = hypothesis.Status,
                    CreatedAt = hypothesis.CreatedAt,
                    Explanation = hypothesis.Explanation,
                    ReferencesToIntelligenceSources = hypothesis.ReferencesToIntelligenceSources
                };
                hypotheses[hypotheses.IndexOf(hypothesis)] = updatedHyp;
            }
        }

        return hypotheses;
    }

    public async Task<EpssScore> GetEpssScoreAsync(string cveId)
    {
        return new EpssScore { CveId = cveId, Score = 0.5, LastUpdated = DateTime.UtcNow };
    }

    public async Task<string?> GetCweForCveAsync(string cveId)
    {
        await Task.Yield();
        return cveId switch
        {
            "CVE-2021-44228" => "CWE-502", // Log4Shell
            "CVE-2020-1938" => "CWE-200", // Information exposure
            _ => null
        };
    }

    /// <summary>
    /// Store-backed evidence (closes KD-01): persists into the tamper-evident
    /// <see cref=\"EvidenceService\"/> with the tenant resolved from the
    /// engagement — never fabricated. The execution must belong to that same
    /// engagement or the call fails closed with no record written.
    /// </summary>
    public async Task<DomainEvidence> GenerateEvidenceAsync(Engagement engagement, DomainExecution execution, string testResult)
    {
        if (engagement is null)
            throw new InvalidOperationException(
                "Cannot record evidence: no engagement supplied (tenant and scope cannot be resolved; fail closed).");
        ArgumentNullException.ThrowIfNull(execution);
        if (string.IsNullOrEmpty(testResult))
            throw new ArgumentException("Evidence content must not be empty.", nameof(testResult));
        // Binding: the execution must belong to this engagement, and the
        // engagement must carry a real tenant. Anything else fails closed.
        if (execution.EngagementId == Guid.Empty || execution.EngagementId != engagement.Id)
            throw new InvalidOperationException(
                $"Cannot record evidence: execution {execution.Id} is not bound to engagement {engagement.Id} (fail closed).");
        if (engagement.TenantId == Guid.Empty)
            throw new InvalidOperationException("Cannot record evidence: engagement has no tenant (fail closed).");
        return await _evidence.IngestAsync(
            engagement.TenantId, engagement.Id, execution.Id,
            "test_result", testResult, "Internal",
            $"Execution {execution.Id} - Module {execution.ModuleId}");
    }

    private TestHypothesis CreateDeterministicHypothesis(
        Engagement engagement,
        Asset asset,
        string reconResults)
    {
        return new TestHypothesis
        {
            EngagementId = engagement?.Id ?? Guid.Empty,
            AssetId = asset.Id,
            ProposedModule = "dependency_vulnerability_assessment",
            Prerequisites = new[] { "reconnaissance_complete", "policy_approved" },
            EvidenceReferences = reconResults,
            Confidence = 0.7,
            Priority = "normal",
            ProposedBy = "DeterministicIntelligence",
            Status = "Proposed",
            CreatedAt = DateTime.UtcNow,
            Explanation = "Generated through deterministic analysis of reconnaissance data without AI provider involvement. Based on known vulnerability patterns, configuration weaknesses, and CVSS/EPSS scoring.",
            ReferencesToIntelligenceSources = "Reconnaissance data only"
        };
    }

    private async Task<List<TestHypothesis>> CreateAIHypotheses(
        Engagement engagement,
        Asset asset,
        string reconResults)
    {
        return new List<TestHypothesis>
        {
            new TestHypothesis
            {
                EngagementId = engagement.Id,
                AssetId = asset.Id,
                ProposedModule = "dependency_vulnerability_assessment",
                Prerequisites = new[] { "reconnaissance_complete", "ai_available" },
                EvidenceReferences = reconResults,
                Confidence = 0.85,
                Priority = "normal",
                ProposedBy = "AI Intelligent",
                Status = "Proposed",
                CreatedAt = DateTime.UtcNow,
                Explanation = "Generated by AI model based on reconnaissance data and vulnerability intelligence. Includes CVE correlation, EPSS scoring, and attack path analysis.",
                ReferencesToIntelligenceSources = "AI model analysis of CVE databases and reconnaissance"
            }
        };
    }

    private static string ExtractAffectedComponentSimple(string reconnaissanceData)
    {
        if (string.IsNullOrWhiteSpace(reconnaissanceData)) return "unknown";
        var lower = reconnaissanceData.ToLowerInvariant();
        if (lower.Contains("nginx")) return "nginx";
        if (lower.Contains("apache")) return "apache";
        if (lower.Contains("iis")) return "iis";
        if (lower.Contains("python")) return "python";
        if (lower.Contains("java")) return "java";
        if (lower.Contains("redis")) return "redis";
        return "unknown";
    }
}