# Weekend 5 — Frontend Properly

## Goal

By Sunday the app is actually usable. You can log in, see your tracked accounts, click into one, and see a table of all 23 skills with current level, XP, and rank. Clicking a skill shows a line chart of XP over time. The data comes from real API calls. This weekend is where the skeleton from Weekend 4 becomes a working product.

## Starting Point

- Backend: all four read endpoints working (`GET /api/accounts`, `DELETE /api/accounts/{id}`, `GET /api/accounts/{id}/skills`, `GET /api/accounts/{id}/skills/{skillId}/history`)
- Frontend: Vite + React + TypeScript project exists, auth flow wired (register, login, JWT stored), skeleton pages render but show no real data
- `AccountListPage` shows "Loading..." or nothing, `AccountDetailPage` is empty

---

## Tasks

- [ ] Account list page: fetch accounts via `useQuery`, display `OsrsUsername`, `DisplayName`, `LastPolledAt` as relative time
- [ ] "Add account" form on the list page: `useMutation` to `POST /api/accounts`, `invalidateQueries` on success
- [ ] Delete button per account: `useMutation` to `DELETE /api/accounts/{id}`, `invalidateQueries` on success
- [ ] Account detail page: fetch skills via `useQuery`, display all 24 skills in a table (skill name, level, XP formatted, rank formatted)
- [ ] Skill history page (or modal): fetch history via `useQuery`, render a line chart showing XP over time
- [ ] "Last polled at" display — human-readable relative time (e.g. "3 hours ago")
- [ ] Loading states — skeleton placeholders or spinner while data loads
- [ ] Empty states — message when no accounts tracked, message when no snapshots exist for a skill
- [ ] Error states — show API error messages in the UI

---

## Choices

### 1. Charting library: Recharts vs Chart.js vs Nivo

**Option A — Recharts**
```tsx
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';

// data shape: [{ date: '2026-05-01', xp: 4200000 }, ...]
export function XpChart({ data }: { data: { date: string; xp: number }[] }) {
  return (
    <ResponsiveContainer width="100%" height={300}>
      <LineChart data={data}>
        <CartesianGrid strokeDasharray="3 3" />
        <XAxis dataKey="date" />
        <YAxis tickFormatter={(v) => (v / 1_000_000).toFixed(1) + 'M'} />
        <Tooltip formatter={(v: number) => v.toLocaleString()} />
        <Line type="monotone" dataKey="xp" stroke="#8884d8" dot={false} />
      </LineChart>
    </ResponsiveContainer>
  );
}
```
Install: `npm install recharts`
Pros: React-native (components, not canvas), declarative, good TypeScript support.
Cons: Slightly larger bundle than Chart.js.

**Option B — Chart.js + react-chartjs-2**
```tsx
import { Line } from 'react-chartjs-2';
import { Chart, LineElement, PointElement, LinearScale, CategoryScale, Tooltip } from 'chart.js';
Chart.register(LineElement, PointElement, LinearScale, CategoryScale, Tooltip);

export function XpChart({ data }: { data: { date: string; xp: number }[] }) {
  const chartData = {
    labels: data.map(d => d.date),
    datasets: [{ label: 'XP', data: data.map(d => d.xp), borderColor: '#8884d8', tension: 0.1 }],
  };
  return <Line data={chartData} />;
}
```
Install: `npm install chart.js react-chartjs-2`
Pros: More flexible, more chart types, slightly smaller if tree-shaken.
Cons: Imperative (canvas-based), requires manual `Chart.register(...)`, less idiomatic in React.

**Option C — Nivo**
```tsx
import { ResponsiveLine } from '@nivo/line';

export function XpChart({ data }: { data: { date: string; xp: number }[] }) {
  const nivoData = [{ id: 'xp', data: data.map(d => ({ x: d.date, y: d.xp })) }];
  return (
    <div style={{ height: 300 }}>
      <ResponsiveLine data={nivoData} xScale={{ type: 'point' }} yScale={{ type: 'linear' }} />
    </div>
  );
}
```
Install: `npm install @nivo/core @nivo/line`
Pros: Beautiful defaults, D3-powered, highly customisable.
Cons: Very large bundle (~180KB), overkill for a line chart.

**Recommendation:** Recharts — React-native, good balance of simplicity and flexibility, best TypeScript support.

---

### 2. Skill table structure: flat list vs grouped sections

