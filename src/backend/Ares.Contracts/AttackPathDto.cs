namespace Ares.Contracts;

public record AttackPathDto
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double RiskScore { get; init; }
    public List<string> Steps { get; init; } = new();
    public string? Mitigation { get; init; }
}