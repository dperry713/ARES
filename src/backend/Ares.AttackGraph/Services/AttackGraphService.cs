using Ares.Domain;
using Ares.Infrastructure.Journal;
using System.Collections.Generic;
using System.Linq;

namespace Ares.AttackGraph.Services;

public interface IAttackGraphService
{
    Task<AttackGraph> BuildGraphAsync(Engagement engagement);
    Task AddAssetRelationshipAsync(Asset source, Asset target, string relationshipType);
    Task AddVulnerabilityRelationshipAsync(Finding finding, Vulnerability vulnerability, string attackPath);
    Task<List<AttackPath>> FindAttackPathsAsync(Asset startingAsset);
    Task<NodePosition> GetNodePositionAsync(Guid nodeId);
}

public sealed record NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; } = 0;
}

public class AttackGraph
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EngagementId { get; set; }
    public List<Node> Nodes { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class Node
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "asset";
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Shape { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}

public class Edge
{
    public Guid Id { get; set; }
    public Guid Source { get; set; }
    public Guid Target { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "connects_to";
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public float? Weight { get; set; }
}

public class AttackPath
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<Guid> NodeIds { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public List<string> Steps { get; set; } = new();
    public string? Mitigation { get; set; }
}

public class AttackGraphService : IAttackGraphService
{
    private readonly IStateJournal _journal;

    public AttackGraphService(IStateJournal journal)
    {
        _journal = journal;
    }

    public async Task<AttackGraph> BuildGraphAsync(Engagement engagement)
    {
        var graph = new AttackGraph { EngagementId = engagement.Id };

        // Add engagement as root node
        var engagementNode = new Node
        {
            Id = Guid.NewGuid(),
            Label = $"Engagement {engagement.Id}",
            Type = "engagement",
            Title = engagement.Name,
            Description = engagement.Description,
            Properties = new Dictionary<string, object>
            {
                ["scope"] = engagement.ScopeDefinition,
                ["allowedAssessmentTypes"] = engagement.AllowedAssessmentTypes
            }
        };
        graph.Nodes.Add(engagementNode);

        // Add assets linked to this engagement from the journal
        var assetEntries = _journal.Read($"asset/{engagement.Id}");
        foreach (var entry in assetEntries)
        {
            try
            {
                var assetPayload = System.Text.Json.JsonDocument.Parse(entry.Payload);
                var root = assetPayload.RootElement;

                var assetId = root.GetProperty("Id").GetGuid();
                var hostname = root.GetProperty("Hostname").GetString() ?? "";
                var ipAddress = root.GetProperty("CanonicalIdentifier").GetString() ?? "";

                var assetNode = new Node
                {
                    Id = assetId,
                    Label = hostname ?? ipAddress,
                    Type = "asset",
                    Title = hostname ?? ipAddress,
                    Description = $"Asset at {ipAddress}",
                    Properties = new Dictionary<string, object>
                    {
                        ["ipAddress"] = ipAddress,
                        ["hostname"] = hostname,
                        ["assetType"] = root.GetProperty("AssetType").GetString(),
                        ["technologyFingerprint"] = root.GetProperty("TechnologyFingerprint").GetString()
                    }
                };
                graph.Nodes.Add(assetNode);

                // Add edge from engagement -> asset
                graph.Edges.Add(new Edge
                {
                    Source = engagementNode.Id,
                    Target = assetId,
                    Label = "contains",
                    Type = "contains"
                });
            }
            catch
            {
                // Skip malformed entries; journal is best-effort rebuild
            }
        }

        // Add findings from the journal as vulnerability nodes
        var findings = _journal.Read($"vulnerability/{engagement.Id}");
        foreach (var findingEntry in findings)
        {
            try
            {
                var findingPayload = System.Text.Json.JsonDocument.Parse(findingEntry.Payload);
                var root = findingPayload.RootElement;

                var vulnNode = new Node
                {
                    Id = Guid.NewGuid(),
                    Label = root.GetProperty("CveId").GetString() ?? $"Vuln {findingEntry.Sequence}",
                    Type = "vulnerability",
                    Title = root.GetProperty("CveId").GetString() ?? "Unknown vulnerability",
                    Description = root.GetProperty("AffectedComponent").GetString() ?? "",
                    Properties = new Dictionary<string, object>
                    {
                        ["cveId"] = root.GetProperty("CveId").GetString(),
                        ["cweId"] = root.GetProperty("CweId").GetString(),
                        ["cvssScore"] = root.GetProperty("CvssScore").GetDouble(),
                        ["severity"] = root.GetProperty("SeverityLevel").GetString()
                    }
                };
                graph.Nodes.Add(vulnNode);

                // Add edge from engagement -> finding
                graph.Edges.Add(new Edge
                {
                    Source = engagementNode.Id,
                    Target = vulnNode.Id,
                    Label = "contains",
                    Type = "contains"
                });
            }
            catch
            {
                // Skip malformed entries
            }
        }

        // Add test hypotheses as hypothesis nodes
        var hypotheses = _journal.Read($"hypothesis/{engagement.Id}");
        foreach (var hypEntry in hypotheses)
        {
            try
            {
                var hypPayload = System.Text.Json.JsonDocument.Parse(hypEntry.Payload);
                var root = hypPayload.RootElement;

                var hypNode = new Node
                {
                    Id = Guid.NewGuid(),
                    Label = $"Hyp {root.GetProperty("ProposedModule").GetString()}",
                    Type = "hypothesis",
                    Title = root.GetProperty("ProposedModule").GetString() ?? "Unknown hypothesis",
                    Description = root.GetProperty("Explanation").GetString() ?? "",
                    Properties = new Dictionary<string, object>
                    {
                        ["confidence"] = root.GetProperty("Confidence").GetDouble(),
                        ["priority"] = root.GetProperty("Priority").GetString(),
                        ["status"] = root.GetProperty("Status").GetString()
                    }
                };
                graph.Nodes.Add(hypNode);

                // Add edge from engagement -> hypothesis
                graph.Edges.Add(new Edge
                {
                    Source = engagementNode.Id,
                    Target = hypNode.Id,
                    Label = "hypothesis",
                    Type = "hypothesis"
                });
            }
            catch
            {
                // Skip malformed entries
            }
        }

        graph.LastUpdated = DateTime.UtcNow;
        return graph;
    }

    public async Task AddAssetRelationshipAsync(Asset source, Asset target, string relationshipType)
    {
        // Will be implemented via journal entries in future phases
        await Task.Yield();
    }

    public async Task AddVulnerabilityRelationshipAsync(Finding finding, Vulnerability vulnerability, string attackPath)
    {
        // Will be implemented via journal entries in future phases
        await Task.Yield();
    }

    public async Task<List<AttackPath>> FindAttackPathsAsync(Asset startingAsset)
    {
        // Basic attack-path finding: find all vulnerability nodes and create paths
        var paths = new List<AttackPath>();

        // In a full implementation, this would traverse the attack graph
        // from the starting asset through connected nodes to vulnerability nodes.
        // For now, create a single default path indicating the attack surface.

        paths.Add(new AttackPath
        {
            Id = Guid.NewGuid(),
            Name = $"Attack path from {startingAsset.AssetType}",
            Description = $"Attack path from asset {startingAsset.CanonicalIdentifier}",
            RiskScore = 5.0,
            Steps = new List<string>
            {
                $"Reconnaissance of {startingAsset.CanonicalIdentifier}",
                $"Vulnerability assessment",
                $"Exploitation attempt",
                $"Post-exploitation activities"
            },
            Mitigation = "Apply remediation guidance; update configuration; patch affected component"
        });

        return paths;
    }

    public async Task<NodePosition> GetNodePositionAsync(Guid nodeId)
    {
        await Task.Yield();
        return new NodePosition { X = 0, Y = 0, Z = 0 };
    }
}