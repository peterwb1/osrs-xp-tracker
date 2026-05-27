using OsrsTracker.Domain.Hiscores;

namespace OsrsTracker.Api.Hiscores;

public class HiscoresClient(HttpClient http) : IHiscoresClient
{
    public async Task<List<HiscoresEntry>> GetStatsAsync(string username, CancellationToken ct = default)
    {
        var url = $"https://secure.runescape.com/m=hiscore_oldschool/index_lite.ws?player={Uri.EscapeDataString(username)}";
        var csv = await http.GetStringAsync(url, ct);
        return HiscoresParser.Parse(csv);
    }
}
