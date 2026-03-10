# Implementation Plan: PoFace — Arcade Emotion-Matching Platform

**Branch**: `001-poface-arcade-platform` | **Date**: 2026-03-09 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/001-poface-arcade-platform/spec.md`

## Summary

PoFace is a full-stack, real-time arcade web application where authenticated users (Microsoft Account) play a 5-round facial-expression matching game. The frontend is a Blazor WASM SPA with a hard "Matrix" terminal aesthetic; the backend is a .NET 10 Minimal API. Both are hosted in a single Azure App Service. The core game loop captures a 640×480 JPEG frame at each round's shutter moment, ships it to the API, scores it against a target emotion via Azure Face API (`Detection_03`), enforces head-pose validation (yaw/pitch ≤ 20°), and persists results into Azure Blob Storage + Azure Table Storage. A global Best-Match leaderboard tracks one entry per user. All infrastructure secrets are fetched from Azure Key Vault via Managed Identity. Three-tier automated tests (xUnit unit, xUnit+Testcontainers integration, Playwright E2E) gate every merge.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (backend API + test projects); Blazor WASM .NET 10 (frontend); TypeScript (Playwright E2E)  
**Primary Dependencies**:
- Backend: `Azure.AI.Vision.Face` v1.0.0-beta.1, `Azure.Data.Tables`, `Azure.Storage.Blobs`, `Azure.Security.KeyVault.Secrets`, `Azure.Identity`, `MediatR`, `Serilog`, `Serilog.Sinks.ApplicationInsights`, `OpenTelemetry.Exporter.AzureMonitor`, `Microsoft.Identity.Web`
- Frontend: Radzen.Blazor, `Microsoft.Authentication.WebAssembly.Msal`
- Testing: `xunit`, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`), `Testcontainers.Azurite`, `Microsoft.Playwright` (TypeScript)
- Build: `Directory.Build.props` (global usings, nullable, warnings-as-errors), `Directory.Packages.props` (Central Package Management)

**Storage**: Azure Table Storage (leaderboard — PartitionKey: `Year`, RowKey: `UserId`); Azure Blob Storage (round snapshots, public blob access, lifecycle archive tier after 6 months inactivity); Azure Key Vault `PoShared` (secrets)  
**Testing**: xUnit unit tests; xUnit + `WebApplicationFactory` + Testcontainers (Azurite) integration tests; Playwright TypeScript E2E  
**Target Platform**: Azure App Service (Linux); Blazor WASM in browser (desktop + mobile); single App Service hosts both static WASM files and API  
**Project Type**: Full-stack web application (SPA + Minimal API)  
**Performance Goals**: Analysis engine round-trip ≤ 3 s on standard connection; frozen frame visible ≤ 3 s on simulated 3G; diagnostics health state reflected within 5 s of change  
**Constraints**: No secrets in `appsettings.json`; no audio files loaded (programmatic only); 640×480 JPEG at 0.85 quality (drops to 0.60 on slow connection); authentication required to play; Leaderboard + Recap pages publicly accessible; WASM + API co-hosted in same App Service  
**Scale/Scope**: Global leaderboard; indefinite image retention for best-match sessions; temporary 24-hour images for non-best authenticated sessions; 5 pages; 6 vertical feature slices; ~2000 LOC estimate

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see bottom of this section.*

### Pre-Design Check

| Principle | Status | Notes |
|---|---|---|
| I. Zero-Waste Codebase | ✅ PASS | Each VSA slice owns only its own code; cleanup tasks included in task plan; no legacy code in greenfield project |
| II. SOLID & GoF Patterns | ✅ PASS | MediatR enforces Mediator pattern for handler dispatch; Repository pattern for Table/Blob access; Strategy pattern for image quality selection (bitrate adaptation); Factory for audio oscillator construction; all non-obvious decisions will carry explanatory comments |
| III. Test Coverage | ✅ PASS | Three tiers mandated: xUnit unit, xUnit + Testcontainers integration, Playwright E2E — one per major feature slice per constitution |
| IV. Vertical Slice Architecture | ✅ PASS | Six slices identified: `Auth`, `GameSession`, `Scoring`, `Leaderboard`, `Recap`, `Diagnostics`; each slice owns its DTOs, handler, validator, endpoint registration; no horizontal root layer folders |
| V. Observability & Debug-First | ✅ PASS | Serilog structured logging + App Insights; correlation ID per request; `ANALYZING…` state visible in UI; `ProblemDetails` surfaces errors to UI in dev/staging; `/diag` endpoint with masked config |
| VI. Clarification-First | ✅ PASS | 5 clarifications completed before plan; no unresolved ambiguities in spec |

### Post-Design Re-Check *(filled after Phase 1)*

