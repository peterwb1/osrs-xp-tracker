# Azure Deployment Guide

One-time guide to get the app live on Azure. Follow the steps in order — each one produces an output (a URL, a domain name) that the next step needs.

---

## Before You Start — Prerequisites

### 1. Install Azure CLI
Download and install from https://aka.ms/installazurecliwindows then verify:
```bash
az --version
```

### 2. Log in to Azure
```bash
az login
```
A browser window opens. Sign in with your Azure account.

### 3. Create a GitHub Personal Access Token (PAT)

You need this to push images to GitHub Container Registry (GHCR) and for Container Apps to pull them.

1. Go to https://github.com/settings/tokens/new (classic token)
2. Name: `osrs-tracker-ghcr`
3. Expiry: 90 days (or longer)
4. Scopes: tick **`write:packages`** and **`read:packages`**
5. Click **Generate token**
6. **Copy it now** — you can't see it again

Save it somewhere safe. You'll use it in several steps below as `YOUR_GITHUB_PAT`.

### 4. Pick a strong database password

Pick one now and save it. You'll use it in Step 3 and Step 5.

Rules: at least 8 characters, must include uppercase, lowercase, and a number. Azure rejects weak passwords.

Example format: `Osrs2026!Tracker` — but use your own.

---

## Step 1 — Commit the Code Changes

The health endpoint and CORS changes need to be in the image. Commit and push first:

```bash
git add api/OsrsTracker.Api/Program.cs api/OsrsTracker.Api/Dockerfile
git commit -m "Add health endpoint and configurable CORS for production"
git push
```

---

## Step 2 — Create the Azure Resource Group

A resource group is a container for all your Azure resources. One group keeps everything together and makes cleanup easy.

```bash
az group create --name osrs-tracker-rg --location uksouth
```

Expected output: JSON with `"provisioningState": "Succeeded"`

---

## Step 3 — Create the Container Apps Environment

This is the network/runtime that your containers live inside.

```bash
az containerapp env create \
  --name osrs-tracker-env \
  --resource-group osrs-tracker-rg \
  --location uksouth
```

This takes ~2 minutes. When it finishes, **note the `staticIp` or domain** in the output. The environment domain looks like:

```
{random-id}.uksouth.azurecontainerapps.io
```

Your API URL will be: `https://osrs-tracker-api.{random-id}.uksouth.azurecontainerapps.io`

Write it down — you'll need it in Step 8.

---

## Step 4 — Create the PostgreSQL Database

```bash
az postgres flexible-server create \
  --resource-group osrs-tracker-rg \
  --name osrs-tracker-db \
  --admin-user osrs \
  --admin-password "YOUR_DB_PASSWORD" \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --version 16 \
  --location uksouth \
  --public-access 0.0.0.0
```

Replace `YOUR_DB_PASSWORD` with the password you chose in the prerequisites.

This takes ~5 minutes. Then create the database inside the server:

```bash
az postgres flexible-server db create \
  --resource-group osrs-tracker-rg \
  --server-name osrs-tracker-db \
  --database-name osrstracker
```

Your connection string (save this — you need it in Step 6):
```
Host=osrs-tracker-db.postgres.database.azure.com;Port=5432;Database=osrstracker;Username=osrs;Password=YOUR_DB_PASSWORD;Ssl Mode=Require
```

> **Why `Ssl Mode=Require`?** Azure PostgreSQL enforces TLS on all connections. Without it the API will fail to connect.

---

## Step 5 — Log in to GitHub Container Registry

```bash
echo YOUR_GITHUB_PAT | docker login ghcr.io -u peterwb1 --password-stdin
```

Expected output: `Login Succeeded`

---

## Step 6 — Build and Push the API Image

Run from the repo root:

```bash
docker build \
  -t ghcr.io/peterwb1/osrs-tracker-api:latest \
  -f api/OsrsTracker.Api/Dockerfile \
  ./api

docker push ghcr.io/peterwb1/osrs-tracker-api:latest
```

The push uploads to `ghcr.io/peterwb1/osrs-tracker-api`. Go to your GitHub profile → Packages to confirm it appeared.

Make the package public (so Container Apps can pull it without credentials):
1. Go to https://github.com/peterwb1?tab=packages
2. Click `osrs-tracker-api`
3. Package settings → Change visibility → **Public**

Alternatively, keep it private and Container Apps will use your PAT (configured in Step 7).

---

## Step 7 — Deploy the API Container App

Generate a strong random JWT secret — it must be at least 32 characters. You can use:
```bash
# Run this and copy the output
node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"
```

Now deploy:

