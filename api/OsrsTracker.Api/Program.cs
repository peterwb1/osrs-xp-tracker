using Microsoft.EntityFrameworkCore;
using OsrsTracker.Api.Data;
using OsrsTracker.Api.Hiscores;
using OsrsTracker.Domain.Hiscores;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpClient<IHiscoresClient, HiscoresClient>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await SkillSeeder.SeedAsync(db);
}

app.MapControllers();

app.Run();
