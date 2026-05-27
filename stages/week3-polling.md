# Weekend 3 — Background Polling

## Goal

A background service runs continuously inside the API process, periodically fetching Hiscores for every tracked account and saving new XP snapshots. When the weekend is done, you can start the API, walk away, come back in 6 hours, and find fresh snapshot rows in the database — without making any API calls yourself. The poller is the entire point of the app: it's what turns a one-time snapshot into a history.

## Starting Point

- Auth is wired up (Weekend 2 complete)
- `TrackedAccount` has `LastPolledAt` (nullable `DateTime?`)
- Hiscores fetching logic exists in `HiscoresClient` and is used by `AccountsController`
- No background service exists yet
- No `PollLog` table exists yet

---

## Tasks

- [ ] Add `PollLog` model to `OsrsTracker.Domain/Models/PollLog.cs`:
  - `int Id`, `int TrackedAccountId`, `DateTime AttemptedAt`, `bool Success`, `string? ErrorMessage`
- [ ] Add `DbSet<PollLog> PollLogs` to `AppDbContext`; configure FK to `TrackedAccount`
- [ ] Add `Polling:IntervalHours` to `appsettings.json` (default `6`)
- [ ] Create `OsrsTracker.Api/Services/PollingOptions.cs` — a plain class with `int IntervalHours`
- [ ] Create `OsrsTracker.Api/Services/PollingService.cs` inheriting `BackgroundService`:
  - Inject `IServiceScopeFactory` and `IOptions<PollingOptions>` and `ILogger<PollingService>`
  - `ExecuteAsync`: loop until `stoppingToken.IsCancellationRequested`
  - Each iteration: create scope → query due accounts → poll each with 2s delay → sleep until next cycle
- [ ] Register in `Program.cs`: `builder.Services.AddHostedService<PollingService>()`
- [ ] Register options: `builder.Services.Configure<PollingOptions>(builder.Configuration.GetSection("Polling"))`
- [ ] Write unit tests for the "which accounts are due?" query logic (pass a list of accounts with different `LastPolledAt` values, assert correct ones are returned)
- [ ] Create new migration: `dotnet-ef migrations add AddPollLog --project OsrsTracker.Api --startup-project OsrsTracker.Api`

---

## Choices

### 1. BackgroundService vs IHostedService directly
| Option | What you implement | Notes |
|--------|--------------------|-------|
| `IHostedService` | `StartAsync`, `StopAsync` | Full control, more boilerplate |
| `BackgroundService` (abstract class) | `ExecuteAsync(CancellationToken)` | Implements `IHostedService` for you |

**Recommendation:** `BackgroundService`. It handles the start/stop plumbing and gives you a clean `ExecuteAsync` where you write your loop. Only go direct to `IHostedService` if you need fine-grained control over startup/shutdown ordering.

### 2. DbContext lifetime in the background service
This is the most important gotcha in this weekend.

`AppDbContext` is registered as `Scoped` (one instance per HTTP request). `BackgroundService` is registered as `Singleton` (one instance for the app lifetime). You **cannot** inject `AppDbContext` directly into the service — it will throw a runtime error or cause subtle bugs from sharing a DbContext across multiple poll cycles.

| Option | How |
|--------|-----|
| Manual scope per iteration | `using var scope = _scopeFactory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();` |
| Repository pattern | Inject a scoped repository interface — but this just moves the same problem one layer up |

**Recommendation:** Manual scope inside `ExecuteAsync` on each iteration. It's explicit, easy to follow, and correct.

### 3. Error handling strategy
| Option | Pros | Cons |
|--------|------|------|
| `try/catch` per account, log and continue | Simple, keeps polling running | No retry — a transient error gives up immediately |
| Polly retry policy | Handles transient failures gracefully | More dependency, more complexity |

**Recommendation:** `try/catch` for now. The README's "Definition of Done" calls for a rate-limited Hiscores client with retry on 503 — that's a later hardening step. For Weekend 3, log the error, write a `PollLog` row with `Success = false`, and move on to the next account.

### 4. Polling loop structure
| Option | Description |
|--------|-------------|
| All due accounts per iteration | Query all accounts with `LastPolledAt < now - interval`, poll them all, sleep |
| One account per tick | Poll the single most-stale account, short sleep, repeat |

