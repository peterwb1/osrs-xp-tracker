using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OsrsTracker.Domain.Models;

namespace OsrsTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<TrackedAccount> TrackedAccounts => Set<TrackedAccount>();
    public DbSet<XpSnapshot> XpSnapshots => Set<XpSnapshot>();
    public DbSet<PollLog> PollLogs => Set<PollLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // required — sets up Identity tables

        modelBuilder.Entity<XpSnapshot>(e =>
        {
            e.HasIndex(s => new { s.TrackedAccountId, s.SkillId, s.CapturedAt });
        });

        modelBuilder.Entity<TrackedAccount>(e =>
        {
            e.Property(a => a.OsrsUsername).HasMaxLength(12);
            e.Property(a => a.DisplayName).HasMaxLength(50);
            e.HasIndex(a => new { a.UserId, a.OsrsUsername }).IsUnique();
            e.HasOne<IdentityUser>()
             .WithMany()
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollLog>(e =>
        {
            e.HasOne(p => p.TrackedAccount)
             .WithMany(a => a.PollLogs)
             .HasForeignKey(p => p.TrackedAccountId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Skill>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(50);
            e.HasIndex(s => s.Name).IsUnique();
        });
    }
}
