using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OsrsTracker.Api.Data;
using OsrsTracker.Domain.Hiscores;

namespace OsrsTracker.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Connection string pointing at the test DB container (port 5433)
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=osrstracker_test;Username=osrs;Password=osrs";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-only-secret-key-needs-32-chars-padding!",
                ["Jwt:Issuer"] = "osrs-tracker-test"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace real DbContext with test DB
            var dbDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null)
                services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(TestConnectionString));

            // Replace real HiscoresClient with a fake so tests don't hit the OSRS API
            var hiscoresDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IHiscoresClient));
            if (hiscoresDescriptor != null)
                services.Remove(hiscoresDescriptor);

            services.AddSingleton<IHiscoresClient, FakeHiscoresClient>();
        });
    }

    /// <summary>
    /// Applies migrations and clears all user/account data.
    /// Call this at the start of each test to start with a clean slate.
    /// </summary>
    public void ResetDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Database.Migrate();

        db.XpSnapshots.RemoveRange(db.XpSnapshots);
        db.TrackedAccounts.RemoveRange(db.TrackedAccounts);
        db.Users.RemoveRange(db.Users);
        db.SaveChanges();
    }
}

/// <summary>
/// Returns 24 fake skill entries so AccountsController tests don't call the real OSRS API.
/// </summary>
public class FakeHiscoresClient : IHiscoresClient
{
    public Task<List<HiscoresEntry>> GetStatsAsync(string username, CancellationToken ct = default)
    {
        var entries = Enumerable.Range(0, 24)
            .Select(_ => new HiscoresEntry(Rank: 100, Level: 99, Xp: 13_034_431))
            .ToList();
        return Task.FromResult(entries);
    }
}
