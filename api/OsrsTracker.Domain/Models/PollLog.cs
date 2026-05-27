namespace OsrsTracker.Domain.Models;

public class PollLog
{
    public int Id { get; set; }
    public int TrackedAccountId { get; set; }
    public DateTime AttemptedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public TrackedAccount TrackedAccount { get; set; } = null!;
}
