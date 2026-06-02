# Feature Ideas & Design Docs

This folder holds design docs for **planned improvements** to the OSRS XP tracker. They are
proposals, not shipped features. Each doc thinks a feature through end-to-end — the problem it
solves, the approach, the concrete backend/frontend changes (referencing real files), edge
cases, and a phased build order — so it can be picked up and built (or converted into a GitHub
issue) later.

These were written against the codebase as it stands today, where:

- XP snapshots already store **rank** alongside XP/level for every skill.
- The OSRS hiscores response already contains **boss/activity** data, but it's currently discarded.
- The history endpoint already accepts a `days` query param.
- The frontend uses **Recharts** and has **no responsive breakpoints** yet.

## The six features

| # | Feature | Effort | One-liner |
|---|---------|--------|-----------|
| 1 | [Improved charting](./improved-charting.md) | S | Sensible Y-axis zoom + selectable time ranges instead of a fixed 30 days. |
| 2 | [Rank charting](./rank-charting.md) | S | Chart your rank over time (the data is already stored). |
| 3 | [Manual refresh](./manual-refresh.md) | M | An on-demand "check now" button, with a cooldown, alongside the 6-hour auto-poll. |
| 4 | [Per-account dashboard](./dashboard.md) | M | Use the empty desktop space for at-a-glance widgets per account. |
| 5 | [Account comparison](./comparison.md) | M/L | Select 2+ accounts and overlay their progress. |
| 6 | [Bossing information](./bossing.md) | L | Track boss kill-counts over time (new tables + parser work). |

Effort: **S** ≈ an afternoon, **M** ≈ a weekend, **L** ≈ multiple weekends.

## Suggested build order

The features have soft dependencies — the charting work is foundational, so doing it first makes
the rank and comparison features much cheaper.

1. **Improved charting** — pure frontend; refactors the chart into something reusable and adds a
   time-based X-axis that the next two features lean on.
2. **Rank charting** — the data already exists, so this is mostly a chart toggle on top of #1.
3. **Manual refresh** — first backend change; refactors the polling code so it can be reused, and
   surfaces the "uneven data points" problem that #1 already handles.
4. **Per-account dashboard** — introduces the first responsive layout and reuses existing snapshot
   data for widgets.
5. **Account comparison** — multi-series chart; far easier once #1's time-based axis exists.
6. **Bossing** — the biggest slice: new reference + snapshot tables, a migration, parser changes,
   polling changes, two endpoints, and new UI. Best saved for last.

## Doc structure

Each doc follows the same shape:

- **Problem** — what's wrong / missing today.
- **Approach** — the chosen design in one paragraph.
- **Backend changes** — entities, endpoints, services (with file paths).
- **Frontend changes** — pages, components, queries (with file paths).
- **Edge cases** — the awkward bits to handle.
- **Build order** — phased steps.
- **Effort** — rough size and what dominates it.
