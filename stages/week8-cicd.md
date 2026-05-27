# Weekend 8 — CI/CD

## Goal

Every push to `main` automatically builds, tests, and deploys the whole app. Every pull request runs the test suite before merge is allowed. You can push a bug fix and have it live in production in under 5 minutes without touching Azure CLI. By Sunday you have two GitHub Actions workflows, a protected `main` branch, and a passing CI badge in the README.

## Starting Point

- App is deployed to Azure (Weekend 7 done)
- Images are in GHCR or ACR
- No GitHub Actions workflows exist yet
- `main` branch has no protection rules

---

## Tasks

- [ ] Create `.github/workflows/pr.yml` — runs on pull requests: build + test the backend, build the frontend
- [ ] Create `.github/workflows/deploy.yml` — runs on push to `main`: build image, push to registry, update Container Apps revision
- [ ] Set up Azure authentication for GitHub Actions (see Choices #1 — Service Principal vs OIDC)
- [ ] Add required GitHub repository secrets
- [ ] Enable branch protection on `main` — require PR + require `build-and-test` status check to pass
- [ ] Add README badge for CI status
- [ ] Verify: push to a branch, open PR → CI runs; merge to main → deploy runs; check Azure for new revision

---

## Choices

### 1. Azure authentication: Service Principal vs OIDC

**Option A — Service Principal with client secret (simpler to set up)**

Create a service principal with contributor access to your resource group:
```bash
az ad sp create-for-rbac \
  --name "osrs-tracker-github-actions" \
  --role contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/osrs-tracker-rg \
  --json-auth
```
This outputs a JSON object. Store the entire JSON as a GitHub secret named `AZURE_CREDENTIALS`.

In the workflow:
```yaml
- uses: azure/login@v2
  with:
    creds: ${{ secrets.AZURE_CREDENTIALS }}
```

Pros: Straightforward, well-documented, works immediately.
Cons: The secret is a long-lived credential that must be manually rotated. If it leaks, your Azure subscription is at risk.

**Option B — OIDC federated credentials (recommended)**
No secret stored in GitHub. Azure trusts GitHub's identity token, which is generated per-workflow-run and expires in minutes.

Step 1 — Create an app registration:
```bash
az ad app create --display-name "osrs-tracker-github-actions"
# Note the appId output

az ad sp create --id <appId>
# Note the id (object ID) output

az role assignment create \
  --role contributor \
  --assignee-object-id <sp-object-id> \
  --scope /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/osrs-tracker-rg
```

Step 2 — Add federated credential:
```bash
az ad app federated-credential create \
  --id <appId> \
  --parameters '{
    "name": "github-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:peterwb1/osrs-xp-tracker:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

Step 3 — Add GitHub secrets: `AZURE_CLIENT_ID` (appId), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

In the workflow:
```yaml
- uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```
Also add to the job:
```yaml
permissions:
  id-token: write
  contents: read
```

Pros: No secret to rotate or leak. Short-lived tokens per run. Industry best practice.
Cons: More setup steps, less Stack Overflow documentation.

**Recommendation:** OIDC if you're willing to do the setup. Service Principal if you want it working in 10 minutes.

---

### 2. Container registry in CI: GHCR vs ACR

**Option A — GHCR (GitHub Container Registry)**
```yaml
- name: Log in to GHCR
  uses: docker/login-action@v3
  with:
    registry: ghcr.io
    username: ${{ github.actor }}
    password: ${{ secrets.GITHUB_TOKEN }}  # GITHUB_TOKEN is auto-provided, no setup needed

- name: Build and push
  uses: docker/build-push-action@v5
  with:
    context: ./api
    file: ./api/OsrsTracker.Api/Dockerfile
    push: true
    tags: |
      ghcr.io/peterwb1/osrs-tracker-api:latest
      ghcr.io/peterwb1/osrs-tracker-api:sha-${{ github.sha }}
```
`GITHUB_TOKEN` is automatically injected by GitHub Actions — no secrets to configure.

**Option B — ACR**
```yaml
- name: Log in to ACR
  uses: azure/docker-login@v1
  with:
    login-server: osrstrackerpeter.azurecr.io
    username: ${{ secrets.ACR_USERNAME }}
    password: ${{ secrets.ACR_PASSWORD }}
```
Requires storing ACR credentials as additional secrets.

**Recommendation:** GHCR — `GITHUB_TOKEN` is free and auto-provided, zero extra secrets.

---

### 3. Image tagging: `latest` only vs `latest` + Git SHA

**Option A — `latest` only**
```yaml
tags: ghcr.io/peterwb1/osrs-tracker-api:latest
```
Simple, but you lose rollback ability — once you push a new image as `latest`, the old one is unreachable.

**Option B — `latest` + SHA tag (recommended)**
```yaml
tags: |
  ghcr.io/peterwb1/osrs-tracker-api:latest
  ghcr.io/peterwb1/osrs-tracker-api:sha-${{ github.sha }}
```
The SHA tag (`sha-a1b2c3d`) is immutable — it always refers to the same exact image. To roll back, update the Container App to use the previous SHA tag.

**Option C — Semantic versioning via tags**
```yaml
# Only runs when you push a git tag like v1.2.3
on:
  push:
    tags:
      - 'v*'
```
Pros: Clear release history.
Cons: Requires manual tagging, slower deployment cadence.

**Recommendation:** Option B — `latest` + SHA. Gives you rollback without requiring a formal release process.

---

### 4. Test coverage reporting: skip vs Coverlet vs Codecov

**Option A — No coverage reporting (simplest)**
Just run `dotnet test` and report pass/fail.

**Option B — Coverlet with summary output**
```yaml
- name: Run tests
  run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

- name: Coverage summary
  run: |
    dotnet tool install -g dotnet-reportgenerator-globaltool
    reportgenerator -reports:"./coverage/**/coverage.cobertura.xml" -targetdir:"./coverage/report" -reporttypes:TextSummary
    cat ./coverage/report/Summary.txt
```
Shows coverage % in the workflow logs.

**Option C — Coverlet + Codecov (recommended for visibility)**
```yaml
- name: Run tests
  run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

- name: Upload coverage to Codecov
  uses: codecov/codecov-action@v4
  with:
    token: ${{ secrets.CODECOV_TOKEN }}
    files: ./coverage/**/coverage.cobertura.xml
    fail_ci_if_error: false
```
Codecov (free for open source) gives you a PR comment showing coverage diff, a badge, and file-level coverage reports. Sign up at codecov.io and add `CODECOV_TOKEN` as a GitHub secret.

**Recommendation:** Coverlet + Codecov (Option C) if your repo is public. Adds visibility and encourages writing tests. Skip if you want to keep things simple.

---

### 5. Deploy trigger: on merge vs manual dispatch

**Option A — Automatic on push to main (recommended)**
```yaml
on:
  push:
    branches:
      - main
```
Every merge to `main` triggers deployment.

**Option B — Manual dispatch only**
```yaml
on:
  workflow_dispatch:
    inputs:
      image_tag:
        description: 'Image tag to deploy (e.g. sha-abc123)'
        required: true
        default: 'latest'
```
Run via GitHub Actions UI: "Run workflow" → enter tag → deploy.
Pros: Control over what gets deployed, can deploy specific versions.
Cons: Manual step, easy to forget.

**Option C — Automatic + manual override**
```yaml
on:
  push:
    branches:
      - main
  workflow_dispatch:
```
Supports both. Most flexible.

**Recommendation:** Option C — auto-deploy on merge is the standard CI/CD pattern, and the manual trigger is useful when you want to redeploy a previous SHA without pushing new code.

---

## Complete Workflow Files

### `.github/workflows/pr.yml`
```yaml
name: PR Checks

on:
  pull_request:
    branches:
      - main

jobs:
  build-and-test:
    name: Build and Test
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore api/OsrsTracker.Api/OsrsTracker.Api.csproj

      - name: Build
        run: dotnet build api/OsrsTracker.Api/OsrsTracker.Api.csproj --no-restore -c Release

      - name: Test
        run: dotnet test api/OsrsTracker.Tests/OsrsTracker.Tests.csproj --no-build --verbosity normal --collect:"XPlat Code Coverage" --results-directory ./coverage

      - name: Upload coverage
        uses: codecov/codecov-action@v4
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
          files: ./coverage/**/coverage.cobertura.xml
          fail_ci_if_error: false

  build-frontend:
    name: Build Frontend
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: web/package-lock.json

      - name: Install
        working-directory: web
        run: npm ci

      - name: Build
        working-directory: web
        run: npm run build
        env:
          VITE_API_URL: https://placeholder.azurecontainerapps.io
```

### `.github/workflows/deploy.yml`
```yaml
name: Deploy to Azure

on:
  push:
    branches:
      - main
  workflow_dispatch:

permissions:
  id-token: write
  contents: read
  packages: write

jobs:
  deploy-api:
    name: Build and Deploy API
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Log in to GHCR
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push API image
        uses: docker/build-push-action@v5
        with:
          context: ./api
          file: ./api/OsrsTracker.Api/Dockerfile
          push: true
          tags: |
            ghcr.io/peterwb1/osrs-tracker-api:latest
            ghcr.io/peterwb1/osrs-tracker-api:sha-${{ github.sha }}

      - name: Log in to Azure
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Update Container App
        run: |
          az containerapp update \
            --name osrs-tracker-api \
            --resource-group osrs-tracker-rg \
            --image ghcr.io/peterwb1/osrs-tracker-api:sha-${{ github.sha }}

  deploy-frontend:
    name: Deploy Frontend
    runs-on: ubuntu-latest
    # Static Web Apps deployment is handled by SWA's auto-generated workflow
    # This job is a placeholder — SWA creates its own .github/workflows/azure-static-web-apps-*.yml
    # If you want to control it here, delete the SWA-generated workflow and use:
    steps:
      - run: echo "Frontend deployment handled by Azure Static Web Apps workflow"
```

---

## Research Topics

**What is a GitHub Actions workflow?**
A YAML file in `.github/workflows/`. GitHub runs it automatically when the `on:` trigger fires (e.g. push to `main`, PR opened). Each workflow has `jobs:` — jobs run in parallel by default, each on a fresh VM (`ubuntu-latest`). Jobs have `steps:` — each step is either a shell command (`run:`) or a pre-built action (`uses:`). The `github` context provides metadata: `${{ github.sha }}` is the commit SHA, `${{ github.actor }}` is the username.

**What is OIDC in GitHub Actions?**
OpenID Connect. GitHub generates a short-lived JWT token for each workflow run, signed by `https://token.actions.githubusercontent.com`. You configure Azure to trust tokens from this issuer for a specific GitHub repo/branch. When the workflow calls `azure/login`, it presents this token instead of a long-lived secret. Azure verifies the token against the trust policy and issues a short-lived access token. No credential ever touches GitHub's secret storage.

**What is branch protection?**
A GitHub repository setting (Settings → Branches → Add rule) that enforces:
- *Require pull requests*: no direct push to `main`, all changes must come through PRs
- *Require status checks*: the PR can't be merged until named CI jobs pass
- *Require up-to-date branches*: the PR must be current with `main` before merging

To enable: Settings → Branches → Add branch protection rule → branch name `main` → check "Require a pull request before merging" and "Require status checks to pass" → add `build-and-test` as a required check.

**What does `az containerapp update --image` do?**
Creates a new revision of the Container App with the updated image. Azure pulls the new image from GHCR, starts the new container, waits for it to be healthy, then routes traffic to it. The old revision stays running until the new one is healthy. This is a rolling update with zero downtime.

**What is `actions/cache` and why does it matter?**
`actions/setup-node` with `cache: 'npm'` automatically caches `node_modules` between workflow runs. Without it, every run does a fresh `npm install` — ~30 seconds. With cache, unchanged dependencies are restored from cache in ~5 seconds. Same principle applies to NuGet with `dotnet restore` and a `~/.nuget/packages` cache.

**What is `dotnet test --collect:"XPlat Code Coverage"`?**
The `XPlat Code Coverage` collector is built into .NET's test host via the `coverlet.collector` package (already in your test project). It generates a `coverage.cobertura.xml` file after the test run. Codecov and other tools read this Cobertura format. No extra package needed — just add the `--collect` flag.

---

## Required GitHub Secrets

| Secret | Value | How to get |
|--------|-------|------------|
| `AZURE_CLIENT_ID` | App registration's Application (client) ID | `az ad app list --display-name osrs-tracker-github-actions` |
| `AZURE_TENANT_ID` | Azure AD tenant ID | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID | `az account show --query id -o tsv` |
| `CODECOV_TOKEN` | Codecov upload token | codecov.io → your repo → Settings → General |
| `GITHUB_TOKEN` | Auto-provided | No setup needed — GitHub injects it automatically |

---

## README Badge

```markdown
![CI](https://github.com/peterwb1/osrs-xp-tracker/actions/workflows/pr.yml/badge.svg)
![Deploy](https://github.com/peterwb1/osrs-xp-tracker/actions/workflows/deploy.yml/badge.svg)
[![codecov](https://codecov.io/gh/peterwb1/osrs-xp-tracker/branch/main/graph/badge.svg)](https://codecov.io/gh/peterwb1/osrs-xp-tracker)
```

---

## Verify

```bash
# 1. Push a branch and open a PR
git checkout -b test/ci-check
echo "# test" >> README.md
git commit -am "test: trigger CI"
git push origin test/ci-check
# → open PR on GitHub
# → Actions tab shows "PR Checks" workflow running
# → Both jobs pass → PR shows green checks

# 2. Merge the PR
# → "Deploy to Azure" workflow triggers
# → Pushes new image to GHCR
# → Updates Container App revision
# → Check Actions tab: all green

# 3. Verify new revision in Azure
az containerapp revision list \
  --name osrs-tracker-api \
  --resource-group osrs-tracker-rg \
  --query "[].{name:name,active:properties.active,created:properties.createdTime}" \
  --output table
# → Two revisions, new one active

# 4. Verify branch protection
# Try pushing directly to main:
git checkout main
echo "# test" >> README.md
git commit -am "test direct push"
git push origin main
# → Rejected: "protected branch hook declined"
```