**Option A — Flat table with sort**
```tsx
const SKILLS = [
  'Overall', 'Attack', 'Defence', 'Strength', 'Hitpoints', 'Ranged', 'Prayer',
  'Magic', 'Cooking', 'Woodcutting', 'Fletching', 'Fishing', 'Firemaking',
  'Crafting', 'Smithing', 'Mining', 'Herblore', 'Agility', 'Thieving',
  'Slayer', 'Farming', 'Runecraft', 'Hunter', 'Construction',
];

export function SkillTable({ skills }: { skills: SkillSnapshot[] }) {
  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="text-left border-b">
          <th className="py-2">Skill</th>
          <th className="py-2 text-right">Level</th>
          <th className="py-2 text-right">XP</th>
          <th className="py-2 text-right">Rank</th>
        </tr>
      </thead>
      <tbody>
        {skills.map(s => (
          <tr key={s.skillId} className="border-b hover:bg-gray-50">
            <td className="py-2">{s.skillName}</td>
            <td className="py-2 text-right">{s.level ?? '—'}</td>
            <td className="py-2 text-right">{s.xp?.toLocaleString() ?? '—'}</td>
            <td className="py-2 text-right">{s.rank?.toLocaleString() ?? '—'}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
```

**Option B — Grouped: Combat / Non-combat / Overall**
```tsx
const COMBAT_SKILLS = ['Attack', 'Defence', 'Strength', 'Hitpoints', 'Ranged', 'Prayer', 'Magic'];
const OTHER_SKILLS = ['Cooking', 'Woodcutting', 'Fletching', /* ... */ ];

export function SkillTable({ skills }: { skills: SkillSnapshot[] }) {
  const byName = Object.fromEntries(skills.map(s => [s.skillName, s]));
  return (
    <div className="space-y-6">
      <SkillGroup title="Overall" skills={['Overall'].map(n => byName[n])} />
      <SkillGroup title="Combat" skills={COMBAT_SKILLS.map(n => byName[n])} />
      <SkillGroup title="Other Skills" skills={OTHER_SKILLS.map(n => byName[n])} />
    </div>
  );
}
```

**Recommendation:** Flat table for Weekend 5. Sorting and grouping are polish — get the data on screen first.

---

### 3. Relative time: date-fns vs dayjs vs manual

**Option A — date-fns `formatDistanceToNow`**
```typescript
import { formatDistanceToNow } from 'date-fns';

function relativeTime(dateStr: string | null): string {
  if (!dateStr) return 'Never';
  return formatDistanceToNow(new Date(dateStr), { addSuffix: true });
  // → "about 3 hours ago"
}
```
Install: `npm install date-fns`
Pros: Tree-shakeable (only import what you use), immutable, ~30KB total but you'll use <1KB.

**Option B — dayjs**
```typescript
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
dayjs.extend(relativeTime);

function relativeTime(dateStr: string | null): string {
  if (!dateStr) return 'Never';
  return dayjs(dateStr).fromNow(); // → "3 hours ago"
}
```
Install: `npm install dayjs`
Pros: 2KB, familiar Moment.js API.
Cons: Plugins mutate `dayjs` globally via `extend()`.

**Option C — Manual (no library)**
```typescript
function relativeTime(dateStr: string | null): string {
  if (!dateStr) return 'Never';
  const diffMs = Date.now() - new Date(dateStr).getTime();
  const diffMins = Math.floor(diffMs / 60_000);
  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays}d ago`;
}
```
Pros: Zero dependencies, predictable output.
Cons: More code, misses edge cases (pluralisation, "1 hour" vs "2 hours").

**Recommendation:** `date-fns` — well-tested edge cases, good TypeScript types, tree-shakeable.

---

### 4. TanStack Query patterns: query keys and invalidation

**Option A — Flat string keys (simple, doesn't scale)**
```typescript
useQuery({ queryKey: ['accounts'], queryFn: fetchAccounts })
useQuery({ queryKey: ['skills-1'], queryFn: () => fetchSkills(1) })
```
Problem: `invalidateQueries({ queryKey: ['skills-1'] })` is fragile. As you add more queries, this pattern creates key collision risk.

**Option B — Nested array keys (recommended)**
```typescript
// src/api/queryKeys.ts
export const queryKeys = {
  accounts: () => ['accounts'] as const,
  skills: (accountId: number) => ['accounts', accountId, 'skills'] as const,
  skillHistory: (accountId: number, skillId: number, days: number) =>
    ['accounts', accountId, 'skills', skillId, 'history', days] as const,
};

