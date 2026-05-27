using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OsrsTracker.Api.Data;
using OsrsTracker.Domain.Hiscores;
using OsrsTracker.Domain.Models;

namespace OsrsTracker.Api.Services;

public class PollingService(
    IServiceScopeFactory scopeFactory,
    IOptions<PollingOptions> options,
    ILogger<PollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Polling service started. Interval: {Hours}h", options.Value.IntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollDueAccountsAsync(stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown signal received — exit the loop cleanly
                break;
            }
        }

        logger.LogInformation("Polling service stopped.");
    }

    private async Task PollDueAccountsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hiscores = scope.ServiceProvider.GetRequiredService<IHiscoresClient>();

        var cutoff = DateTime.UtcNow.AddHours(-options.Value.IntervalHours);
        // IsDue() can't be used here — EF Core can't translate arbitrary C# method calls
        // to SQL. Inline the expression so EF Core can generate the WHERE clause.
        var due = await db.TrackedAccounts
            .Where(a => a.LastPolledAt == null || a.LastPolledAt < cutoff)
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            logger.LogDebug("No accounts due for polling.");
            return;
        }

        logger.LogInformation("Polling {Count} due account(s).", due.Count);

        foreach (var account in due)
        {
            if (ct.IsCancellationRequested) break;
            await PollAccountAsync(db, hiscores, account, ct);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollAccountAsync(
        AppDbContext db,
        IHiscoresClient hiscores,
        TrackedAccount account,
        CancellationToken ct)
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
        }
    }

    // Internal so unit tests can call it directly without constructing the service.
    internal static bool IsDue(TrackedAccount account, DateTime cutoff) =>
        account.LastPolledAt == null || account.LastPolledAt < cutoff;
}
