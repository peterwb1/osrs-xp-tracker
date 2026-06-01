# Local Development Guide

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — runs the database, API, and frontend
- [Node.js 20](https://nodejs.org/) — only needed if running the frontend outside Docker
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) — only needed if running the API outside Docker

---

## Option A — Full stack in Docker (simplest)

Starts everything: PostgreSQL, the .NET API, and the Next.js frontend.

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API | http://localhost:8080 |

The first `--build` takes a few minutes. Subsequent runs are fast (layers are cached).

To stop:
```bash
docker compose down
```

To wipe the database and start completely fresh:
```bash
docker compose down -v
```

---

## Option B — Database + API in Docker, frontend with `npm run dev`

Use this when actively working on the frontend — you get hot reload instead of rebuilding the Docker image on every change.

**Terminal 1** — start the database and API:
```bash
docker compose up db api
```

**Terminal 2** — start the frontend dev server:
```bash
cd web
npm install
npm run dev
```

Frontend is at http://localhost:3000 with hot reload. API is at http://localhost:8080.

> The `NEXT_PUBLIC_API_URL` in `web/.env.local` points to `http://localhost:8080` by default — this is already set up correctly.

---

## Option C — Everything outside Docker (database still in Docker)

Use this when actively working on both the API and frontend simultaneously.

**Terminal 1** — start only the databases:
```bash
docker compose up db db-test
```

**Terminal 2** — start the API:

First, create your local JWT config file (one-time setup):
```bash
cp api/OsrsTracker.Api/appsettings.Development.Local.json.example \
   api/OsrsTracker.Api/appsettings.Development.Local.json
```
Edit `appsettings.Development.Local.json` and set a JWT key (any string 32+ characters).

Then run the API:
```bash
cd api
dotnet run --project OsrsTracker.Api/OsrsTracker.Api.csproj
```

**Terminal 3** — start the frontend:
```bash
cd web
npm install
npm run dev
```

---

## Running tests

Integration tests need the test database running:
```bash
docker compose up db-test
```

Then in a separate terminal:
```bash
cd api
dotnet test
```

Unit tests only (no database needed):
```bash
dotnet test --filter "Category!=Integration"
```

---

## Useful commands

```bash
# Rebuild only the API container (after backend changes)
docker compose up --build api

# Rebuild only the frontend container
docker compose up --build frontend

# View API logs
docker compose logs api --follow

# View all logs
docker compose logs --follow

# Apply new database migrations (runs automatically on API startup)
# If you need to run them manually:
cd api
dotnet ef database update --project OsrsTracker.Api/OsrsTracker.Api.csproj

# Add a new migration after changing an EF Core model
dotnet ef migrations add YourMigrationName --project OsrsTracker.Api/OsrsTracker.Api.csproj
```

---

## Environment summary

| Variable | Where set | Purpose |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | `web/.env.local` | API base URL for the frontend |
| `Jwt__Key` | `appsettings.Development.Local.json` | JWT signing key (dev only) |
| `Jwt__Issuer` | `appsettings.Development.Local.json` | JWT issuer |
| `ConnectionStrings__Default` | `appsettings.json` | PostgreSQL connection string |