// Usage:
useQuery({ queryKey: queryKeys.skills(accountId), queryFn: () => fetchSkills(accountId) })

// After adding an account — invalidate the whole accounts list:
queryClient.invalidateQueries({ queryKey: queryKeys.accounts() })

// After deleting account 1 — invalidate everything under account 1:
queryClient.invalidateQueries({ queryKey: ['accounts', 1] })
```
TanStack Query's prefix matching means `['accounts', 1]` invalidates `['accounts', 1, 'skills']` and `['accounts', 1, 'skills', 2, 'history', 30]` automatically.

**Recommendation:** Nested array keys with a `queryKeys` factory file. Keeps invalidation logic readable.

---

### 5. Add account form: controlled inputs vs React Hook Form

**Option A — Controlled inputs (built-in, no dependency)**
```tsx
function AddAccountForm() {
  const [username, setUsername] = useState('');
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (username: string) => api.post('/api/accounts', { osrsUsername: username }).then(r => r.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounts'] });
      setUsername('');
    },
  });

  return (
    <form onSubmit={e => { e.preventDefault(); mutation.mutate(username); }}>
      <input
        value={username}
        onChange={e => setUsername(e.target.value)}
        placeholder="OSRS username"
        maxLength={12}
        className="border rounded px-3 py-2"
      />
      <button type="submit" disabled={mutation.isPending} className="ml-2 bg-blue-600 text-white px-4 py-2 rounded">
        {mutation.isPending ? 'Adding...' : 'Track'}
      </button>
      {mutation.isError && <p className="text-red-500 mt-1">{(mutation.error as Error).message}</p>}
    </form>
  );
}
```

**Option B — React Hook Form**
```tsx
import { useForm } from 'react-hook-form';

function AddAccountForm() {
  const { register, handleSubmit, reset, formState: { errors } } = useForm<{ username: string }>();
  const mutation = useMutation({ /* ... */ onSuccess: () => reset() });

  return (
    <form onSubmit={handleSubmit(d => mutation.mutate(d.username))}>
      <input
        {...register('username', { required: 'Username required', maxLength: { value: 12, message: 'Max 12 chars' } })}
        placeholder="OSRS username"
        className="border rounded px-3 py-2"
      />
      {errors.username && <p className="text-red-500">{errors.username.message}</p>}
      <button type="submit" disabled={mutation.isPending}>Track</button>
    </form>
  );
}
```
Install: `npm install react-hook-form`
Pros: Uncontrolled inputs (better performance for large forms), built-in validation.
Cons: Extra dependency, more complex API — overkill for a single input.

**Recommendation:** Controlled inputs for Weekend 5. One input doesn't need a form library.

---

### 6. Loading states: skeleton vs spinner

**Option A — Tailwind skeleton**
```tsx
function SkillTableSkeleton() {
  return (
    <div className="animate-pulse space-y-2">
      {Array.from({ length: 24 }).map((_, i) => (
        <div key={i} className="flex justify-between">
          <div className="h-4 bg-gray-200 rounded w-24" />
          <div className="h-4 bg-gray-200 rounded w-12" />
          <div className="h-4 bg-gray-200 rounded w-20" />
          <div className="h-4 bg-gray-200 rounded w-16" />
        </div>
      ))}
    </div>
  );
}

// Usage:
if (isLoading) return <SkillTableSkeleton />;
```

**Option B — Spinner**
```tsx
function Spinner() {
  return (
    <div className="flex justify-center py-8">
      <div className="h-8 w-8 animate-spin rounded-full border-4 border-blue-600 border-t-transparent" />
    </div>
  );
}
```

**Recommendation:** Spinner for Weekend 5. Skeletons are better UX but require matching the exact layout — build the real layout first, then add skeletons as a polish step.

---

## Research Topics

**What is a TanStack Query `queryKey`?**
The cache key. TanStack Query stores server data in a cache, keyed by `queryKey`. `['accounts']` and `['accounts', 1, 'skills']` are separate cache entries. When you call `invalidateQueries({ queryKey: ['accounts'] })`, it marks all entries whose key *starts with* `['accounts']` as stale, triggering a background refetch. This prefix matching is why nested array keys are powerful.

**What is `useMutation` vs `useQuery`?**
`useQuery` is for reading data — it runs automatically on mount, retries on failure, caches results. `useMutation` is for writes — it runs only when you call `mutate(...)`, does not cache, and gives you `isPending`/`isError`/`isSuccess` state. Always use `useMutation` for POST/PUT/DELETE.

**What is `queryClient.invalidateQueries`?**
Marks matching cached queries as stale. On the next render where a component uses that query, TanStack Query refetches in the background. This is how you keep the UI in sync after a mutation — add an account → invalidate the accounts list → TanStack Query refetches → UI updates.

**What is `ResponsiveContainer` in Recharts?**
A wrapper that makes charts fill their parent element's width. Without it, you'd have to specify a fixed pixel width. Wrap your `LineChart` in it: `<ResponsiveContainer width="100%" height={300}><LineChart data={...}>`. The parent div must have a defined width (any block element works).

**What is `toLocaleString()` for numbers?**
`(4200000).toLocaleString()` → `"4,200,000"` (with comma separators). `toLocaleString('en-GB')` forces UK formatting if needed. Use it for XP and rank numbers — they're easier to read with separators.

**What is a Tailwind `animate-pulse`?**
A CSS animation that fades an element between full opacity and 75% opacity — creates a "breathing" skeleton placeholder effect. Works best on grey `bg-gray-200` rounded blocks. No JavaScript needed.

---

## Key Patterns

### Account list page with TanStack Query
```tsx
// src/pages/AccountListPage.tsx
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { formatDistanceToNow } from 'date-fns';

