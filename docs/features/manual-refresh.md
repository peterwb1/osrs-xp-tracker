# Manual refresh

**Effort:** M (weekend) · **Backend:** yes · **Depends on:** nothing · **Pairs well with:**
[improved charting](./improved-charting.md)

## Problem

The app polls the OSRS hiscores on a fixed **6-hour cycle** (a background service checks every 5
minutes for accounts whose `LastPolledAt` is older than the interval). If you just finished a
grind and want to see it reflected, you might wait up to 6 hours. There's no "check now" button.

Adding one raises two real questions, both answered below:

1. **How does it interact with the 6-hour auto-poll?**
2. **What does it do to the charts** if data points can now arrive close together?

## Approach

Add a `POST /api/accounts/{id}/refresh` endpoint that runs the **same poll logic** the background
service uses, guarded by a short server-side **cooldown** so the OSRS API isn't hammered. The
frontend gets a "Refresh" button that's disabled during the cooldown window.

### How it interacts with the 6-hour poll

This is the neat part: **no special coordination is needed.** The background service decides an
account is due when `LastPolledAt < (now - interval)`
(`api/OsrsTracker.Api/Services/PollingService.cs`). A manual refresh writes a new snapshot and
updates `LastPolledAt = now`. That automatically pushes the next auto-poll out by a full interval
— so a manual refresh just "resets the clock". No double-polling, no race to reconcile.

## Backend changes

**Files:**
- `api/OsrsTracker.Api/Services/PollingService.cs` — contains `PollAccountAsync` today.
- `api/OsrsTracker.Api/Controllers/AccountsController.cs` — new endpoint.
- `api/OsrsTracker.Domain/Models/PollLog.cs` — reuse for auditing.

### 1. Extract the poll logic into a shared service

Right now the logic to poll one account lives **inside** the `BackgroundService`. To call it from
a controller without duplicating it, lift it into an injectable service — e.g. `IAccountPoller`
with `Task<PollResult> PollAsync(int accountId, CancellationToken ct)`:

- Move the body of `PollAccountAsync` (fetch hiscores → parse → write 25 snapshots → update
  `LastPolledAt` → write `PollLog`) into the new service.
- `PollingService` becomes a thin scheduler that finds due accounts and calls `IAccountPoller`.
- The controller calls the same `IAccountPoller`.

This keeps a **single source of truth** for "how we poll an account" — the manual and automatic
paths can never drift.

### 2. The endpoint + cooldown

```csharp
[HttpPost("{id}/refresh")]
public async Task<IActionResult> Refresh(int id, ...)
{
    // 1. Load the account, 404 if not owned by the user (reuse existing ownership check).
    // 2. Cooldown: if LastPolledAt is within the cooldown window, return 429.
    var cooldown = TimeSpan.FromMinutes(5);
    if (account.LastPolledAt is { } last && DateTime.UtcNow - last < cooldown)
    {
        var retryAfter = (int)(cooldown - (DateTime.UtcNow - last)).TotalSeconds;
        Response.Headers.RetryAfter = retryAfter.ToString();
        return StatusCode(429, new { message = "Recently refreshed", retryAfter });
    }
    // 3. Poll via the shared service; return the fresh skills (or 200 + summary).
    await poller.PollAsync(id, ct);
    return Ok();
}
```

- **Cooldown (~5 min)** protects the OSRS API and prevents button-spam. The auto-poll's 2-second
  inter-account delay still applies inside the shared poll logic.
- **429 + `Retry-After`** is the correct, standard way to say "too soon" — the frontend can read
  it to show how long to wait.
- Reuse the existing `PollLog` writes so manual refreshes are audited exactly like auto-polls.

## Frontend changes

**Files:**
- `web/app/accounts/[id]/page.tsx` — add the button here (account detail).
- `web/lib/queryKeys.ts`, `web/lib/api.ts` — mutation + invalidation.

```tsx
const refresh = useMutation({
  mutationFn: () => api.post(`/api/accounts/${id}/refresh`),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: queryKeys.skills(id) });
    queryClient.invalidateQueries({ queryKey: ['accounts', id] }); // history + skills under this account
  },
});
```

- Show a "Refresh" button next to the account header; spinner while pending (reuse `Spinner`).
- **Disable during cooldown**: derive remaining cooldown from `lastPolledAt` (already on the
  account/skills payload) and disable the button with a tooltip like "Available in 4m". On a 429,
  read `retryAfter` and start a local countdown.

## Edge cases

- **Charts get clustered / uneven points.** Manual refreshes can add several snapshots within one
  day. The current chart buckets the X-axis by *day string*, so same-day points collapse to one
  label and look evenly spaced. This is exactly why [improved charting](./improved-charting.md)
  switches to a **time-based X-axis** — do that alongside this feature so clustered points render
  truthfully. Optionally de-dupe *identical consecutive* snapshots (XP unchanged) to avoid noise,
  though keeping them is harmless with a time axis.
- **OSRS API failure during a manual poll**: surface it — return a non-200 and show "Couldn't
  reach the hiscores, try again shortly". The `PollLog` already records the failure.
- **Concurrent refresh + auto-poll**: the cooldown plus `LastPolledAt` check makes a duplicate
  poll a no-op in practice; if you want belt-and-braces, guard with a short per-account lock.
- **Account never polled yet (`LastPolledAt == null`)**: allowed immediately (no cooldown to
  apply).

## Build order

1. Refactor `PollAccountAsync` into `IAccountPoller`; register it; make `PollingService` use it.
   Verify the background poll still works unchanged.
2. Add the `POST /{id}/refresh` endpoint with the cooldown + 429.
3. Add the frontend button, mutation, and invalidation.
4. Add the cooldown-aware disabled state + countdown.
5. (Recommended) Land the time-based X-axis from improved-charting so clustered points look right.

## Effort

Medium. The endpoint itself is small; the value (and the care) is in the refactor that makes the
poll logic reusable and in the cooldown/UX details.
