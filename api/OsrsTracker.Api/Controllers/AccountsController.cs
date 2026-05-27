using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OsrsTracker.Api.Data;
using OsrsTracker.Api.Dtos;
using OsrsTracker.Domain.Hiscores;
using OsrsTracker.Domain.Models;

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
