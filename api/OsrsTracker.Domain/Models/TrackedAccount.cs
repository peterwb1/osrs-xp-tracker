namespace OsrsTracker.Domain.Models;

public class TrackedAccount
{
    public int Id { get; set; }
    public required string OsrsUsername { get; set; }
    public required string DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastPolledAt { get; set; }

    public ICollection<XpSnapshot> XpSnapshots { get; set; } = [];
}
