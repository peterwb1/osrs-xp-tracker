namespace OsrsTracker.Domain.Models;

public class Skill
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<XpSnapshot> XpSnapshots { get; set; } = [];
}
