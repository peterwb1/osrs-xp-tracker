namespace OsrsTracker.Domain.Stats;

/// <summary>
/// Calculates an OSRS combat level from the seven combat skill levels,
/// using Jagex's published formula.
/// </summary>
public static class CombatLevel
{
    public static int Calculate(
        int attack, int strength, int defence, int hitpoints,
        int ranged, int prayer, int magic)
    {
        var baseLevel = 0.25 * (defence + hitpoints + Math.Floor(prayer / 2.0));
        var melee = 0.325 * (attack + strength);
        var ranged2 = 0.325 * Math.Floor(ranged * 3 / 2.0);
        var magic2 = 0.325 * Math.Floor(magic * 3 / 2.0);

        var combat = baseLevel + Math.Max(melee, Math.Max(ranged2, magic2));
        return (int)Math.Floor(combat);
    }
}
