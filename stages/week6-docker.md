# Weekend 6 — Dockerise Everything

## Goal

One command — `docker compose up --build` — starts the entire app: Postgres, the .NET API, and the React frontend served by nginx. No `dotnet run`, no `npm run dev`, no separate terminals. By Sunday you have a `docker-compose.yml` that anyone can clone and run without installing .NET or Node. This weekend is about making the development-to-production gap smaller.

## Starting Point

- API Dockerfile exists (from Weekend 1) but has suboptimal layer ordering
- No `web/Dockerfile` exists
- `docker-compose.yml` has only `db` and `api` services
- Frontend runs only via `npm run dev`

---

## Tasks

- [ ] Update `api/OsrsTracker.Api/Dockerfile` — fix layer caching (copy `.csproj` files before source)
- [ ] Create `web/Dockerfile` — multi-stage build: Node build → nginx serve
- [ ] Create `web/nginx.conf` — SPA routing (`try_files $uri /index.html`)
- [ ] Create `web/.env.production` — `VITE_API_URL=http://localhost:8080` (for local Docker Compose)
- [ ] Update `docker-compose.yml`:
  - Add `frontend` service
  - Add Postgres healthcheck
  - Add `depends_on: condition: service_healthy` to `api`
  - Port map frontend: `3000:80`
- [ ] Verify: `docker compose up --build`, open `http://localhost:3000`, login works
- [ ] Update README "Getting Started" section

---

## Choices

### 1. Frontend web server: nginx vs `serve` (npm) vs Caddy

**Option A — nginx (recommended)**
`nginx:alpine` is ~8MB, production-grade, handles SPA routing with `try_files`, and supports compression, caching headers, and reverse proxying. The config is minimal:

```nginx
# web/nginx.conf
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets aggressively
    location ~* \.(js|css|png|jpg|ico|svg)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

**Option B — `serve` npm package**
```dockerfile
FROM node:20-alpine
RUN npm install -g serve
COPY --from=build /app/dist /app
EXPOSE 3000
CMD ["serve", "-s", "/app", "-p", "3000"]
```
Pros: Simpler config — SPA mode (`-s`) is one flag.
Cons: Node.js runtime in the image adds ~80MB, slower startup, no production-grade features.

**Option C — Caddy**
```dockerfile
FROM caddy:alpine
COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist /srv
```
```
# Caddyfile
:80 {
    root * /srv
    try_files {path} /index.html
    file_server
}
```
Pros: Automatic HTTPS when deployed (great for Weekend 7+), minimal config.
Cons: Less documentation, less familiar than nginx, slightly larger image than nginx.

**Recommendation:** nginx — it's the industry standard for serving React apps, has the most documentation, and the config is straightforward.

---

### 2. API Dockerfile: layer caching

**Current (Weekend 1) — suboptimal:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .                    # ← copies ALL source first
RUN dotnet restore OsrsTracker.Api/OsrsTracker.Api.csproj
RUN dotnet publish ...
```
Problem: every code change invalidates the `COPY . .` layer, forcing a fresh `dotnet restore` — slow.

**Option A — Layer-cached (recommended):**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first — restore only re-runs when these change
COPY OsrsTracker.Domain/OsrsTracker.Domain.csproj OsrsTracker.Domain/
COPY OsrsTracker.Api/OsrsTracker.Api.csproj OsrsTracker.Api/
COPY OsrsTracker.Tests/OsrsTracker.Tests.csproj OsrsTracker.Tests/
RUN dotnet restore OsrsTracker.Api/OsrsTracker.Api.csproj

