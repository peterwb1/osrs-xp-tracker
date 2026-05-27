# Weekend 4 — Read Endpoints + Frontend Bootstrap

## Goal

Two things happen this weekend: the backend gets the remaining read/delete endpoints, and the frontend goes from nothing to a working auth flow. By Sunday you can open a browser, register, log in, and see an empty account list — not yet functional, but the skeleton is wired to real API calls. Every future weekend builds UI on top of this foundation, so getting the wiring right matters more than making it look good.

## Starting Point

- Backend: only `POST /api/accounts` exists; no list, delete, or history endpoints
- `TrackedAccount.UserId` is set (Weekend 2 done)
- No `web/` directory at all

---

## Backend Tasks

- [ ] `GET /api/accounts` — return current user's tracked accounts (id, osrsUsername, displayName, lastPolledAt)
- [ ] `DELETE /api/accounts/{id}` — ownership check, then delete account + cascade XpSnapshots
- [ ] `GET /api/accounts/{id}/skills` — for each of the 24 skills, return the most recent snapshot (level, xp, rank). If no snapshot exists for a skill, still return the skill with null values.
- [ ] `GET /api/accounts/{id}/skills/{skillId}/history?days=30` — return snapshots ordered by `CapturedAt ASC` for chart data
- [ ] All four endpoints need `[Authorize]` + ownership checks
- [ ] Add response DTOs (separate classes from the EF models — don't return raw EF entities to the API)

---

## Frontend Tasks

### Approach A — Vite + React (recommended)
```bash
# From project root
npm create vite@latest web -- --template react-ts
cd web
npm install @tanstack/react-query react-router-dom axios
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

### Approach B — Next.js (alternative, more opinionated)
```bash
npx create-next-app@latest web --typescript --tailwind --no-app --no-src-dir
cd web && npm install @tanstack/react-query axios
```
Next.js adds file-based routing and server components — more powerful but more to learn. Stick with Vite for this project unless you already know Next.js.

### Common frontend tasks (either approach)
- [ ] Configure Tailwind: add `@tailwind base/components/utilities` to `index.css`; set `content` paths in `tailwind.config.js`
- [ ] Create `src/api/client.ts` — configured HTTP client with base URL and auth header
- [ ] Create auth state management (see Choices below)
- [ ] Create route structure with protected routes
- [ ] Skeleton pages: `LoginPage`, `RegisterPage`, `AccountListPage`, `AccountDetailPage`
- [ ] Wrap app in `QueryClientProvider` (TanStack Query)

---

## Choices

### 1. HTTP client: axios vs fetch

**Option A — axios**
```typescript
// src/api/client.ts
import axios from 'axios';

export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL });

