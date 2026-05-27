namespace OsrsTracker.Domain.Hiscores;

public static class HiscoresParser
{
    public static List<HiscoresEntry> Parse(string csv)
    {
        var entries = new List<HiscoresEntry>();

        foreach (var line in csv.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var parts = trimmed.Split(',');
            if (parts.Length < 3) continue;

            if (!int.TryParse(parts[0], out var rank) ||
                !int.TryParse(parts[1], out var level) ||
                !long.TryParse(parts[2], out var xp))
                continue;

            entries.Add(new HiscoresEntry(rank, level, xp));
        }

        return entries;
    }
}
