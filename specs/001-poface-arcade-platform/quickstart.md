# Quickstart: PoFace Local Development

**Branch**: `001-poface-arcade-platform`  
**Target**: New developer running the full stack locally in < 10 steps  
**Time estimate**: ~10 minutes (excluding Docker image pulls on first run)

---

## Prerequisites

Install the following before starting:

| Tool | Version | Why |
|---|---|---|
| [.NET SDK](https://dot.net/download) | 10.0+ | `PoFace.Api` + `PoFace.Client` |
| [Node.js](https://nodejs.org) | 22+ (LTS) | Playwright E2E tests |
| [Docker Desktop](https://www.docker.com/products/docker-desktop) | Latest | Azurite emulator (Testcontainers) |
| [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) | Latest | Key Vault secret access (dev only) |

Verify your installs:
```powershell
dotnet --version   # should output 10.x.x
node --version     # should output 22.x.x
docker info        # should show Server Version
az --version       # should output azure-cli x.x.x
```

---

## Step 1 — Clone and Restore

```powershell
git clone <repo-url> PoFace
cd PoFace
git checkout 001-poface-arcade-platform
dotnet restore
```

Central Package Management (CPM) resolves all NuGet versions from `Directory.Packages.props` automatically.

---

## Step 2 — Log In to Azure (for Key Vault access)

The app fetches secrets from Azure Key Vault `PoShared` on startup via `DefaultAzureCredential`. In local dev, this uses the Azure CLI token.

```powershell
az login
az account set --subscription "Punkouter26"
$env:POFACE_ENABLE_KEYVAULT = "true"
```

Verify you have secret access:
```powershell
az keyvault secret show --vault-name "PoShared" --name "FaceApiKey" --query "value" -o tsv
```

> **Note**: If you don't have Key Vault access, ask the project admin to grant you `Key Vault Secrets User` role on `PoShared` in the Punkouter26 subscription.

---

## Step 3 — Configure Local App Settings

The only file you need to edit is `src/PoFace.Api/appsettings.Development.json`. Set the non-secret configuration values (all secrets are pulled from Key Vault automatically):

```json
{
  "AzureFace": {
    "Endpoint": "https://<your-region>.api.cognitive.microsoft.com/"
  },
  "AzureStorage": {
    "AccountName": "poface"
  },
  "KeyVault": {
    "Uri": "https://PoShared.vault.azure.net/"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

> No secrets go in this file. Key Vault loads `FaceApiKey`, `StorageConnectionString`, and `ApplicationInsightsConnectionString` at startup when `POFACE_ENABLE_KEYVAULT=true`. Local development uses Azurite when `StorageConnectionString` is not otherwise provided.

---

## Step 4 — Start Azurite (Azure Storage Emulator)

Integration tests use Testcontainers which starts Azurite automatically. For local API development you need Azurite running manually:

```powershell
docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 `
  mcr.microsoft.com/azure-storage/azurite
```

Verify it's running:
```powershell
docker ps | Select-String "azurite"
```

To use Azurite instead of real Azure Storage locally, temporarily set this env var before running the API:
```powershell
$env:AZURE_STORAGE_CONNECTION_STRING = "UseDevelopmentStorage=true"
```

---

## Step 5 — Run the API

```powershell
cd src\PoFace.Api
dotnet run
```

The API starts on `https://localhost:7001`. Verify:
```powershell
Invoke-WebRequest https://localhost:7001/health -SkipCertificateCheck
```

Expected: `{"status":"Healthy"}`

---

## Step 6 — Run the Blazor WASM Client (Development)

The Blazor WASM client is served by the API in production (static file middleware). In development, run it separately with hot-reload:

```powershell
cd src\PoFace.Client
dotnet watch
```

The client starts on `https://localhost:7002`. The API URL is configured in `wwwroot/appsettings.Development.json`:
```json
{
  "ApiBaseUrl": "https://localhost:7001"
}
```

---

## Step 7 — Authenticate (Dev Mode)

The local dev bypass is available in `Development` only. Hit the dev-login endpoint to create test auth cookies:

```powershell
Invoke-WebRequest -Method POST https://localhost:7001/dev-login `
  -ContentType "application/json" `
  -Body '{"userId":"dev-user-001","displayName":"Dev User"}' `
  -SkipCertificateCheck
```

Or open the browser and navigate to `https://localhost:7002` — the app shows a "Dev Login" banner with a pre-filled form when running in Development mode.

---

## Step 8 — Run Tests

**Unit tests** (no external dependencies):
```powershell
dotnet test tests\PoFace.UnitTests
```

**Integration tests** (requires Docker for Azurite via Testcontainers):
```powershell
dotnet test tests\PoFace.IntegrationTests
```
> Docker must be running. Testcontainers pulls `azurite` on first run (~200 MB).

**E2E tests** (Playwright TypeScript — requires API + Client running):

```powershell
cd tests\PoFace.E2ETests
npm install
npx playwright install --with-deps
npx playwright test
```

Run a specific test file:
```powershell
npx playwright test tests/game-loop.spec.ts
```

View the HTML report:
```powershell
npx playwright show-report
```

---

## Step 9 — Verify Diagnostics

With the API running and authenticated, check the `/api/diag` endpoint:

```powershell
# Get a dev-login token first (see Step 7), then:
Invoke-WebRequest https://localhost:7001/api/diag -SkipCertificateCheck | Select-Object -ExpandProperty Content
```

Expected: JSON with `faceApi`, `blobStorage`, `tableStorage` all showing `"status": "OK"`.

---

## Common Issues

| Problem | Solution |
|---|---|
| `DefaultAzureCredential` fails | Run `az login` and `az account set --subscription Punkouter26` |
| Camera not working in browser | Check browser camera permissions; use Chrome/Edge for best compatibility |
| `docker: command not found` | Install Docker Desktop and ensure it's running |
| Azurite connection refused | Run Step 4 to start the Azurite container |
| `HTTPS certificate not trusted` | Run `dotnet dev-certs https --trust` once |
| Playwright `browserType.launch` fails | Run `npx playwright install --with-deps` in `tests\PoFace.E2ETests` |
| Port 7001 already in use | Change `applicationUrl` in `src\PoFace.Api\Properties\launchSettings.json` |

## Storage Lifecycle Policy

Apply lifecycle management with `infra/storage-lifecycle.json` after your storage account is provisioned:

```powershell
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/storage-lifecycle.json \
  --parameters storageAccountName=<storage-account-name>
```
