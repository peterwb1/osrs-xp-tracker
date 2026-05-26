using Microsoft.EntityFrameworkCore;
using OsrsTracker.Domain.Models;

namespace OsrsTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<TrackedAccount> TrackedAccounts => Set<TrackedAccount>();
    public DbSet<XpSnapshot> XpSnapshots => Set<XpSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<XpSnapshot>(e =>
        {
            e.HasIndex(s => new { s.TrackedAccountId, s.SkillId, s.CapturedAt });
        });

        modelBuilder.Entity<TrackedAccount>(e =>
        {
            e.Property(a => a.OsrsUsername).HasMaxLength(12);
            e.Property(a => a.DisplayName).HasMaxLength(50);
            e.HasIndex(a => a.OsrsUsername).IsUnique();
        });

        modelBuilder.Entity<Skill>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(50);
            e.HasIndex(s => s.Name).IsUnique();
        });
    }
}