```bash
az containerapp create \
  --name osrs-tracker-api \
  --resource-group osrs-tracker-rg \
  --environment osrs-tracker-env \
  --image ghcr.io/peterwb1/osrs-tracker-api:latest \
  --registry-server ghcr.io \
  --registry-username peterwb1 \
  --registry-password YOUR_GITHUB_PAT \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 3 \
  --secrets \
    "db-conn=Host=osrs-tracker-db.postgres.database.azure.com;Port=5432;Database=osrstracker;Username=osrs;Password=YOUR_DB_PASSWORD;Ssl Mode=Require" \
    "jwt-key=YOUR_JWT_SECRET" \
  --env-vars \
    "ConnectionStrings__Default=secretref:db-conn" \
    "Jwt__Key=secretref:jwt-key" \
    "Jwt__Issuer=OsrsTracker" \
    "ASPNETCORE_ENVIRONMENT=Production"
```

Replace:
- `YOUR_GITHUB_PAT` — the token from prerequisites
- `YOUR_DB_PASSWORD` — the database password from Step 4
- `YOUR_JWT_SECRET` — the 32+ char string you just generated

Get the API URL:
```bash
az containerapp show \
  --name osrs-tracker-api \
  --resource-group osrs-tracker-rg \
  --query properties.configuration.ingress.fqdn -o tsv
```

Output will be something like:
```
osrs-tracker-api.{random-id}.uksouth.azurecontainerapps.io
```

**Write this down** — you need it in the next step.

---

## Step 8 — Build and Push the Frontend Image

The API URL is baked into the frontend at build time. Use the FQDN from Step 7:

```bash
docker build \
  -t ghcr.io/peterwb1/osrs-tracker-frontend:latest \
  --build-arg NEXT_PUBLIC_API_URL=https://osrs-tracker-api.{random-id}.uksouth.azurecontainerapps.io \
  ./web

docker push ghcr.io/peterwb1/osrs-tracker-frontend:latest
```

Make it public on GitHub Packages the same way as the API (or keep private and use your PAT).

---

## Step 9 — Deploy the Frontend Container App

```bash
az containerapp create \
  --name osrs-tracker-frontend \
  --resource-group osrs-tracker-rg \
  --environment osrs-tracker-env \
  --image ghcr.io/peterwb1/osrs-tracker-frontend:latest \
  --registry-server ghcr.io \
  --registry-username peterwb1 \
  --registry-password YOUR_GITHUB_PAT \
  --target-port 3000 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 2
```

Get the frontend URL:
```bash
az containerapp show \
  --name osrs-tracker-frontend \
  --resource-group osrs-tracker-rg \
  --query properties.configuration.ingress.fqdn -o tsv
```

Output:
```
osrs-tracker-frontend.{random-id}.uksouth.azurecontainerapps.io
```

---

## Step 10 — Wire Up CORS

The API needs to know the frontend's URL to allow browser requests. Update the API with the frontend FQDN from Step 9:

```bash
az containerapp update \
  --name osrs-tracker-api \
  --resource-group osrs-tracker-rg \
  --set-env-vars "Frontend__Url=https://osrs-tracker-frontend.{random-id}.uksouth.azurecontainerapps.io"
```

---

## Verification

### Check the API is healthy
```bash
curl https://osrs-tracker-api.{random-id}.uksouth.azurecontainerapps.io/health
```
Expected: `Healthy`

### Check the API requires auth
```bash
curl https://osrs-tracker-api.{random-id}.uksouth.azurecontainerapps.io/api/accounts
```
Expected: `401 Unauthorized`

### Open the app
Open `https://osrs-tracker-frontend.{random-id}.uksouth.azurecontainerapps.io` in your browser.

Golden path:
1. Register a new account
2. Log in → redirected to `/accounts`
3. Add an OSRS username
4. Account appears in the list with "Last polled: Never"
5. Click the account → skills table loads
6. Click a skill → chart page loads (empty until the poller runs)

### Check the API logs if something breaks
```bash
az containerapp logs show \
  --name osrs-tracker-api \
  --resource-group osrs-tracker-rg \
  --follow
```

---

## Useful Commands

```bash
# List all your Container Apps
az containerapp list --resource-group osrs-tracker-rg --output table

# Restart the API (e.g. after updating env vars)
az containerapp revision restart \
  --name osrs-tracker-api \
  --resource-group osrs-tracker-rg \
  --revision $(az containerapp revision list \
    --name osrs-tracker-api \
    -g osrs-tracker-rg \
    --query "[0].name" -o tsv)

# Update to a new image version
az containerapp update \
  --name osrs-tracker-api \
  --resource-group osrs-tracker-rg \
  --image ghcr.io/peterwb1/osrs-tracker-api:latest

# Delete everything (stops all billing)
az group delete --name osrs-tracker-rg --yes
```

---

## Cost Estimate

| Resource | Tier | Est. cost |
|---|---|---|
| Container Apps (API + frontend) | Consumption plan | ~£0 at low traffic (generous free grant) |
| PostgreSQL Flexible Server | Standard_B1ms | Free for 12 months on new accounts, then ~£10/month |
| Container Apps environment | — | Free |
| GHCR image storage | Public repo | Free |
| **Total** | | **£0 first year, ~£10/month after** |

To stop all billing immediately: `az group delete --name osrs-tracker-rg --yes`
