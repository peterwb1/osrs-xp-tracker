using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OsrsTracker.Api.Data;
using OsrsTracker.Api.Dtos;
using OsrsTracker.Api.Services;
using OsrsTracker.Domain.Hiscores;
using OsrsTracker.Domain.Models;
using OsrsTracker.Domain.Stats;

namespace OsrsTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public class AccountsController(AppDbContext db, IHiscoresClient hiscores) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAccounts(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var accounts = await db.TrackedAccounts
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.DisplayName)
            .Select(a => new AccountSummaryDto(a.Id, a.OsrsUsername, a.DisplayName, a.CreatedAt, a.LastPolledAt))
            .ToListAsync(ct);
        return Ok(accounts);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var account = await db.TrackedAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (account is null) return NotFound();
        db.TrackedAccounts.Remove(account);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id}/skills")]
    public async Task<IActionResult> GetSkills(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var account = await db.TrackedAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (account is null) return NotFound();

        var skills = await db.Skills.OrderBy(s => s.DisplayOrder).ToListAsync(ct);

        var latestSnapshots = await db.XpSnapshots
            .Where(s => s.TrackedAccountId == id)
            .GroupBy(s => s.SkillId)
            .Select(g => g.OrderByDescending(s => s.CapturedAt).First())
            .ToListAsync(ct);

        var snapshotBySkill = latestSnapshots.ToDictionary(s => s.SkillId);

        var result = skills.Select(skill =>
        {
            snapshotBySkill.TryGetValue(skill.Id, out var snap);
            return new SkillSnapshotDto(skill.Id, skill.Name, skill.DisplayOrder,
                snap?.Xp, snap?.Level, snap?.Rank);
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}/skills/{skillId}/history")]
    public async Task<IActionResult> GetSkillHistory(
        int id, int skillId, [FromQuery] int days = 30, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var account = await db.TrackedAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (account is null) return NotFound();

        var since = DateTime.UtcNow.AddDays(-days);
        var history = await db.XpSnapshots
            .Where(s => s.TrackedAccountId == id && s.SkillId == skillId && s.CapturedAt >= since)
            .OrderBy(s => s.CapturedAt)
            .Select(s => new SkillHistoryPointDto(s.CapturedAt, s.Xp, s.Level, s.Rank))
            .ToListAsync(ct);

        return Ok(history);
    }

    [HttpGet("{id}/summary")]
    public async Task<IActionResult> GetSummary(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var account = await db.TrackedAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (account is null) return NotFound();

        var skillNameById = await db.Skills.ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var overallSkillId = skillNameById.First(kv => kv.Value == "Overall").Key;

        // Latest snapshot per skill = current values.
        var latest = await db.XpSnapshots
            .Where(s => s.TrackedAccountId == id)
            .GroupBy(s => s.SkillId)
            .Select(g => g.OrderByDescending(s => s.CapturedAt).First())
            .ToListAsync(ct);

        if (latest.Count == 0)
            return Ok(new AccountDashboardDto(account.DisplayName, account.OsrsUsername,
                account.LastPolledAt, 0, 0, 0, 0, 0, null, null));

        var currentByName = latest.ToDictionary(s => skillNameById[s.SkillId]);
        int Lvl(string name) => currentByName.TryGetValue(name, out var s) ? s.Level : 1;

        currentByName.TryGetValue("Overall", out var overall);
        var totalLevel = overall?.Level ?? 0;
        var totalXp = overall?.Xp ?? 0;

        var combat = CombatLevel.Calculate(
            attack: Lvl("Attack"), strength: Lvl("Strength"), defence: Lvl("Defence"),
            hitpoints: Lvl("Hitpoints"), ranged: Lvl("Ranged"), prayer: Lvl("Prayer"),
            magic: Lvl("Magic"));

        // Earliest snapshot per skill — the fallback baseline for accounts that
        // haven't been tracked for the full comparison window yet.
        var earliestBySkillId = (await db.XpSnapshots
                .Where(s => s.TrackedAccountId == id)
                .GroupBy(s => s.SkillId)
                .Select(g => g.OrderBy(s => s.CapturedAt).First())
                .ToListAsync(ct))
            .ToDictionary(s => s.SkillId);

        var now = DateTime.UtcNow;
        var weekBaseline = await BaselineAtAsync(id, now.AddDays(-7), ct);
        var dayBaseline = await BaselineAtAsync(id, now.AddDays(-1), ct);

        long BaselineXp(Dictionary<int, XpSnapshot> baseline, int skillId) =>
            (baseline.TryGetValue(skillId, out var b)
                ? b
                : earliestBySkillId.GetValueOrDefault(skillId))?.Xp ?? 0;

        var xpGainedThisWeek = Math.Max(0, totalXp - BaselineXp(weekBaseline, overallSkillId));
        var xpGainedToday = Math.Max(0, totalXp - BaselineXp(dayBaseline, overallSkillId));

        // Fastest-growing skill over the week (excluding the Overall aggregate).
        DashboardSkillGainDto? fastest = null;
        foreach (var snap in latest)
        {
            if (snap.SkillId == overallSkillId) continue;
            var gained = snap.Xp - BaselineXp(weekBaseline, snap.SkillId);
            if (gained > 0 && (fastest is null || gained > fastest.XpGained))
                fastest = new DashboardSkillGainDto(skillNameById[snap.SkillId], gained);
        }

        // Most recent level-up across skills in the last 30 days. Levels only go
        // up, so a level increase between two consecutive snapshots is a level-up.
        var levelWindow = now.AddDays(-30);
        var levelRows = await db.XpSnapshots
            .Where(s => s.TrackedAccountId == id && s.SkillId != overallSkillId && s.CapturedAt >= levelWindow)
            .OrderBy(s => s.CapturedAt)
            .Select(s => new { s.SkillId, s.Level, s.CapturedAt })
            .ToListAsync(ct);

        DashboardLevelUpDto? lastLevelUp = null;
        foreach (var grp in levelRows.GroupBy(r => r.SkillId))
        {
            var ordered = grp.OrderBy(r => r.CapturedAt).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Level > ordered[i - 1].Level &&
                    (lastLevelUp is null || ordered[i].CapturedAt > lastLevelUp.At))
                {
                    lastLevelUp = new DashboardLevelUpDto(
                        skillNameById[grp.Key], ordered[i].Level, ordered[i].CapturedAt);
                }
            }
        }

        return Ok(new AccountDashboardDto(
            account.DisplayName, account.OsrsUsername, account.LastPolledAt,
            totalLevel, totalXp, combat, xpGainedToday, xpGainedThisWeek,
            fastest, lastLevelUp));
    }

    // Latest snapshot per skill at or before <paramref name="cutoff"/>.
    private async Task<Dictionary<int, XpSnapshot>> BaselineAtAsync(
        int accountId, DateTime cutoff, CancellationToken ct) =>
        (await db.XpSnapshots
            .Where(s => s.TrackedAccountId == accountId && s.CapturedAt <= cutoff)
            .GroupBy(s => s.SkillId)
            .Select(g => g.OrderByDescending(s => s.CapturedAt).First())
            .ToListAsync(ct))
        .ToDictionary(s => s.SkillId);

    // How soon after the last poll a manual refresh is allowed, to respect the OSRS API.
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(5);

    [HttpPost("{id}/refresh")]
    public async Task<IActionResult> Refresh(
        int id, [FromServices] IAccountPoller poller, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var account = await db.TrackedAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (account is null) return NotFound();

        // Cooldown: a manual refresh also resets LastPolledAt, which pushes the
        // next automatic poll back by a full interval — so the two never collide.
        if (account.LastPolledAt is { } last)
        {
            var elapsed = DateTime.UtcNow - last;
            if (elapsed < RefreshCooldown)
            {
                var retryAfter = (int)Math.Ceiling((RefreshCooldown - elapsed).TotalSeconds);
                Response.Headers.RetryAfter = retryAfter.ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { error = "Recently refreshed. Try again shortly.", retryAfter });
            }
        }

        var result = await poller.PollAsync(account, ct);
        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "Couldn't reach the OSRS Hiscores. Try again shortly." });

        return Ok(new { polledAt = account.LastPolledAt, skillCount = result.SkillCount });
    }

    [HttpPost]
    public async Task<IActionResult> AddAccount([FromBody] AddAccountRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No user ID in token");

        var username = request.OsrsUsername.Trim();

        List<HiscoresEntry> stats;
        try
        {
            stats = await hiscores.GetStatsAsync(username, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new { error = $"OSRS account '{username}' not found on Hiscores." });
        }

        if (stats.Count == 0)
            return BadRequest(new { error = "Hiscores returned no data." });

        if (await db.TrackedAccounts.AnyAsync(a => a.OsrsUsername == username && a.UserId == userId, ct))
            return Conflict(new { error = $"'{username}' is already being tracked." });

        var skills = await db.Skills.OrderBy(s => s.DisplayOrder).ToListAsync(ct);

        var account = new TrackedAccount
        {
            OsrsUsername = username,
            DisplayName = request.DisplayName ?? username,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            LastPolledAt = DateTime.UtcNow
        };

        db.TrackedAccounts.Add(account);
        await db.SaveChangesAsync(ct);

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
                    CapturedAt = DateTime.UtcNow
                };
            })
            .ToList();

        db.XpSnapshots.AddRange(snapshots);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(AddAccount), new
        {
            id = account.Id,
            osrsUsername = account.OsrsUsername,
            displayName = account.DisplayName,
            createdAt = account.CreatedAt,
            skillCount = snapshots.Count,
            totalXp = snapshots.FirstOrDefault(s => skills.First(k => k.Id == s.SkillId).Name == "Overall")?.Xp
        });
    }
}

public record AddAccountRequest(string OsrsUsername, string? DisplayName);