api.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  res => res,
  err => {
    if (err.response?.status === 401) {
      localStorage.removeItem('token');
      window.location.href = '/login';
    }
    return Promise.reject(err);
  }
);
```
Pros: interceptors for auth + 401 handling, automatic JSON parsing, cleaner error objects.
Cons: extra ~14KB dependency.

**Option B — native fetch with a wrapper**
```typescript
// src/api/client.ts
const baseURL = import.meta.env.VITE_API_URL;

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = localStorage.getItem('token');
  const res = await fetch(`${baseURL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  });
  if (res.status === 401) { localStorage.removeItem('token'); window.location.href = '/login'; }
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}
```
Pros: zero dependencies, explicit.
Cons: more boilerplate, no interceptor system.

**Recommendation:** axios — the interceptor pattern makes auth handling cleaner, and it integrates naturally with TanStack Query's `queryFn`.

---

### 2. JWT storage: localStorage vs httpOnly cookie

**Option A — localStorage**
```typescript
// After login:
localStorage.setItem('token', response.data.token);

// Read it:
const token = localStorage.getItem('token');
```
Pros: dead simple, works with axios interceptors.
Cons: XSS-vulnerable — malicious scripts can read `localStorage`. For a personal project this is acceptable; for a multi-user public app it's a real risk.

**Option B — httpOnly cookie (server sets it)**
The server sets `Set-Cookie: token=...; HttpOnly; SameSite=Strict` in the login response. The browser sends it automatically on every request. JavaScript can never read it — XSS-safe.

Cons: requires CORS to be configured with `AllowCredentials()`, and your axios calls need `withCredentials: true`. Also doesn't work well across different origins (API on port 8080, frontend on 5173). More complex for this stage.

**Option C — in-memory (most secure)**
Store the token in a React state variable (or Zustand store). Disappears on page refresh — requires re-login. Secure against XSS but annoying UX.

**Recommendation:** localStorage for this project. Document the XSS tradeoff with a comment. If this ever becomes a real public service, migrate to httpOnly cookies.

---

### 3. Auth state management: Context vs Zustand vs Redux

**Option A — React Context**
```typescript
// src/contexts/AuthContext.tsx
const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState(() => localStorage.getItem('token'));

  const login = (newToken: string) => {
    localStorage.setItem('token', newToken);
    setToken(newToken);
  };

  const logout = () => {
    localStorage.removeItem('token');
    setToken(null);
  };

  return (
    <AuthContext.Provider value={{ token, login, logout, isAuthenticated: !!token }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext)!;
```
Pros: built-in, zero dependencies, sufficient for auth state (which changes rarely).
Cons: re-renders all consumers when state changes — fine for auth, not for frequently-changing state.

**Option B — Zustand**
```typescript
// src/stores/authStore.ts
import { create } from 'zustand';

interface AuthStore {
  token: string | null;
  login: (token: string) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthStore>(set => ({
  token: localStorage.getItem('token'),
  login: token => { localStorage.setItem('token', token); set({ token }); },
  logout: () => { localStorage.removeItem('token'); set({ token: null }); },
}));
```
Pros: simpler API than Context, no Provider wrapping needed, fine-grained subscriptions.
Cons: extra dependency (~3KB).

**Option C — Redux Toolkit**
Overkill for this project. Redux shines with complex interdependent state. Skip it.

**Recommendation:** React Context for auth. Add Zustand in Weekend 5 if you find yourself fighting Context re-renders.

---

### 4. TanStack Query version: v4 vs v5

| | v4 | v5 |
|--|----|----|
| `useQuery` options | `useQuery(['key'], fn, { options })` | `useQuery({ queryKey: ['key'], queryFn: fn })` |
| `useMutation` | `useMutation(fn, { onSuccess })` | `useMutation({ mutationFn: fn, onSuccess })` |
| Status fields | `isLoading`, `isError`, `isSuccess` | Same, plus `isPending` replaces `isLoading` in some cases |
| Package | `@tanstack/react-query@4` | `@tanstack/react-query@5` |

**Recommendation:** v5. It's the current version. Most Stack Overflow answers are still v4-shaped — if you see the old signature, just convert to the object form.

---

### 5. Protected route pattern

**Option A — Wrapper component**
```typescript
// src/components/PrivateRoute.tsx
export function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
}

// In App.tsx:
<Route path="/accounts" element={<PrivateRoute><AccountListPage /></PrivateRoute>} />
```

**Option B — Layout route**
```typescript
function AuthLayout() {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <Outlet />; // renders child routes
}

// In App.tsx:
<Route element={<AuthLayout />}>
  <Route path="/accounts" element={<AccountListPage />} />
  <Route path="/accounts/:id" element={<AccountDetailPage />} />
</Route>
```
Pros: cleaner nesting, one guard protects multiple routes.

**Recommendation:** Layout route (Option B) — it scales better as you add more protected routes.

---

## Research Topics

**What is Vite?**
A build tool and dev server. `npm run dev` starts a dev server that serves your React app using native ES modules — no bundling during development, which makes hot reload near-instant. `npm run build` produces an optimised static bundle for production. Compare to Create React App (webpack-based, much slower).

**What is TanStack Query?**
A "server state" library. It separates data that comes from the server (loading/error/stale/refetch) from UI state (what tab is selected, is modal open). Instead of:
```typescript
const [data, setData] = useState(null);
const [loading, setLoading] = useState(true);
useEffect(() => { fetch('/api/accounts').then(r => r.json()).then(setData).finally(() => setLoading(false)); }, []);
```
You write:
```typescript
const { data, isLoading } = useQuery({ queryKey: ['accounts'], queryFn: () => api.get('/api/accounts').then(r => r.data) });
```
It handles loading state, error state, caching (won't re-fetch if data is fresh), background refetch on window focus, and deduplication of concurrent requests.

**What is React Router v6?**
Declarative client-side routing. `<Routes>` matches the current URL to `<Route>` components. The `useNavigate()` hook navigates programmatically. `useParams()` reads URL params like `:id`. No page loads — just component swaps.

**What is a response DTO?**
Data Transfer Object — a separate class that defines exactly what shape the API returns. Never return your EF entity classes directly from controllers. Reasons: (1) you might expose fields that shouldn't be public; (2) EF entities can have circular navigation property references that cause JSON serialisation to loop; (3) your API contract becomes coupled to your database schema. Create `AccountDto`, `SkillSnapshotDto` etc. and map to them.

**What is `VITE_API_URL`?**
Vite exposes environment variables prefixed with `VITE_` to your frontend code via `import.meta.env.VITE_API_URL`. Create a `.env.local` file (gitignored) with `VITE_API_URL=http://localhost:5000`. In production (Docker), it's set at build time as a build argument.

---

## Verify

```bash
# Backend
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/accounts
# → []

# After adding account:
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/accounts/1/skills
# → [{ "skillName": "Overall", "level": 2277, "xp": 199999999, "rank": 1 }, ...]

# Frontend
cd web && npm run dev
# Open http://localhost:5173
# → Register page renders, form submits, JWT stored, redirected to /accounts
# → Account list page shows empty state
```
