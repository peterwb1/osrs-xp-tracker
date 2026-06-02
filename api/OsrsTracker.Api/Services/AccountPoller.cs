using Microsoft.EntityFrameworkCore;
using OsrsTracker.Api.Data;
using OsrsTracker.Domain.Hiscores;
using OsrsTracker.Domain.Models;

namespace OsrsTracker.Api.Services;

public record PollResult(bool Success, int SkillCount, string? Error);

/// <summary>
/// Polls a single tracked account: fetches the OSRS hiscores, writes a snapshot
/// per skill, updates <see cref="TrackedAccount.LastPolledAt"/>, and records a
/// <see cref="PollLog"/>. Shared by the background <see cref="PollingService"/>
/// and the manual-refresh endpoint so both paths behave identically.
/// </summary>
public interface IAccountPoller
{
    Task<PollResult> PollAsync(TrackedAccount account, CancellationToken ct);
}

public class AccountPoller(
    AppDbContext db,
    IHiscoresClient hiscores,
    ILogger<AccountPoller> logger) : IAccountPoller
{
    public async Task<PollResult> PollAsync(TrackedAccount account, CancellationToken ct)
    {
        try
        {
            var stats = await hiscores.GetStatsAsync(account.OsrsUsername, ct);

            var skills = await db.Skills.OrderBy(s => s.DisplayOrder).ToListAsync(ct);
            var now = DateTime.UtcNow;

            var snapshots = skills
                .Where(s => s.DisplayOrder < stats.Count)
                .Select(s =>
                {
                    var entry = stats[s.DisplayOrder];
                    return new XpSnapshot
                    {
                        TrackedAccountId = account.Id,
                        SkillId = s.Id,
                        Xp = entry.Xp,
                        Level = entry.Level,
                        Rank = entry.Rank,
                        CapturedAt = now
                    };
                })
                .ToList();

            db.XpSnapshots.AddRange(snapshots);
            account.LastPolledAt = now;
            db.PollLogs.Add(new PollLog
            {
                TrackedAccountId = account.Id,
                AttemptedAt = now,
                Success = true
            });

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Polled {Username}: {SkillCount} skills saved.",
                account.OsrsUsername, snapshots.Count);

            return new PollResult(true, snapshots.Count, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var now = DateTime.UtcNow;
            db.PollLogs.Add(new PollLog
            {
                TrackedAccountId = account.Id,
                AttemptedAt = now,
                Success = false,
                ErrorMessage = ex.Message
            });

            try { await db.SaveChangesAsync(ct); }
            catch { /* if DB is also down, just move on */ }

            logger.LogWarning(
                "Failed to poll {Username}: {Error}",
                account.OsrsUsername, ex.Message);

            return new PollResult(false, 0, ex.Message);
        }
    }
}
