# Improved charting

**Effort:** S (afternoon) · **Backend:** optional · **Depends on:** nothing · **Unblocks:**
[rank charting](./rank-charting.md), [comparison](./comparison.md)

## Problem

The XP history chart on `web/app/accounts/[id]/skills/[skillId]/page.tsx` has two issues:

1. **The Y-axis doesn't frame the data well.** XP is slow to earn at high levels — if an account is
   at 80M XP and gains 100k, that gain is ~0.1% of the value. Because the axis spans a large range
   (and effectively starts low relative to the value), the line looks dead flat. You can't see the
   progress you actually made.

   > This isn't *always* wrong — some skills/accounts gain XP rapidly (low levels, or a heavy
   > grind), where a wider scale reads fine. So the fix should adapt to the data, not be a fixed
   > override.

2. **The time range is hardcoded to 30 days.** There's no way to look at the last 7 days or the
   full history. The request always sends `?days=30`.

   > Note: an account tracked for less than the selected range already just shows the shorter span
   > of data it has — that's fine and stays.

## Approach

This is a **frontend-only** change (with one optional backend tweak for an "all time" range). Pull
the chart out of the page into a reusable component, make the Y-axis fit the data, and add a
time-range selector wired to the already-existing `days` query param.

## Frontend changes

**Files:**
- `web/app/accounts/[id]/skills/[skillId]/page.tsx` — the current chart lives here inline.
- `web/lib/queryKeys.ts` — `skillHistory(accountId, skillId, days = 30)` already takes `days`.
- New: `web/components/XpHistoryChart.tsx` (extracted, reusable — rank + comparison will reuse it).

### 1. Y-axis that fits the data

Recharts' `<YAxis>` currently has no explicit `domain`, so it auto-scales. Switch to a
data-driven domain with a little padding so small gains are visible:

```tsx
<YAxis
  domain={['dataMin', 'dataMax']}
  tickFormatter={(v: number) => (v / 1_000_000).toFixed(2) + 'M'}
  width={56}
/>
```

`['dataMin', 'dataMax']` makes the chart frame only the range the data actually covers, so a
100k gain on 80M XP fills a visible slice of the chart instead of looking flat. Add a small pad
(e.g. compute `min - range*0.1` / `max + range*0.1` in the data transform) so the line isn't
glued to the chart edges.

Optionally add a **"Fit / From zero"** toggle so the user can flip back to a zero-based axis
(`domain={[0, 'dataMax']}`) when they want absolute context rather than zoomed-in deltas.

### 2. Time-range selector

Add a small segmented control above the chart:

```tsx
const RANGES = [
  { label: '7d', days: 7 },
  { label: '30d', days: 30 },
  { label: '90d', days: 90 },
  { label: 'All', days: 36500 }, // ~100 years; effectively unbounded
];
const [range, setRange] = useState(30);
```

Wire `range` into both the query key and the request — `queryKeys.skillHistory` already accepts
`days`, so each range is cached separately and switching is instant after the first fetch:

```tsx
const { data: history } = useQuery({
  queryKey: queryKeys.skillHistory(id, skillId, range),
  queryFn: () =>
    api.get(`/api/accounts/${id}/skills/${skillId}/history?days=${range}`).then((r) => r.data),
});
```

Style the buttons with the existing segmented look (reuse the card/border tokens; active button
gets `bg-blue-600 text-white`, inactive gets the muted style). Keep it usable on mobile.

### 3. Time-based X-axis

Today the X-axis uses a pre-formatted **day string** (`toLocaleDateString('en-GB', ...)`) as the
`dataKey`. That collapses multiple polls on the same day into one label and spaces points evenly
regardless of the real time gaps. Switch to a numeric/time axis:

```tsx
const chartData = history?.map((s) => ({
  t: new Date(s.capturedAt).getTime(),
  xp: s.xp,
}));

<XAxis
  dataKey="t"
  type="number"
  scale="time"
  domain={['dataMin', 'dataMax']}
  tickFormatter={(t) => new Date(t).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })}
/>
```

This makes points sit at their true position in time — important once
[manual refresh](./manual-refresh.md) can add several points within a single day.

## Edge cases

- **Sparse data**: a single data point still shows the existing empty-state message ("needs at
  least two polls"). Keep that guard.
- **"All" with a brand-new account**: just renders the couple of points it has — no special case.
- **Tick crowding on wide ranges**: with `scale="time"`, let Recharts pick ticks (or set
  `minTickGap`) so 90d/All don't overlap labels.
- **Flat series**: if `dataMin === dataMax` (no change in range), the padded domain avoids a
  zero-height axis; guard the pad so it's never `0`.

## Build order

1. Extract the inline chart into `web/components/XpHistoryChart.tsx` (props: `data`, `metric`,
   `yDomain`). No behaviour change yet — pure refactor.
2. Switch the X-axis to time-based and the Y-axis to `['dataMin','dataMax']` with padding.
3. Add the range selector state + buttons; wire into the query key.
4. (Optional) Add the "Fit / From zero" toggle.
5. (Optional backend) If you'd rather not send `days=36500` for "All", add an explicit unbounded
   mode to `GET /api/accounts/{id}/skills/{skillId}/history` (e.g. omit `days` ⇒ no lower bound)
   in `api/OsrsTracker.Api/Controllers/AccountsController.cs`.

## Effort

Small. The reusable-chart extraction is the bulk of the value because rank charting and
comparison both build on it. No required backend change.
