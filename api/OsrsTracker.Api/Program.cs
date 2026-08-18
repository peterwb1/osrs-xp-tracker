using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OsrsTracker.Api.Data;
using OsrsTracker.Api.Hiscores;
using OsrsTracker.Api.Services;
using OsrsTracker.Domain.Hiscores;
using Polly;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// OpenAPI / Swagger. Publicly exposed (see UseSwaggerUI below): the doc only
// describes the API — every protected endpoint still requires a JWT. The
// Authorize button lets you paste a token so "try it out" works.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OSRS XP Tracker API",
        Version = "v1",
        Description = "Most endpoints require a JWT. Log in via POST /api/auth/login, "
            + "copy the returned token, click Authorize, and paste it in."
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste your JWT here (no 'Bearer ' prefix needed).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", jwtScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwtScheme] = Array.Empty<string>() });
});

// Rate limiting for the auth endpoints, partitioned per client IP, so a public
// Swagger page can't be used to brute-force logins or spam registrations.
// Effectively disabled under the "Testing" environment so integration tests
// (which register many users quickly) aren't throttled.
var authPermitLimit = builder.Environment.IsEnvironment("Testing") ? 10_000 : 10;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
    {
        // Behind Container Apps the real client is the first entry of
        // X-Forwarded-For; fall back to the socket address when running locally.
        // Best-effort (the header is spoofable) — fine for a demo, not a hard guarantee.
        var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpClient<IHiscoresClient, HiscoresClient>()
    .AddTransientHttpErrorPolicy(p =>
        p.WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(retry * 2)));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 8;
    // NOTE: tighten password rules before production
})
.AddEntityFrameworkStores<AppDbContext>();

// AddIdentity sets DefaultAuthenticateScheme/DefaultChallengeScheme to cookies.
// We must override them here so JWT is used instead.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

builder.Services.Configure<PollingOptions>(builder.Configuration.GetSection("Polling"));
builder.Services.AddScoped<IAccountPoller, AccountPoller>();
builder.Services.AddHostedService<PollingService>();

builder.Services.AddHealthChecks();

var frontendUrl = builder.Configuration["Frontend:Url"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = new List<string> { "http://localhost:3000" };
        if (!string.IsNullOrEmpty(frontendUrl))
            origins.Add(frontendUrl);
        policy.WithOrigins(origins.ToArray()).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await SkillSeeder.SeedAsync(db);
}

var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unknown";

// Swagger is served publicly (all environments) so the demo can be shown remotely.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "OSRS XP Tracker API v1");
    options.DocumentTitle = "OSRS XP Tracker API";
});

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapGet("/api/info", () => new { version, environment = app.Environment.EnvironmentName });
app.MapControllers();

app.Run();

// Needed for WebApplicationFactory in integration tests
public partial class Program { }
