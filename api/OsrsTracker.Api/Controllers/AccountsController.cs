using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OsrsTracker.Api.Data;
using OsrsTracker.Domain.Hiscores;
using OsrsTracker.Domain.Models;

namespace OsrsTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public class AccountsController(AppDbContext db, IHiscoresClient hiscores) : ControllerBase
{
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
