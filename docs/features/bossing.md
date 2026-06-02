# Bossing information

**Effort:** L (multiple weekends) · **Backend:** substantial · **Depends on:** nothing
(pairs with [improved charting](./improved-charting.md) for the KC chart)

## Problem

OSRS tracks more than skills — it also records **boss kill-counts** and other activity scores
(clue scrolls, minigames, etc.). The same hiscores response we already fetch contains all of this,
but the app **throws it away**: only the 25 skills are stored. Players want to see boss KC history
the same way they see XP history — "how many Zulrah kills did I do this month?".

### What's already happening

`HiscoresParser` (`api/OsrsTracker.Domain/Hiscores/HiscoresParser.cs`) parses the *entire* CSV
into `HiscoresEntry` rows, and the polling service keeps only the first 25 (the skills, indexed by
`Skill.DisplayOrder`). Everything after index 24 — the activities and bosses — is parsed and
discarded. So the data is arriving; we just don't persist it.

## Approach

A full vertical slice that **mirrors the existing skills feature**: a reference table of
activities, a per-activity snapshot table, parser changes to read activity rows, a polling change
to persist them, two endpoints, and UI on the account page. This is the largest of the six
features because it touches the schema, requires a migration, and reworks the parser.

## Backend changes

**Files:**
- `api/OsrsTracker.Domain/Models/` — new `Activity` + `ActivitySnapshot` entities (mirroring
  `Skill` / `XpSnapshot`).
- `api/OsrsTracker.Domain/Hiscores/HiscoresParser.cs` + `HiscoresEntry.cs` — handle activity rows.
- `api/OsrsTracker.Api/Data/AppDbContext.cs` + a seeder (mirroring `SkillSeeder`).
- `api/OsrsTracker.Api/Services/` — extend the poll to write activity snapshots.
- `api/OsrsTracker.Api/Controllers/AccountsController.cs` — two new endpoints.
- New EF migration.

### 1. Reference entity: `Activity`

Mirror `Skill`:

```csharp
class Activity {
  int Id;
  string Name;            // "Zulrah", "Clue Scrolls (all)", "LMS - Rank", ...
  int DisplayOrder;       // index in the hiscores CSV, AFTER the 25 skills
  ActivityType Type;      // Boss | Minigame | Clue | Raid | Other (for grouping/filtering)
}
```

Seed it like skills are seeded today. **Crucially, the CSV order is fixed and published** by
Jagex: activities follow the 25 skills in a known sequence (clues, bounty hunter, LMS, soul wars,
rifts, then bosses A–Z, etc.). The seeder encodes `DisplayOrder` → name from that published
ordering. Because Jagex *appends* new bosses over time, the seeder must be **extendable** —
adding a new activity is appending a row, never renumbering existing ones.

### 2. Snapshot entity: `ActivitySnapshot`

Mirror `XpSnapshot`, but activities have **rank + score (kill-count)**, no level/XP:

```csharp
class ActivitySnapshot {
  int Id;
  int TrackedAccountId;
  int ActivityId;
  int Rank;          // -1 when unranked
  int Score;         // kill-count / clue count / minigame score; -1 when none
  DateTime CapturedAt;
}
// Composite index: (TrackedAccountId, ActivityId, CapturedAt) — same shape as XpSnapshot.
```

### 3. Parser change (the tricky bit)

Activity rows in the CSV have **only two fields** — `rank,score` — not the `rank,level,xp` triple
that skills use. The current parser assumes 3 fields. The parser must:

- Keep reading rows past index 24.
- For activity rows, parse `rank,score` (2 fields); there is no level/XP.
- Preserve order so `DisplayOrder` indexing still works for both skills and activities.

Easiest is to have the parser return all rows generically (rank + the remaining numeric fields),
and let the **polling service** decide: rows `0..24` → skills (use field as XP/level), rows `25+`
→ activities (use the second field as `Score`). Or split into `ParseSkills` / `ParseActivities`.

Handle the OSRS sentinel: `rank == -1` / `score == -1` means "no entry / unranked" — store as
`-1` (or null) and skip in the UI.

### 4. Polling change

Extend the shared poll (see [manual refresh](./manual-refresh.md), which lifts the poll into
`IAccountPoller`) so each poll writes **both** the 25 `XpSnapshot` rows *and* the
`ActivitySnapshot` rows, in the same transaction, with the same `CapturedAt`. No new fetch — it's
the same hiscores response.

### 5. Endpoints

Mirror the skills endpoints:

```
GET /api/accounts/{id}/activities
  → latest snapshot per activity: [{ activityId, name, type, score, rank }]

GET /api/accounts/{id}/activities/{activityId}/history?days=30
  → [{ capturedAt, score, rank }]   // reuse the `days` param convention
```

### 6. Migration

New tables `Activities` + `ActivitySnapshots` and their indexes. Existing accounts get activity
data from their **next poll onward** (no backfill — historical activity data was never stored and
can't be recovered).

## Frontend changes

**Files:**
- `web/app/accounts/[id]/page.tsx` — add a Skills/Bosses section or tabs.
- New: `web/app/accounts/[id]/activities/[activityId]/page.tsx` — KC history (reuse the chart).
- `web/lib/queryKeys.ts` — add `activities` / `activityHistory` keys.
- `web/lib/skills.ts` (icon helper) — an equivalent for activity icons.

- On the account detail page, add a **Bosses** view alongside skills: a table of activities (icon,
  kill-count, rank), filterable/groupable by `type`. Click an activity → KC history chart
  (reuses `XpHistoryChart` from [improved charting](./improved-charting.md), plotting `score`).
- Only show activities the account actually has (score ≥ 0).

## Edge cases

- **Activities the player has never done** are omitted/`-1` by the hiscores — don't show empty
  rows.
- **New bosses added by Jagex**: the CSV grows; the seeder must accept appended activities without
  disturbing existing `DisplayOrder` values. Guard the parser so unknown trailing rows don't crash
  it (store only activities the seeder knows about; log the rest).
- **Score isn't monotonic for everything**: kill-counts only go up, but some "activities" are
  ranks/points (e.g. LMS) — the `Type` field lets the UI label them correctly.
- **Index drift**: the whole feature relies on the CSV order matching the seeded `DisplayOrder`.
  Pin it to the published ordering and add a parser test with a captured sample response so a Jagex
  change is caught early.
- **Icons**: boss icons come from the OSRS wiki (same source as skill icons); names need
  URL-mapping/overrides like `skillIconUrl` does today.

## Build order

1. Add `Activity` + `ActivitySnapshot` entities, DbContext config, and the seeder; create the
   migration.
2. Rework the parser to read activity rows (`rank,score`) and add a parser test against a captured
   real response.
3. Extend the poll to persist activity snapshots (ideally after the manual-refresh refactor so
   there's one poll path).
4. Add the two endpoints.
5. Build the Bosses table + KC history page on the frontend.

## Effort

Large — this is the only feature that changes the database schema and reworks the parser. Do it
last; it benefits from the chart refactor (improved-charting) and the poll refactor
(manual-refresh) already being in place.