| Principle | Status | Notes |
|---|---|---|
| I. Zero-Waste Codebase | ✅ PASS | Data model and contracts introduce no orphaned entities; all 6 entities are referenced by at least one endpoint |
| II. SOLID & GoF Patterns | ✅ PASS | Contracts define clean interfaces between WASM client and API; MediatR handler per slice confirmed in contracts |
| III. Test Coverage | ✅ PASS | All 5 API contracts have corresponding unit + integration test anchors in tasks |
| IV. Vertical Slice Architecture | ✅ PASS | Every contract endpoint maps 1:1 to a named VSA slice folder |
| V. Observability & Debug-First | ✅ PASS | `/diag` contract defined; correlation ID propagation documented in research |
| VI. Clarification-First | ✅ PASS | No new ambiguities introduced by design phase |

## Project Structure

### Documentation (this feature)

```text
specs/001-poface-arcade-platform/
├── plan.md              # This file
├── research.md          # Phase 0 — resolved unknowns, decisions, rationale
├── data-model.md        # Phase 1 — entities, fields, relationships, state transitions
├── quickstart.md        # Phase 1 — local dev setup in < 10 steps
├── contracts/
│   ├── api-endpoints.md      # All Minimal API routes, request/response shapes
│   └── blazor-interop.md     # JS Interop contracts (webcam, Web Audio API)
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
PoFace.sln

Directory.Build.props          # Global: LangVersion, Nullable, TreatWarningsAsErrors, ImplicitUsings
Directory.Packages.props       # Central Package Management — all NuGet versions pinned here

src/
├── PoFace.Api/                # .NET 10 Minimal API — backend
│   ├── Features/
│   │   ├── Auth/              # VS1 — MSAL token validation, /dev-login (dev/test only)
│   │   │   ├── AuthEndpoints.cs
│   │   │   └── DevLoginEndpoint.cs
│   │   ├── GameSession/       # VS2 — session lifecycle (start, complete, discard)
│   │   │   ├── StartSessionCommand.cs
│   │   │   ├── CompleteSessionCommand.cs
│   │   │   └── GameSessionEndpoints.cs
│   │   ├── Scoring/           # VS3 — image capture ingest, Face API call, head-pose validation
│   │   │   ├── ScoreRoundCommand.cs
│   │   │   ├── ScoreRoundHandler.cs
│   │   │   ├── HeadPoseValidator.cs
│   │   │   ├── FaceAnalysisService.cs   # wraps Azure.AI.Vision.Face SDK
│   │   │   └── ScoringEndpoints.cs
│   │   ├── Leaderboard/       # VS4 — best-match upsert, global query
│   │   │   ├── UpsertLeaderboardEntryCommand.cs
│   │   │   ├── GetLeaderboardQuery.cs
│   │   │   ├── LeaderboardTableRepository.cs
│   │   │   └── LeaderboardEndpoints.cs
│   │   ├── Recap/             # VS5 — session recap retrieval (public, no auth)
│   │   │   ├── GetRecapQuery.cs
│   │   │   ├── RecapEndpoints.cs
│   │   │   └── BlobImageRepository.cs
│   │   └── Diagnostics/       # VS6 — /diag health + masked config
│   │       ├── DiagnosticsQuery.cs
│   │       ├── ConfigMaskingService.cs
│   │       └── DiagnosticsEndpoints.cs
│   ├── Infrastructure/
│   │   ├── Auth/
│   │   │   └── PoTestAuthExtensions.cs  # AddPoTestAuth for dev/test header bypass
│   │   ├── KeyVault/
│   │   │   └── KeyVaultSecretLoader.cs
│   │   ├── Storage/
│   │   │   ├── BlobStorageService.cs
│   │   │   └── TableStorageService.cs
│   │   ├── Telemetry/
│   │   │   ├── CorrelationIdMiddleware.cs
│   │   │   └── OtelMetrics.cs
│   │   └── Logging/
│   │       └── SerilogConfiguration.cs
│   ├── appsettings.json        # No secrets — only non-sensitive config (endpoint URLs, region)
│   ├── appsettings.Development.json
│   └── Program.cs
│
└── PoFace.Client/             # Blazor WASM .NET 10 — frontend SPA
    ├── Features/
    │   ├── Home/
    │   │   └── HomePage.razor
    │   ├── Arena/             # Core game loop page + all sub-components
    │   │   ├── ArenaPage.razor
    │   │   ├── FaceGuide.razor
    │   │   ├── CountdownOverlay.razor
    │   │   ├── FrozenFrameOverlay.razor
    │   │   └── arena.module.css
    │   ├── Leaderboard/
    │   │   ├── LeaderboardPage.razor
    │   │   └── LeaderboardGrid.razor    # Radzen DataGrid
    │   ├── Recap/
    │   │   ├── RecapPage.razor
    │   │   └── RoundPanel.razor
    │   └── Diagnostics/
    │       └── DiagnosticsPage.razor
    ├── Shared/
    │   ├── MainLayout.razor
    │   ├── NavMenu.razor
    │   └── ThemeService.cs             # Radzen Matrix theme wrapper
    ├── Services/
    │   ├── GameOrchestrator.cs          # Client-side game state machine
    │   ├── AudioService.cs             # Web Audio API programmatic 8-bit sound
    │   └── ApiClient.cs                # Typed HttpClient for all API calls
    ├── Interop/
    │   ├── webcam.js                   # getUserMedia + canvas capture (640×480, JPEG 0.85/0.60)
    │   └── audio.js                   # Web Audio API oscillator factory
    ├── wwwroot/
    │   ├── index.html
    │   └── css/
    │       ├── terminal.css            # Global Matrix aesthetic (scanlines, glow, flicker)
    │       └── app.css
    ├── App.razor
    └── Program.cs

tests/
├── PoFace.UnitTests/                   # xUnit — isolated logic
│   ├── Scoring/
│   │   ├── HeadPoseValidatorTests.cs
│   │   └── ScoreRoundHandlerTests.cs
│   ├── Leaderboard/
│   │   └── UpsertLeaderboardEntryTests.cs
│   └── Diagnostics/
│       └── ConfigMaskingServiceTests.cs
│
├── PoFace.IntegrationTests/            # xUnit + WebApplicationFactory + Testcontainers (Azurite)
│   ├── Scoring/
│   │   └── ScoringEndpointTests.cs
│   ├── Leaderboard/
│   │   └── LeaderboardEndpointTests.cs
│   ├── Recap/
│   │   └── RecapEndpointTests.cs
│   ├── Diagnostics/
│   │   └── DiagnosticsEndpointTests.cs
│   └── Infrastructure/
│       ├── AzuriteFixture.cs
│       └── PoFaceWebAppFactory.cs      # WebApplicationFactory + AddPoTestAuth
│
└── PoFace.E2ETests/                   # Playwright TypeScript
    ├── tests/
    │   ├── game-loop.spec.ts           # US1: full 5-round session
    │   ├── leaderboard.spec.ts         # US2: best-match enforcement
    │   ├── recap.spec.ts               # US3: public gallery access
    │   ├── aesthetics.spec.ts          # US4: terminal CSS verification
    │   └── diagnostics.spec.ts        # US6: health page (auth required)
    ├── fixtures/
    │   └── authFixture.ts             # Injects PoTestAuth header for E2E sessions
    ├── playwright.config.ts
    └── package.json
```