**Recommendation:** All due accounts per iteration. It matches the mental model in the README and is simpler to reason about. At the scale of a personal project (tens of accounts, not thousands), processing them all in one pass is fine.

---

## Research Topics

**What is `BackgroundService`?**
An abstract base class in `Microsoft.Extensions.Hosting`. You inherit it and override `ExecuteAsync(CancellationToken stoppingToken)`. ASP.NET Core calls this method on startup in a background thread. Write a `while (!stoppingToken.IsCancellationRequested)` loop inside it. When the app shuts down, `stoppingToken` is cancelled, your loop condition becomes false, and the method exits cleanly.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await DoWorkAsync(stoppingToken);
        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
    }
}
```

**Why can't I inject `AppDbContext` directly into a Singleton?**
Dependency injection lifetime rules: a Singleton can only depend on things with equal or longer lifetime. `AppDbContext` is Scoped (shorter lifetime than Singleton). If you try, ASP.NET Core will throw a `InvalidOperationException` about captive dependency at startup.

The fix: inject `IServiceScopeFactory` (which IS a Singleton), then call `factory.CreateScope()` to get a new DI scope, and resolve `AppDbContext` from that scope. Dispose the scope when done.

**What is `IOptions<T>`?**
A pattern for strongly-typed configuration. Three steps:
1. Define a class: `public class PollingOptions { public int IntervalHours { get; set; } = 6; }`
2. Register: `services.Configure<PollingOptions>(config.GetSection("Polling"));`
3. Inject: `IOptions<PollingOptions> options` → read with `options.Value.IntervalHours`

This is better than `IConfiguration["Polling:IntervalHours"]` because it's typed, validates at startup, and is testable.

**What does `CancellationToken` do in this context?**
When ASP.NET Core shuts down (CTRL+C, container stop signal, etc.), it cancels the `stoppingToken`. If you pass it to `Task.Delay`, the delay wakes up immediately rather than waiting out the full sleep period. If you pass it to `db.SaveChangesAsync(ct)`, the database operation aborts cleanly instead of running into the void. Always pass it through.

**What HTTP status codes does the Hiscores API return?**
- `200 OK` — player found, CSV body with stats
- `404 Not Found` — username doesn't exist or account is deactivated
- `503 Service Unavailable` — Jagex hiscores are down for maintenance (happens during game updates)

Your `catch` blocks should handle `HttpRequestException` and check `.StatusCode` to distinguish these cases and write meaningful error messages to `PollLog`.

**What is `ILogger<T>`?**
ASP.NET Core's built-in structured logging interface. `_logger.LogInformation("Polled {Username}, {SkillCount} skills saved", username, count)`. In development, this outputs to the console. In production, you'd configure Serilog (mentioned in the README's "Definition of Done") to write structured JSON logs. Use it — logs from the background service will be your only visibility into what's happening.

---

## Key Patterns

### PollingService skeleton
```csharp
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
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // check every 5 min, poll every 6h
        }
    }

    private async Task PollDueAccountsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hiscores = scope.ServiceProvider.GetRequiredService<IHiscoresClient>();

        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(options.Value.IntervalHours);
        var due = await db.TrackedAccounts
            .Where(a => a.LastPolledAt == null || a.LastPolledAt < cutoff)
            .ToListAsync(ct);

        foreach (var account in due)
        {
            if (ct.IsCancellationRequested) break;
            await PollAccountAsync(db, hiscores, account, ct);
            await Task.Delay(TimeSpan.FromSeconds(2), ct); // be polite to Jagex
        }
    }
}
```

### appsettings.json addition
```json
"Polling": {
  "IntervalHours": 6
}
```

---

## Verify

```bash
# Temporarily change IntervalHours to 0 in appsettings.Development.json
# (so the poller immediately considers all accounts due)
# Start the API, add an account, wait ~10 seconds

# Check PollLog table in psql:
docker exec -it <postgres-container> psql -U osrs -d osrstracker
SELECT * FROM "PollLogs" ORDER BY "AttemptedAt" DESC LIMIT 5;

# Check XpSnapshots has multiple rows for same account:
SELECT COUNT(*), "TrackedAccountId" FROM "XpSnapshots" GROUP BY "TrackedAccountId";
```

Run tests: `dotnet test` — the "which accounts are due?" unit tests should pass.
