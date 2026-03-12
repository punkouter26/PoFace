# DevOps — PoFace Arcade

## Day 1: Local Development

### Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| .NET SDK | 10.0+ | Build & run API + Blazor WASM |
| Docker Desktop | Any recent | Run Azurite storage emulator |
| Node.js + npm | 18+ | E2E Playwright tests |
| Azure CLI | Latest | `az login` for managed identity fallback |

### Clone & First Run

```bash
git clone <repo-url>
cd PoFace

# 1 — Start Azurite (Azure Storage emulator)
docker compose up -d

# 2 — Run the API (serves Blazor WASM on http://localhost:5000)
dotnet run --project src/PoFace.Api/PoFace.Api.csproj
```

The API reads `ASPNETCORE_ENVIRONMENT=Development` and automatically connects to Azurite via `StorageConnectionString = UseDevelopmentStorage=true`. No Azure credentials needed for basic local play.

### Optional: Enable Key Vault locally

```bash
az login
export POFACE_ENABLE_KEYVAULT=true
export KeyVault__Uri=https://kv-poshared.vault.azure.net/
dotnet run --project src/PoFace.Api/PoFace.Api.csproj
```

---

## Environment Configuration

### App Settings Hierarchy

```
appsettings.json
  ↓ overridden by
appsettings.{Environment}.json
  ↓ overridden by
Environment Variables
  ↓ overridden by
Azure Key Vault (when POFACE_ENABLE_KEYVAULT=true or running in Azure)
```

### Required Secrets

| Key | Location | Description |
|---|---|---|
| `StorageConnectionString` | Key Vault | Azure Storage connection string. Dev: `UseDevelopmentStorage=true` |
| `GoogleVision:CredentialJson` | Key Vault | Google Cloud service account JSON (full JSON string) |
| `AzureAd:TenantId` | Key Vault / appsettings | Entra ID tenant (`1639b208-d5bf-4d71-9096-06163884a5e4`) |
| `AzureAd:ClientId` | Key Vault / appsettings | App registration client ID (`68629c3f-65e6-4bb8-b2ea-777f292f7776`) |

### App Settings Injected by Bicep (Azure Deploy)

| Key | Value Source |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (hardcoded) |
| `AzureStorage__AccountName` | Storage account name output from Bicep |
| `KeyVault__Uri` | Key Vault URI output from Bicep |
| `ApplicationInsights__ConnectionString` | Shared App Insights connection string |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Same — both keys set for SDK compat |

---

## Docker Compose (Local Storage Only)

`docker-compose.yml` runs **Azurite** only — the .NET app runs directly on the host.

```yaml
# Ports:
#   10000 — Blob Storage
#   10001 — Queue Storage
#   10002 — Table Storage
docker compose up -d     # start Azurite
docker compose down      # stop and remove containers
docker compose down -v   # also remove azurite-data volume
```

---

## CI/CD — Azure Developer CLI (azd)

`azure.yaml` defines a single `web` service deployed as App Service via `azd`.

### Provision & Deploy

```bash
# First time — provision all Azure resources and deploy
azd up

# Subsequent deploys
azd deploy

# Tear down all provisioned resources
azd down
```

### azd Environment Variables

```bash
azd env set AZURE_LOCATION eastus
azd env set AZURE_ENV_NAME dev
azd env set sharedKeyVaultName kv-poshared
azd env set sharedKeyVaultResourceGroupName PoShared
azd env set sharedAppServicePlanName asp-poshared-linux
azd env set sharedAppInsightsName appi-poshared
```

---

## Infrastructure (Bicep)

`infra/main.bicep` provisions per-environment resources. Shared resources (Key Vault, App Service Plan, App Insights) are in the `PoShared` resource group and referenced by name.

### Resources Created Per Environment

| Resource | SKU / Config |
|---|---|
| `Microsoft.Storage/storageAccounts` | `Standard_LRS`, `StorageV2`, TLS 1.2 min, blob lifecycle policy |
| `Microsoft.Web/sites` (App Service) | Linux, .NET 10, `alwaysOn`, HTTP/2, SystemAssigned managed identity |

### RBAC — Managed Identity Grants

`keyvault-access.bicep` assigns:
- **Key Vault Secrets User** → App Service managed identity on the shared Key Vault

> Storage access in Azure uses the `StorageConnectionString` secret from Key Vault (not RBAC). Blob public access is enabled for image URLs shared in recaps.

### Blast Radius Assessment

| Refactor | Downstream Impact |
|---|---|
| Change Storage account name | Update `AzureStorage__AccountName` app setting; blob URLs in existing recaps will break |
| Change Key Vault name | Update `KeyVault__Uri` app setting; all secret reads fail until updated |
| Change App Service Plan SKU | Affects all services sharing `asp-poshared-linux`; coordinate with PoShared owners |
| Change `AzureAd:ClientId` | Client MSAL config (`appsettings.json` in `wwwroot`) must be redeployed in sync |
| Modify `storage-lifecycle.json` | Changes blob retention; may delete recap images earlier than expected |
| Add new Table Storage tables | No migration needed (schemaless); new entities are backward compatible |
| Rename API routes | Break Blazor client `ApiClient.cs` calls; no versioning in place — coordinate deploy |

---

## Running Tests

### Unit Tests

```bash
dotnet test tests/PoFace.UnitTests/PoFace.UnitTests.csproj
```

### Integration Tests (requires Azurite)

```bash
docker compose up -d
dotnet test tests/PoFace.IntegrationTests/PoFace.IntegrationTests.csproj
```

Integration tests use `TestContainers` / Azurite and `WebApplicationFactory`. `PoTestAuth` header scheme (`X-Test-User-Id`, `X-Test-Display-Name`) is used in place of real Entra ID tokens.

### E2E Tests (Playwright)

```bash
cd tests/PoFace.E2ETests
npm install
npx playwright install
npx playwright test
```

Requires the API to be running locally (`dotnet run` or debug launch).

---

## Logging

- **Console**: structured JSON via Serilog (all environments)
- **File**: rolling logs at `src/PoFace.Api/logs/poface-*.log` (local dev)
- **App Insights**: OTel traces and metrics via `Azure.Monitor.OpenTelemetry.AspNetCore` (production)

Log levels are configured in `appsettings.json` → `Serilog:MinimumLevel`.
