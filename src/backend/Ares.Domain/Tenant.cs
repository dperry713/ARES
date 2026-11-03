using Ares.Contracts;
namespace Ares.Domain;

public sealed record Tenant : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "Active";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Configuration { get; init; } = string.Empty;
    public string RetentionPolicy { get; init; } = "default";
}