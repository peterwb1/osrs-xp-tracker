using Microsoft.EntityFrameworkCore;
using OsrsTracker.Domain.Models;

namespace OsrsTracker.Api.Data;

public static class SkillSeeder
{
    private static readonly string[] SkillNames =
    [
        "Overall", "Attack", "Defence", "Strength", "Hitpoints", "Ranged", "Prayer",
        "Magic", "Cooking", "Woodcutting", "Fletching", "Fishing", "Firemaking",
        "Crafting", "Smithing", "Mining", "Herblore", "Agility", "Thieving",
        "Slayer", "Farming", "Runecraft", "Hunter", "Construction"
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Skills.AnyAsync()) return;

        var skills = SkillNames
            .Select((name, i) => new Skill { Name = name, DisplayOrder = i })
            .ToList();

        db.Skills.AddRange(skills);
        await db.SaveChangesAsync();
    }
}
