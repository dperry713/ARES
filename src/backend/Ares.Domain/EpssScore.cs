namespace Ares.Domain;

public class EpssScore
{
    public string CveId { get; set; } = string.Empty;
    public double Score { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}