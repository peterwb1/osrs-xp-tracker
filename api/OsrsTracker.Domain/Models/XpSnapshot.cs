namespace OsrsTracker.Domain.Models;

public class XpSnapshot
{
    public int Id { get; set; }
    public int TrackedAccountId { get; set; }
    public int SkillId { get; set; }
    public long Xp { get; set; }
    public int Level { get; set; }
    public int Rank { get; set; }
    public DateTime CapturedAt { get; set; }

    public TrackedAccount TrackedAccount { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}
