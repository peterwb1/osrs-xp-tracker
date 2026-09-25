# Local Development Guide

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) — to run the API
- [SQL Server LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb) — ships with Visual Studio, or install standalone. This is the default local database, no container needed.
- [Node.js 20](https://nodejs.org/) — to run the frontend
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — only needed for the full-stack-in-Docker option, integration tests (`db-test`), or on macOS/Linux where LocalDB isn't available

---

## Option A — Native dev with LocalDB (simplest, Windows)

Runs the API directly against `(localdb)\MSSQLLocalDB` — no Docker required for the database or API.

First, create your local JWT config file (one-time setup):
```bash
cp api/OsrsTracker.Api/appsettings.Development.Local.json.example \
   api/OsrsTracker.Api/appsettings.Development.Local.json
```
Edit `appsettings.Development.Local.json` and set a JWT key (any string 32+ characters).

Then run the API — it applies migrations and seeds skills against LocalDB automatically on startup:
```bash
cd api
dotnet run --project OsrsTracker.Api/OsrsTracker.Api.csproj
```

**Terminal 2** — start the frontend:
```bash
cd web
npm install
npm run dev
```

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API | http://localhost:8080 |

---

## Option B — Full stack in Docker (cross-platform)

Starts everything: SQL Server, the .NET API, and the Next.js frontend. Use this on macOS/Linux, or if you don't want to install LocalDB.

```bash
docker compose --profile full up --build
```

The first `--build` takes a few minutes. Subsequent runs are fast (layers are cached).

To stop:
```bash
docker compose --profile full down
```

To wipe the database and start completely fresh:
```bash
docker compose --profile full down -v
```

---

## Option C — Database + API in Docker, frontend with `npm run dev`

Use this when actively working on the frontend but don't have LocalDB installed — you get hot reload instead of rebuilding the Docker image on every change.

**Terminal 1** — start the database and API:
```bash
docker compose --profile full up db api
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

## Running tests

Integration tests need the test database running (this always uses a Docker SQL Server container, regardless of which option you use above for the API):
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
docker compose --profile full up --build api

# Rebuild only the frontend container
docker compose --profile full up --build frontend

# View API logs
docker compose logs api --follow

# View all logs
docker compose logs --follow

# Apply new database migrations (runs automatically on API startup)
# If you need to run them manually (requires LocalDB running):
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
| `ConnectionStrings__Default` | `appsettings.json` | SQL Server (LocalDB) connection string |
