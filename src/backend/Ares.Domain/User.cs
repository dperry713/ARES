using Ares.Contracts;
namespace Ares.Domain;

public sealed record User : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string ExternalIdentity { get; init; } = string.Empty;
    public string RoleAssignments { get; init; } = string.Empty;
    public string Status { get; init; } = "Active";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}