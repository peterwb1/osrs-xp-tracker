namespace OsrsTracker.Domain.Hiscores;

public interface IHiscoresClient
{
    Task<List<HiscoresEntry>> GetStatsAsync(string username, CancellationToken ct = default);
}