**Structure Decision**: Web application layout with three projects under `src/` (API, Client) and three test projects under `tests/`. Both API and Client are co-hosted in the same Azure App Service — the API serves the WASM static files from `PoFace.Client/wwwroot` and exposes all API routes under `/api/`. This avoids CORS complexity and matches the single-App-Service deployment constraint. Feature code is strictly separated by vertical slice inside each project; shared/cross-cutting infrastructure lives in `Infrastructure/` (API) and `Services/` + `Shared/` (Client) — never under feature folders.

## Complexity Tracking

> No constitution violations. All choices are justified by current requirements.

| Decision | Why Chosen | Simpler Alternative Rejected Because |
|---|---|---|
| Azure Table Storage (not SQL) | Leaderboard is a simple key-value upsert; no relational joins needed; Table Storage is faster and cheaper | SQL Server / PostgreSQL adds migration overhead and cost for a single-table access pattern |
| Azure Blob Storage (not Files) | Unstructured binary snapshots with public URL access and lifecycle tier management is exactly what Blob Storage is designed for | File Share requires SMB/NFS mount; CosmosDB attachment is deprecated |
| `WebApplicationFactory` + Testcontainers (Azurite) | Tests real HTTP pipeline + real Storage emulation without cloud network dependency | Mocking `IBlobServiceClient` would give false confidence; real Azurite catches serialization and connection bugs |
| Single App Service (WASM + API together) | No CORS config; simpler deployment; single scale unit; matches spec constraint | Separate App Services would require CORS headers on every API call and split deployment pipelines |
| `AddPoTestAuth` bypass (dev/test only) | Playwright E2E can't run real MSAL flows in headless CI; header-based bypass is the standard ASP.NET Core testing pattern | Real MSAL in CI requires managed service principals and browser automation that is fragile and slow |

## Implementation Phases

### Phase 0: Research (see `research.md`)

Resolved unknowns:
1. Azure Face SDK `Detection_03` — emotion + head-pose model capabilities and return types
2. Testcontainers Azurite — Docker image, Table + Blob emulation API compatibility
3. `Microsoft.Identity.Web` with Blazor WASM — MSAL token acquisition flow and `AddPoTestAuth` pattern
4. Web Audio API — oscillator types, frequency values for 8-bit blip / chord / frequency-sweep
5. Bitrate adaptation — `navigator.connection` API + canvas `toDataURL` quality parameter
6. OpenTelemetry with Azure Monitor — custom metric registration pattern in .NET 10
7. Blazor CSS isolation + Radzen ThemeService — how to apply global terminal CSS alongside Radzen theme

### Phase 1: Design (see `data-model.md`, `contracts/`, `quickstart.md`)

- Data model: 6 entities mapped to Azure Table / Blob storage with partition strategies
- API contracts: 10 endpoints across 6 feature slices
- JS Interop contracts: webcam capture + Web Audio API interfaces
- Quickstart: local dev in < 10 steps using Azurite Docker + `dotnet run`

### Phase 2: Tasks (`tasks.md` — generated by `/speckit.tasks`)

Tasks will be ordered by user story priority (P1 → P6) to enable incremental vertical slice delivery.