interface Account {
  id: number;
  osrsUsername: string;
  displayName: string;
  lastPolledAt: string | null;
}

export function AccountListPage() {
  const queryClient = useQueryClient();
  const { data: accounts, isLoading } = useQuery({
    queryKey: ['accounts'],
    queryFn: () => api.get<Account[]>('/api/accounts').then(r => r.data),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => api.delete(`/api/accounts/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounts'] }),
  });

  if (isLoading) return <div>Loading...</div>;

  return (
    <div className="max-w-2xl mx-auto p-6">
      <h1 className="text-2xl font-bold mb-4">Tracked Accounts</h1>
      {accounts?.length === 0 && <p className="text-gray-500">No accounts tracked yet.</p>}
      <ul className="space-y-2">
        {accounts?.map(account => (
          <li key={account.id} className="flex items-center justify-between p-4 border rounded">
            <div>
              <p className="font-medium">{account.displayName}</p>
              <p className="text-sm text-gray-500">
                Last polled: {account.lastPolledAt
                  ? formatDistanceToNow(new Date(account.lastPolledAt), { addSuffix: true })
                  : 'Never'}
              </p>
            </div>
            <button
              onClick={() => deleteMutation.mutate(account.id)}
              className="text-red-500 hover:text-red-700"
            >
              Remove
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

### Skill history chart with Recharts
```tsx
// src/pages/SkillHistoryPage.tsx
import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { api } from '../api/client';

interface Snapshot {
  capturedAt: string;
  xp: number;
  level: number;
  rank: number;
}

export function SkillHistoryPage() {
  const { accountId, skillId } = useParams<{ accountId: string; skillId: string }>();

  const { data, isLoading } = useQuery({
    queryKey: ['accounts', Number(accountId), 'skills', Number(skillId), 'history', 30],
    queryFn: () =>
      api.get<Snapshot[]>(`/api/accounts/${accountId}/skills/${skillId}/history?days=30`)
        .then(r => r.data),
  });

  if (isLoading) return <div>Loading chart...</div>;
  if (!data || data.length === 0) return <div>No snapshots yet for this skill.</div>;

  const chartData = data.map(s => ({
    date: new Date(s.capturedAt).toLocaleDateString(),
    xp: s.xp,
  }));

  return (
    <div className="p-6">
      <h2 className="text-xl font-bold mb-4">XP History (last 30 days)</h2>
      <ResponsiveContainer width="100%" height={300}>
        <LineChart data={chartData}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="date" />
          <YAxis tickFormatter={(v) => (v / 1_000_000).toFixed(1) + 'M'} />
          <Tooltip formatter={(v: number) => v.toLocaleString()} />
          <Line type="monotone" dataKey="xp" stroke="#6366f1" dot={false} strokeWidth={2} />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
```

---

## Verify

```bash
cd web && npm run dev
# Open http://localhost:5173

# Login → should redirect to /accounts
# Account list shows tracked accounts with relative time
# Click account → skill table shows 24 rows with level/XP/rank
# Click skill → line chart renders with data points

# Add an account via the form → list refreshes automatically
# Delete an account → list refreshes automatically

# Edge cases:
# - New account with no poll yet → "Never polled" shows, skill values are null (show "—")
# - Account with one snapshot → chart shows single point (no line)
```
