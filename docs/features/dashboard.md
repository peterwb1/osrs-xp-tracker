# Per-account dashboard

**Effort:** M (weekend) · **Backend:** optional · **Depends on:** nothing · **Pairs well with:**
[manual refresh](./manual-refresh.md)

## Problem

On desktop there's a lot of **unused horizontal space**. The accounts list page
(`web/app/accounts/page.tsx`, capped at `max-w-2xl`) is just an add-account form and a list of
accounts, leaving most of a wide screen empty. It would be nice to use that space for
at-a-glance widgets — last level-up, XP gained this week, and so on.

### The "which account?" problem

The obvious idea is to put widgets on the **accounts list** page. But that breaks down with many
accounts: if you track 50 accounts, *whose* "last level-up" does the dashboard show? There's no
single account in focus on a list page.

### The refinement

Put the dashboard on the **per-account page** (`web/app/accounts/[id]/page.tsx`) instead. There,
exactly one account is in focus, so "XP gained this week" is unambiguous. The skills table is
already there and doesn't fill a desktop width — the spare space beside it is the perfect home for
widgets.

**Constraint:** on **mobile**, the skills table must stay first/primary (that's the main thing you
open the page to see). Widgets stack *below* it on mobile, and move *beside* it on desktop.

## Approach

Restructure `web/app/accounts/[id]/page.tsx` into a responsive two-column layout. This introduces
the **first responsive breakpoints** in the codebase (currently none are used). Widgets reuse the
existing card pattern and are computed mostly from data already on the client.

## Frontend changes

**Files:**
- `web/app/accounts/[id]/page.tsx` — becomes a responsive layout host.
- New: `web/components/widgets/` — one small component per widget.
- Existing card token: `rounded-xl border border-gray-200 bg-white p-... shadow-sm dark:border-gray-700 dark:bg-gray-800`.

### Responsive layout (mobile-first)

```tsx
<div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
  {/* Skills table: full width on mobile, 2/3 on desktop, and FIRST in source order */}
  <section className="lg:col-span-2">
    <SkillsTable ... />
  </section>

  {/* Widgets: below the table on mobile, a sidebar column on desktop */}
  <aside className="grid grid-cols-2 gap-4 lg:col-span-1 lg:grid-cols-1">
    <LastLevelUpWidget ... />
    <XpGainedWidget range="week" ... />
    {/* ... */}
  </aside>
</div>
```

Because the skills `<section>` comes **first in the DOM**, mobile (single column) naturally shows
it on top; `lg:` turns on the two-column split where widgets sit beside it. Bump the page's
`max-w-3xl` up to something wider (e.g. `max-w-6xl`) so the desktop layout has room to breathe.

### Widget ideas

All reuse the card token; each is a small self-contained component:

| Widget | Source of data |
|--------|----------------|
| **Last level-up** | Compare latest vs previous snapshots per skill; find the most recent `level` increase. |
| **XP gained this week / today** | Overall-skill snapshot now minus the snapshot nearest N days ago. |
| **Fastest-growing skill** | Largest XP delta across skills over the selected window. |
| **Total level** | Sum of per-skill `level` (or the Overall row if it carries it). |
| **Combat level** | Computed from the combat skills' levels (standard OSRS formula). |
| **Last polled / next poll ETA** | `lastPolledAt` + the 6h interval; reuse `formatDistanceToNow`. |

Show a **placeholder** ("Not enough data yet") on widgets that need two snapshots when the account
only has one.

## Backend changes (optional)

Most widgets are derivable on the client from the existing `skills` payload plus a history fetch.
But "XP gained this week" across *all* skills naively means pulling a lot of history to the
client. If that gets heavy, add a small server-computed summary:

```
GET /api/accounts/{id}/summary
→ {
    totalXp, totalLevel, combatLevel,
    xpGainedToday, xpGainedThisWeek,
    fastestSkill: { skillId, gained },
    lastLevelUp: { skillId, level, at },
    lastPolledAt
  }
```

Compute it in `AccountsController` directly from `XpSnapshot` with grouped queries (the composite
index `(TrackedAccountId, SkillId, CapturedAt)` supports the "nearest snapshot N days ago"
lookups). This keeps the client simple and the payload tiny. Treat it as a later optimisation —
start client-side.

## Edge cases

- **Brand-new account (one snapshot)**: all delta widgets show placeholders; "total level" /
  "combat level" still work from the single snapshot.
- **Mobile priority**: verify the skills table renders above widgets at `<lg`; the source order
  above guarantees it, but check after any refactor.
- **Wide `max-w`**: don't let the skills table stretch awkwardly — cap its column or keep numbers
  in `tabular-nums` as today.
- **Pairs with manual refresh**: a "Refresh" button (see [manual refresh](./manual-refresh.md))
  fits naturally in the widget column header.

## Build order

1. Widen the page container and wrap the existing skills table in the responsive grid (table only,
   no widgets yet) — confirm mobile/desktop layout and that the table stays first on mobile.
2. Build widgets one at a time as client-side components from existing `skills` data
   (start with total level / combat level / last polled — no history needed).
3. Add delta widgets (XP this week, last level-up, fastest skill) using a history fetch.
4. (Optional) Move the heavy deltas server-side behind `GET /api/accounts/{id}/summary`.

## Effort

Medium. The responsive restructure is straightforward; the work is in building each widget and
deciding which deltas are worth a server endpoint.