# Now copy source — this layer invalidates on every code change,
# but restore (above) is already cached
COPY . .
RUN dotnet publish OsrsTracker.Api/OsrsTracker.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OsrsTracker.Api.dll"]
```

**Option B — BuildKit cache mount (advanced):**
```dockerfile
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore OsrsTracker.Api/OsrsTracker.Api.csproj
```
Pros: Even faster — NuGet packages are cached on the Docker host, not just in image layers.
Cons: Requires `DOCKER_BUILDKIT=1`, doesn't work in all CI environments without configuration.

**Recommendation:** Option A. Layer ordering is universally supported and gives 80% of the benefit.

---

### 3. Frontend Dockerfile: build-time API URL

The React app needs to know the API URL at build time (Vite bakes it in as `import.meta.env.VITE_API_URL`). There are three patterns:

**Option A — Build arg (baked in at build time):**
```dockerfile
ARG VITE_API_URL=http://localhost:8080
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build
```
In docker-compose:
```yaml
frontend:
  build:
    context: ./web
    args:
      VITE_API_URL: http://localhost:8080
```
Pros: Simple. Cons: Different API URL = different image (can't reuse the same image across environments).

**Option B — Runtime injection via `window.__env__`:**
Build with a placeholder, then inject at container start via an entrypoint script:
```bash
# entrypoint.sh
envsubst '${VITE_API_URL}' < /usr/share/nginx/html/env.js.template > /usr/share/nginx/html/env.js
nginx -g 'daemon off;'
```
In `index.html`: `<script src="/env.js"></script>` then read `window.__env__.API_URL`.
Pros: Same image, different config per environment.
Cons: Requires changing how the app reads configuration, more complex.

**Option C — Nginx reverse proxy to API (sidesteps the problem):**
Configure nginx to proxy `/api/` requests to the API service:
```nginx
location /api/ {
    proxy_pass http://api:8080/api/;
}
```
Frontend calls `/api/accounts` (relative URL, no base URL needed). `VITE_API_URL` becomes `/`.
Pros: No CORS issues, simpler frontend config, same image everywhere.
Cons: Nginx now has two responsibilities (serve frontend + proxy API).

**Recommendation:** Option A for Weekend 6. Option C is elegant but adds complexity. Option B is for production where you want environment-agnostic images.

---

### 4. Postgres healthcheck: `pg_isready` vs TCP check

**Option A — `pg_isready` (recommended):**
```yaml
db:
  image: postgres:16-alpine
  healthcheck:
    test: ["CMD-SHELL", "pg_isready -U osrs -d osrstracker"]
    interval: 5s
    timeout: 5s
    retries: 5
```
`pg_isready` is Postgres's own tool. It checks that the server is ready to accept connections, not just that the port is open.

**Option B — TCP port check:**
```yaml
healthcheck:
  test: ["CMD", "nc", "-z", "localhost", "5432"]
```
Pros: Works for any TCP service.
Cons: Port open ≠ Postgres ready. The API might connect before the DB has finished initialising, causing startup errors.

**Recommendation:** `pg_isready`. It's the right tool for the job and alpine images include it.

---

### 5. docker-compose structure: single file vs multiple override files

**Option A — Single `docker-compose.yml` (recommended for Weekend 6):**
```yaml
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: osrs
      POSTGRES_PASSWORD: osrs
      POSTGRES_DB: osrstracker
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U osrs -d osrstracker"]
      interval: 5s
      retries: 5

  api:
    build:
      context: ./api
      dockerfile: OsrsTracker.Api/Dockerfile
    ports:
      - "8080:8080"
    environment:
      ConnectionStrings__Default: "Host=db;Port=5432;Database=osrstracker;Username=osrs;Password=osrs"
      Jwt__Key: "dev-secret-key-change-in-production"
      Jwt__Issuer: "OsrsTracker"
      ASPNETCORE_URLS: "http://+:8080"
    depends_on:
      db:
        condition: service_healthy

  frontend:
    build:
      context: ./web
      args:
        VITE_API_URL: http://localhost:8080
    ports:
      - "3000:80"
    depends_on:
      - api

volumes:
  postgres_data:
