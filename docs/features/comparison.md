# Account comparison

**Effort:** M/L (weekend+) · **Backend:** optional · **Depends on:**
[improved charting](./improved-charting.md)

## Problem

You can track several accounts, but you can only ever look at them **one at a time**. There's no
way to put two (or more) side by side — e.g. compare two accounts racing the same skill, or see
which of your alts is gaining fastest. Comparison is one of the most motivating views a tracker
can offer, and right now it's missing entirely.

## Approach

A new `/compare` page: pick 2+ of your accounts, pick a metric (default **Overall XP**; optionally
any skill), and overlay each account as its own line on one chart, plus a small comparison table
of current values and gains. The history endpoint is already per-account/per-skill, so the client
can fetch each selected account in parallel and merge the series — **no required backend change**.

## Frontend changes

**Files:**
- New: `web/app/compare/page.tsx`.
- `web/lib/queryKeys.ts` — reuse `skillHistory` per account.
- `web/components/XpHistoryChart.tsx` — extend to render multiple `<Line>` series.
- Add a nav entry/link to `/compare` (there's no navbar today — a link from `/accounts` works).

### Selection UI

- Multi-select of the user's accounts (checkboxes or chips), sourced from the existing
  `GET /api/accounts`.
- A metric picker: **Overall XP** by default (the "Overall" skill, `DisplayOrder = 0`), with the
  option to choose any skill so all selected accounts are compared on the *same* skill.
- The shared time-range selector from [improved charting](./improved-charting.md).

### Parallel fetch with `useQueries`

```tsx
const results = useQueries({
  queries: selectedIds.map((id) => ({
    queryKey: queryKeys.skillHistory(id, skillId, range),
    queryFn: () =>
      api.get(`/api/accounts/${id}/skills/${skillId}/history?days=${range}`).then((r) => r.data),
  })),
});
```

This reuses the exact same endpoint and cache keys the single-account chart already uses — so an
account you just viewed is a free cache hit here.

### Series alignment (the hard part)

Accounts are polled on independent schedules, so their snapshot timestamps **don't line up**. You
can't just zip arrays. Merge into one dataset keyed by time, one value-field per account:

```tsx
// → [{ t: 1719000000000, acc12: 1_000_000, acc34: 980_000 }, { t: ..., acc12: ..., acc34: null }, ...]
```

Then render one `<Line>` per account against the shared time axis:

```tsx
{selectedIds.map((id, i) => (
  <Line key={id} dataKey={`acc${id}`} stroke={PALETTE[i % PALETTE.length]}
        connectNulls dot={false} />
))}
```

- The **time-based X-axis** from improved-charting is what makes this work — each account's points
  land at their true time, and `connectNulls` bridges the gaps where one account has no snapshot
  at another's timestamp.
- Use a fixed colour **palette** and show a legend mapping colour → account display name.

### Comparison table

Below the chart, a compact table for the selected accounts: current value, gained over the range,
and (for skill comparisons) current rank. Reuse the card/table styling from the account detail
page.

## Backend changes (optional)

None to start — the client fan-out is fine for a handful of accounts. If users routinely compare
many accounts and the parallel requests become a bottleneck, add a batch endpoint later:

```
POST /api/compare  { accountIds: [...], skillId, days }
→ per-account history in one round trip
```

Keep this as a future optimisation; don't build it up front.

## Edge cases

- **Different tracking start dates**: an account added last week simply has a shorter line; the
  shared axis spans the union of all series and `connectNulls` handles the missing early region.
- **Too many accounts selected**: cap the selection (e.g. 5–6) so colours stay distinguishable and
  the fan-out stays small; show a hint when the cap is hit.
- **Same-skill requirement**: comparison only makes sense on one metric at a time — all selected
  accounts are charted on the chosen skill. Make that explicit in the UI.
- **Unranked (`-1`) on rank comparisons**: filter as in [rank charting](./rank-charting.md).
- **One account selected**: just render the normal single series (or prompt to pick another).

## Build order

1. Land [improved charting](./improved-charting.md) (time axis + reusable multi-capable chart).
2. Build `/compare` with account multi-select + a fixed metric (Overall XP).
3. Implement the merge-by-time transform and multi-line rendering with a colour palette + legend.
4. Add the metric picker (any skill) and the comparison table.
5. (Optional) Batch `POST /api/compare` endpoint if fan-out becomes a problem.

## Effort

Medium-to-large. The selection UI and table are routine; the **time-series merge/alignment** is
the genuinely fiddly part, and it gets much easier once the improved-charting time axis exists.
