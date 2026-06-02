# Rank charting

**Effort:** S (afternoon) · **Backend:** none · **Depends on:**
[improved charting](./improved-charting.md)

## Problem

Each skill on the OSRS hiscores has a **world rank** (e.g. you're the 152,043rd highest in
Fishing). The app already records this rank in every snapshot, but it's only ever shown as a
single current number — there's no view of how your rank has moved over time. Rank is a satisfying
progress signal: it climbs even during stretches where the level doesn't change.

## Approach

Rank is **already stored and already returned by the API** — this is a pure frontend feature. Add
a rank view to the skill history page that reuses the chart component from
[improved charting](./improved-charting.md), plotting `rank` instead of `xp`, with the Y-axis
reversed so "up = better".

### Why no backend work?

The `XpSnapshot` entity (`api/OsrsTracker.Domain/Models/XpSnapshot.cs`) has a `Rank` column,
the polling service writes it on every poll, and the history endpoint
(`GET /api/accounts/{id}/skills/{skillId}/history`) already returns it in `SkillHistoryPointDto`:

```csharp
{ "capturedAt": ..., "xp": ..., "level": ..., "rank": ... }
```

So the rank time-series is sitting there unused on the client.

## Frontend changes

**Files:**
- `web/app/accounts/[id]/skills/[skillId]/page.tsx`
- `web/components/XpHistoryChart.tsx` (the reusable chart from improved-charting)

### 1. Metric toggle

Add a small segmented toggle near the chart: **XP ⇄ Rank**. Recommended over showing two charts
stacked — it keeps the page uncluttered and reuses the exact same container.

```tsx
const [metric, setMetric] = useState<'xp' | 'rank'>('xp');
```

Pass `metric` to the chart and pick the data key + formatting accordingly.

### 2. Reversed Y-axis for rank

Rank is **inverted** — rank 1 is the best, a *lower* number is *better*. A normal axis would draw
improvement as the line going *down*, which reads as "getting worse". Reverse it so improvement
goes up:

```tsx
<YAxis
  reversed
  domain={['dataMin', 'dataMax']}
  tickFormatter={(v: number) => v.toLocaleString('en-GB')}
  width={72}
/>
```

(`reversed` + `['dataMin','dataMax']` together: the smallest rank number sits at the top, and the
axis zooms to the range actually covered, same as the XP improvement from improved-charting.)

Rank numbers are large, so format with thousands separators and widen the axis (`width={72}`).
The tooltip should label the value as "Rank #..." not raw number.

## Edge cases

- **Unranked skills return `-1`.** OSRS reports `-1` for a skill the account isn't ranked in
  (very low level, below the hiscores cutoff). A `-1` mixed into real ranks would wreck the axis.
  Filter these out of the rank series (and if *every* point is `-1`, show an empty state like
  "Not ranked in this skill yet" instead of a chart).
- **Rank can jump around** even when XP is flat, because everyone else is also gaining. That's
  expected and is exactly the insight the chart provides — no smoothing needed.
- **Shared time range**: the range selector from improved-charting applies unchanged; rank uses
  the same `history` data already fetched, so switching XP⇄Rank needs **no extra request**.

## Build order

1. Land [improved charting](./improved-charting.md) first (reusable chart + time axis).
2. Add the `metric` toggle state and UI.
3. Teach `XpHistoryChart` to render either metric: pick `dataKey`, axis `reversed` flag, and
   formatter from `metric`.
4. Add the `-1` filter + "not ranked" empty state.

## Effort

Small — no backend, no new data fetching, mostly a toggle and an axis flag on top of the
improved-charting work.