```

**Option B — Base + override files:**
```
docker-compose.yml        # base (no ports, no env values)
docker-compose.dev.yml    # dev overrides (ports, dev secrets)
docker-compose.prod.yml   # prod overrides (real secrets via env vars)
```
Run with: `docker compose -f docker-compose.yml -f docker-compose.dev.yml up`
Pros: Same base file for all environments, no duplication.
Cons: More complex to manage, overkill until you have a real prod environment.

**Recommendation:** Single file for Weekend 6. Add override files in Weekend 7 when you have a real prod environment with different values.

---

## Research Topics

**What is Docker layer caching?**
Docker builds images as a stack of layers, one per instruction. If a layer hasn't changed since the last build, Docker reuses the cached version and skips rebuilding it. The key insight: `COPY package.json .` followed by `RUN npm install` means npm install only re-runs when `package.json` changes. If you put `COPY . .` first, any source file change invalidates everything below it. Layer order matters enormously for build speed.

**What is a multi-stage Dockerfile?**
Multiple `FROM` statements in one file. Each `FROM` starts a new stage. Early stages (the "build stage") can use large SDKs (`node:20`, `mcr.microsoft.com/dotnet/sdk:8.0`). The final stage (`FROM nginx:alpine`) is what gets deployed — you use `COPY --from=build /app/dist /usr/share/nginx/html` to bring over only the output. The final image doesn't contain the SDK, Node, or intermediate build artifacts. Result: a ~20MB nginx image instead of a ~900MB Node image.

**Why does `try_files $uri /index.html` matter for React Router?**
React Router handles routing entirely in the browser. When you visit `http://localhost:3000/accounts/1` directly (or refresh the page), nginx looks for a file at `/accounts/1` on disk — it doesn't exist, so it returns 404. The `try_files` directive says: "try to serve the file at this path; if it doesn't exist, serve `index.html` instead." React then boots, React Router reads the URL, and renders the correct page. Without this, every direct URL access or refresh breaks.

**What is `depends_on: condition: service_healthy`?**
Plain `depends_on: api` just waits for the container to *start* (the process exists). `service_healthy` waits for the healthcheck to *pass* (the service is actually ready). Without this, the API starts, tries to connect to Postgres, Postgres is still initialising, and the API crashes on startup. With the healthcheck condition, Docker holds the API start until Postgres reports ready.

**What are Docker named volumes vs bind mounts?**
A named volume (`postgres_data:/var/lib/postgresql/data`) is managed by Docker — data persists between `docker compose down` and `docker compose up`. A bind mount (`./data:/var/lib/postgresql/data`) maps a host directory into the container — you can inspect the files directly on your machine. For a database, named volumes are recommended: Docker manages placement and permissions, and they work identically on all OSes. `docker compose down -v` removes named volumes (wipes the database).

**What is `ASPNETCORE_URLS`?**
Tells Kestrel (the .NET web server) which address to listen on. In Docker, you want `http://+:8080` — `+` means all network interfaces, so Docker can route traffic to the container. Without this, Kestrel might default to `localhost:5000` which Docker can't proxy to.

---

## Key Files

### `web/Dockerfile`
```dockerfile
# Stage 1: Build the React app
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
ARG VITE_API_URL=http://localhost:8080
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

# Stage 2: Serve with nginx
FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

### `web/nginx.conf`
```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location ~* \.(js|css|png|jpg|ico|svg|woff2)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    gzip on;
    gzip_types text/plain text/css application/javascript application/json;
}
```

---

## Verify

```bash
# Build and start everything
docker compose up --build

# Expected output:
# db healthy
# api started (migrations run, skills seeded)
# frontend started (nginx serving on :3000)

# Open http://localhost:3000 → React app loads
# Register/login → auth works (API calls go to localhost:8080)
# Add an OSRS account → appears in list

# Check images are small:
docker images | grep osrs
# frontend image should be ~20MB (nginx), not 900MB (node)
# api image should be ~250MB (aspnet runtime), not 800MB (sdk)

# Verify data persists:
docker compose down
docker compose up
# → same accounts still visible (postgres_data volume preserved)

# Wipe and start fresh:
docker compose down -v
docker compose up
# → no accounts, migrations ran on fresh DB
```
