# Tasks: PoFace — Arcade Emotion-Matching Platform

**Branch**: `001-poface-arcade-platform`  
**Date**: 2026-03-09  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Total Tasks**: 86 | **Phases**: 9

---

## Phase 1 — Setup (Project Scaffolding)

**Goal**: Create the solution and project structure so every subsequent task has files to write into.  
**Independent test**: `dotnet build` succeeds with zero errors and zero warnings.

- [X] T001 Create solution file `PoFace.sln` at repo root with `dotnet new sln -n PoFace`
- [X] T002 Create `Directory.Build.props` at repo root — set `<LangVersion>13.0</LangVersion>`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<ImplicitUsings>enable</ImplicitUsings>`
- [X] T003 Create `Directory.Packages.props` at repo root — add `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and pin all NuGet package versions: `Azure.AI.Vision.Face` 1.0.0-beta.1, `Azure.Data.Tables` 12.x, `Azure.Storage.Blobs` 12.x, `Azure.Security.KeyVault.Secrets` 4.x, `Azure.Identity` 1.x, `MediatR` 12.x, `Serilog.AspNetCore` 8.x, `Serilog.Sinks.ApplicationInsights` 4.x, `OpenTelemetry.Exporter.AzureMonitor` 1.x, `Microsoft.Identity.Web` 3.x, `Radzen.Blazor` latest, `Microsoft.Authentication.WebAssembly.Msal` 10.x, `xunit` 2.x, `Microsoft.AspNetCore.Mvc.Testing` 10.x, `Testcontainers.Azurite` latest, `Microsoft.Playwright` latest
- [X] T004 Scaffold `src/PoFace.Api` project with `dotnet new web -n PoFace.Api -o src/PoFace.Api` and add to `PoFace.sln`
- [X] T005 Scaffold `src/PoFace.Client` project with `dotnet new blazorwasm -n PoFace.Client -o src/PoFace.Client` and add to `PoFace.sln`
- [X] T006 Create `tests/PoFace.UnitTests` project with `dotnet new xunit -n PoFace.UnitTests -o tests/PoFace.UnitTests` and add to `PoFace.sln`
- [X] T007 Create `tests/PoFace.IntegrationTests` project with `dotnet new xunit -n PoFace.IntegrationTests -o tests/PoFace.IntegrationTests` and add to `PoFace.sln`
- [X] T008 Create `tests/PoFace.E2ETests` directory with `npm init -y`, add `playwright.config.ts`, and install `@playwright/test` — do NOT create a .NET project
- [X] T009 Create VSA folder skeleton inside `src/PoFace.Api/Features/`: `Auth/`, `GameSession/`, `Scoring/`, `Leaderboard/`, `Recap/`, `Diagnostics/`; create `src/PoFace.Api/Infrastructure/Auth/`, `Infrastructure/KeyVault/`, `Infrastructure/Storage/`, `Infrastructure/Telemetry/`, `Infrastructure/Logging/`
- [X] T010 Create `src/PoFace.Client/Features/` skeleton with sub-folders: `Home/`, `Arena/`, `Leaderboard/`, `Recap/`, `Diagnostics/`; create `src/PoFace.Client/Shared/`, `src/PoFace.Client/Services/`, `src/PoFace.Client/Interop/`; create `src/PoFace.Client/wwwroot/js/` and `src/PoFace.Client/wwwroot/css/`

---

## Phase 2 — Foundational Infrastructure (Blocking Prerequisites)

**Goal**: Core plumbing that every feature slice depends on — app bootstrap, Key Vault, logging, correlation IDs, auth scheme registration, and test infrastructure. Nothing user-visible yet.  
**Independent test**: `dotnet build` passes; `PoFaceWebAppFactory` starts in the integration test project without throwing.

- [X] T011 Write `src/PoFace.Api/Program.cs` — wire `WebApplication.CreateBuilder`, add essential services placeholder, call `app.Run()`; include static file middleware to serve WASM from `PoFace.Client/wwwroot`
- [X] T012 Write `src/PoFace.Api/Infrastructure/KeyVault/KeyVaultSecretLoader.cs` — use `SecretClient` with `DefaultAzureCredential` to load `FaceApiKey`, `StorageConnectionString`, `ApplicationInsightsConnectionString` into `IConfiguration` at startup; register via `builder.Configuration.AddAzureKeyVault()`
- [X] T013 Write `src/PoFace.Api/Infrastructure/Logging/SerilogConfiguration.cs` — configure Serilog with `WriteTo.Console()` and `WriteTo.ApplicationInsights()` using the App Insights connection string from Key Vault; enrich with `FromLogContext()`, `WithMachineName()`; call `UseSerilog()` in `Program.cs`
- [X] T014 Write `src/PoFace.Api/Infrastructure/Telemetry/CorrelationIdMiddleware.cs` — reads `X-Correlation-Id` header (or generates `Guid.NewGuid`), pushes to `LogContext.PushProperty("CorrelationId", ...)`, adds to response header, stores in `HttpContext.Items`; register in `Program.cs` before all other middleware
- [X] T015 Write `src/PoFace.Api/Infrastructure/Telemetry/OtelMetrics.cs` — register `Meter("PoFace.Api.Metrics")` with `ObservableGauge<double>` named `emotion.intensity.average` (dimension: `emotion`) and `Counter<long>` named `session.completion.count`; wire `AddOpenTelemetry()` → `WithMetrics()` → `AddAzureMonitorMetricExporter()` in `Program.cs`
- [X] T016 Write `src/PoFace.Api/Infrastructure/Auth/PoTestAuthExtensions.cs` — implement `AddPoTestAuth` as an `AuthenticationHandler` that reads `X-Test-User-Id` + `X-Test-Display-Name` headers and returns an authenticated `ClaimsPrincipal`; register only when `!app.Environment.IsProduction()`
- [X] T017 Register `Microsoft.Identity.Web` authentication in `Program.cs` for `Production`/`Staging` — `AddMicrosoftIdentityWebApiAuthentication()`; add `AddAuthorization()` with a default policy requiring authenticated user
- [X] T018 Write `src/PoFace.Api/Infrastructure/Storage/BlobStorageService.cs` — register `BlobServiceClient` from connection string; implement `UploadRoundImageAsync(userId, sessionId, roundNumber, stream)` returning public URL; implement `GetBlobUrlAsync(path)`
- [X] T019 Write `src/PoFace.Api/Infrastructure/Storage/TableStorageService.cs` — register `TableServiceClient` from connection string; implement generic `UpsertEntityAsync<T>` and `GetEntityAsync<T>` helpers
- [X] T020 Write `tests/PoFace.IntegrationTests/Infrastructure/AzuriteFixture.cs` — `IAsyncLifetime` using `AzuriteBuilder().Build()`, expose `ConnectionString`
- [X] T021 Write `tests/PoFace.IntegrationTests/Infrastructure/PoFaceWebAppFactory.cs` — extends `WebApplicationFactory<Program>`, overrides `ConfigureServices` to swap `TableServiceClient` + `BlobServiceClient` with Azurite-backed instances and call `AddPoTestAuth`
- [X] T022 [P] Write `tests/PoFace.UnitTests/Infrastructure/GlobalUsings.cs` — add xunit and project usings; verify unit test project compiles against `PoFace.Api`

---

## Phase 3 — User Story 1: Core Game Loop (Priority: P1)

**Story goal**: Authenticated user can play a complete 5-round game — camera feed → GET READY → countdown → shutter capture → ANALYZING → score reveal → slide transition → total score.  
**Independent test**: Start the app, navigate to `/arena`, play all 5 rounds manually. Final score screen shows a total out of 50. No persistence or leaderboard needed.

### Backend — Scoring Slice

- [X] T023 [P] Write `src/PoFace.Api/Features/Scoring/ScoreRoundCommand.cs` — record with `SessionId`, `RoundNumber`, `TargetEmotion`, `ImageBytes`; matching `ScoreRoundResult` record with all `RoundCapture` fields per [data-model.md](data-model.md)
- [X] T024 Write `src/PoFace.Api/Features/Scoring/FaceAnalysisService.cs` — wraps `FaceClient.DetectAsync()` with `Detection_03` + `Recognition04` and requests `HeadPose` + `Emotion` attributes; maps `FaceEmotionProperties` to `AnalysisResult`; returns `FaceDetected = false` when result list is empty
- [X] T025 Write `src/PoFace.Api/Features/Scoring/HeadPoseValidator.cs` — static `Validate(yaw, pitch)` returning `bool`; forces score to 0 when `Abs(yaw) > 20 || Abs(pitch) > 20`
- [X] T026 Write `src/PoFace.Api/Features/Scoring/ScoreRoundHandler.cs` — MediatR `IRequestHandler<ScoreRoundCommand, ScoreRoundResult>`; calls `FaceAnalysisService`, `HeadPoseValidator`, `BlobStorageService.UploadRoundImageAsync`, `TableStorageService.UpsertEntityAsync<RoundCapture>`; maps result; instruments `emotion.intensity.average` OTel gauge
- [X] T027 Write `src/PoFace.Api/Features/Scoring/ScoringEndpoints.cs` — `POST /api/sessions/{sessionId}/rounds/{roundNumber}/score` multipart endpoint; enforces 500 KB limit + `image/jpeg` Content-Type; delegates to `IMediator.Send(ScoreRoundCommand)`; returns `ScoreRoundResult` as JSON; includes warmup ping call to Face API during round 1
- [X] T028 [P] [US1] Write `tests/PoFace.UnitTests/Scoring/HeadPoseValidatorTests.cs` — test cases: yaw=0/pitch=0 → valid; yaw=21 → invalid; pitch=-21 → invalid; yaw=20/pitch=20 → valid (boundary)
- [X] T029 [P] [US1] Write `tests/PoFace.UnitTests/Scoring/ScoreRoundHandlerTests.cs` — mock `FaceAnalysisService`, `BlobStorageService`, `TableStorageService`; verify: score = `Round(confidence * 10)` when head pose valid; score = 0 when head pose invalid; score = 0 when `FaceDetected = false`
- [X] T030 [P] [US1] Write `tests/PoFace.IntegrationTests/Scoring/ScoringEndpointTests.cs` — use `PoFaceWebAppFactory`; POST a real 640×480 JPEG to the endpoint with `X-Test-User-Id` header; assert 200 with valid `ScoreRoundResult` JSON; assert 413 on oversized payload; assert 415 on non-JPEG

### Backend — GameSession Slice

- [X] T031 [P] Write `src/PoFace.Api/Features/GameSession/StartSessionCommand.cs` — creates a new `GameSession` in Table Storage (PartitionKey=UserId, RowKey=new Guid); returns the **fixed** 5-emotion sequence in canonical order: Happiness → Surprise → Anger → Sadness → Fear (FR-009: order is hardcoded, NOT shuffled); increments Player `LastSeenAt`
- [X] T032 [P] Write `src/PoFace.Api/Features/GameSession/CompleteSessionCommand.cs` — verifies all 5 rounds are scored; computes `TotalScore`; sets `IsPersonalBest` flag; sets `ExpiresAt` for non-best sessions; persists `GameSession` to Table Storage; increments `session.completion.count` OTel counter
- [X] T033 [P] Write `src/PoFace.Api/Features/GameSession/GameSessionEndpoints.cs` — `POST /api/sessions` (start), `POST /api/sessions/{id}/complete`, `DELETE /api/sessions/{id}` (discard); all require auth except none have special role; delegate to MediatR

### Client — Arena Page & Game Orchestrator

- [X] T034 Write `src/PoFace.Client/wwwroot/js/webcam.js` — export `window.webcamInterop` with `initCamera(videoElementId)`, `captureFrame(videoElementId)`, `releaseCamera()`, `flashShutter(overlayElementId)` per [contracts/blazor-interop.md](contracts/blazor-interop.md); enforce 640×480 canvas; apply bitrate adaptation (`navigator.connection.downlink < 1.0` → quality 0.60, else 0.85)
- [X] T035 Write `src/PoFace.Client/Services/GameOrchestrator.cs` — client-side state machine with states: `Idle` → `GetReady` (2 s fixed) → `Countdown` (3-2-1) → `Capturing` → `Analyzing` → `ScoreReveal` (2 s hold) → `SlideTransition` → `GameOver`; manages round index (1–5); calls `IJSRuntime` for camera ops and audio; calls `ApiClient` for scoring; exposes `StateChanged` event; on entering `Capturing` state MUST call `AudioService.VibrateDevice([200])` for shutter haptic (FR-039)
- [X] T036 Write `src/PoFace.Client/Services/ApiClient.cs` — typed `HttpClient` wrapper; implements `StartSessionAsync()`, `ScoreRoundAsync(sessionId, roundNumber, jpegBytes)`, `CompleteSessionAsync(sessionId)`, `DiscardSessionAsync(sessionId)`; all methods add `Authorization: Bearer {token}` header from MSAL token acquisition
- [X] T037 Write `src/PoFace.Client/Features/Arena/ArenaPage.razor` — full game page; subscribes to `GameOrchestrator.StateChanged`; renders: camera `<video>`, `FaceGuide`, `CountdownOverlay`, `FrozenFrameOverlay`, score display, slide transition; calls `JS.InvokeVoidAsync("webcamInterop.initCamera")` in `OnAfterRenderAsync`; implements `IAsyncDisposable` to call `releaseCamera`
- [X] T038 [P] Write `src/PoFace.Client/Features/Arena/CountdownOverlay.razor` — renders 3/2/1 numerals over camera feed; on each tick calls `AudioService.PlayBlip()` AND `AudioService.VibrateDevice([80])` (FR-039: short vibration MUST fire on each countdown tick on supported devices); driven by `GameOrchestrator` state
- [X] T039 [P] Write `src/PoFace.Client/Features/Arena/FrozenFrameOverlay.razor` — renders captured JPEG as `<img>`; overlays `ANALYZING…` spinner while `GameOrchestrator.State == Analyzing`; replaces with animated count-up `0 → score` over 500 ms once score arrives; holds for 2 s from score reveal
- [X] T040 [P] Write `src/PoFace.Client/Features/Arena/FaceGuide.razor` — renders the centered glowing ellipse overlay; turns red and pulses when head pose is invalid (driven by boolean parameter from `ArenaPage`)
- [X] T041 [US1] Write `tests/PoFace.E2ETests/tests/game-loop.spec.ts` — Playwright test using `authFixture.ts` (inject `X-Test-User-Id` header); navigate to `/arena`; mock camera via Playwright `page.route`; step through all 5 rounds; assert score screen displays total out of 50

---

## Phase 4 — User Story 2: Authentication & Best-Match Leaderboard (Priority: P2)

**Story goal**: Microsoft Account authentication gates the Arena. Completed sessions upsert a leaderboard entry only when the new score beats the stored personal best. Leaderboard page lists one entry per user sorted by total score descending.  
**Independent test**: Log in via dev auth, complete two sessions (high score then low score), verify leaderboard shows one entry with the high score only.

### Backend — Auth Slice

- [X] T042 Write `src/PoFace.Api/Features/Auth/DevLoginEndpoint.cs` — `POST /dev-login` — reads `{userId, displayName}` body, sets `X-Test-User-Id` + `X-Test-Display-Name` cookies; returns 403 in Production
- [X] T043 [P] Write `src/PoFace.Api/Features/Auth/AuthEndpoints.cs` — `GET /api/auth/me` — returns `{userId, displayName}` from current authenticated `ClaimsPrincipal`; requires auth; used by WASM client to confirm identity post-login

### Backend — Leaderboard Slice

- [X] T044 Write `src/PoFace.Api/Features/Leaderboard/LeaderboardTableRepository.cs` — Table Storage access for `Leaderboard` table (PartitionKey: Year, RowKey: UserId); implements `GetEntryAsync(year, userId)` and `UpsertEntryAsync(entry)`
- [X] T045 Write `src/PoFace.Api/Features/Leaderboard/UpsertLeaderboardEntryCommand.cs` + handler — implements best-match upsert strategy: read existing, compare `TotalScore`, write only if higher (or no existing entry); tagged with `[BestMatchUpsertStrategy]` comment per constitution Principle II
- [X] T046 Write `src/PoFace.Api/Features/Leaderboard/GetLeaderboardQuery.cs` + handler — reads all entries for current year from Table Storage; paginates; sorts by `TotalScore` desc then `AchievedAt` **desc** (recency tie-break: most recent submission ranked higher per FR-023); enforces `top` param max 500
- [X] T047 Write `src/PoFace.Api/Features/Leaderboard/LeaderboardEndpoints.cs` — `GET /api/leaderboard?top={n}` — no auth required; delegates to `GetLeaderboardQuery`; returns paginated leaderboard JSON per [contracts/api-endpoints.md](contracts/api-endpoints.md)
- [X] T048 [P] [US2] Write `tests/PoFace.UnitTests/Leaderboard/UpsertLeaderboardEntryTests.cs` — test cases: no existing entry → insert; new score > existing → replace; new score = existing → no-op; new score < existing → no-op
- [X] T049 [P] [US2] Write `tests/PoFace.IntegrationTests/Leaderboard/LeaderboardEndpointTests.cs` — use `PoFaceWebAppFactory` + Azurite; seed two users; verify sort order, single entry per user, tie-break by recency

### Client — Auth + Leaderboard Page

- [X] T050 Write `src/PoFace.Client/Program.cs` — configure MSAL: `builder.Services.AddMsalAuthentication(opts => opts.ProviderOptions.DefaultAccessTokenScopes.Add("api://..."))`; register `ApiClient`, `GameOrchestrator`, `AudioService`; configure `HttpClient` base address
- [X] T051 Write `src/PoFace.Client/App.razor` — configure `<Router>`; add `<AuthorizeRouteView>` for auth-guarded routes; redirect unauthenticated users attempting `/arena` to Microsoft login
- [X] T052 Write `src/PoFace.Client/Features/Leaderboard/LeaderboardPage.razor` — calls `GET /api/leaderboard`; renders `<RadzenDataGrid>` with columns: rank, display name, total score, device type emoji, "View Gallery" link; no auth required
- [X] T053 [P] [US2] Write `tests/PoFace.E2ETests/tests/leaderboard.spec.ts` — Playwright test; complete two sessions via API; assert leaderboard page shows one row per user with correct score; assert lower-score session does NOT replace existing entry

---

## Phase 5 — User Story 3: Match Recap Gallery (Priority: P3)

**Story goal**: Every completed session has a publicly accessible recap URL showing all 5 round images with emotion labels, scores, and timestamps. Leaderboard "View Gallery" links navigate here. Non-best sessions remain accessible for 24 hours.  
**Independent test**: Complete a session, copy the recap URL, open it in an incognito window (no auth) — all 5 images and metadata render correctly.

### Backend — Recap Slice

- [X] T054 Write `src/PoFace.Api/Features/Recap/BlobImageRepository.cs` — reads blob URLs for a session by prefix `{userId}/{sessionId}/round-*.jpg`; returns ordered list of 5 URLs; handles missing blobs with placeholder URL constant
- [X] T055 Write `src/PoFace.Api/Features/Recap/GetRecapQuery.cs` + handler — reads `GameSession` + all 5 `RoundCaptures` from Table Storage; calls `BlobImageRepository` for image URLs; checks `ExpiresAt` and returns 410 if expired; no auth required
- [X] T056 Write `src/PoFace.Api/Features/Recap/RecapEndpoints.cs` — `GET /api/recap/{sessionId}` — no auth; delegates to `GetRecapQuery`; returns 404/410/200 per contract; sets correct `Cache-Control` headers (public, 1 hour for best-match; private no-store for temp sessions)
- [X] T057 [P] [US3] Write `tests/PoFace.IntegrationTests/Recap/RecapEndpointTests.cs` — use `PoFaceWebAppFactory` + Azurite; seed a completed session; verify 200 with 5 round panels; verify 410 for an expired non-best session; verify no auth header required

### Client — Recap Page

- [X] T058 Write `src/PoFace.Client/Features/Recap/RecapPage.razor` — accepts `sessionId` route parameter; calls `GET /api/recap/{sessionId}`; renders 5 `RoundPanel` components; handles 410 with "Session expired" message; no auth required (`[AllowAnonymous]`)
- [X] T059 [P] Write `src/PoFace.Client/Features/Recap/RoundPanel.razor` — renders single round card: `<img src="imageUrl">`, emotion name, score badge (e.g., "7/10"), timestamp
- [X] T060 [US3] Write `tests/PoFace.E2ETests/tests/recap.spec.ts` — Playwright test; complete a session; navigate to `recapUrl` without auth cookies; assert all 5 images visible; assert emotion labels and scores present

---

## Phase 6 — User Story 4: Terminal UI & Aesthetic Fidelity (Priority: P4)

**Story goal**: All 5 pages render in the "Matrix" terminal aesthetic — black background, neon green, monospaced fonts, CRT scanline overlay, neon glow on interactive elements. Arena face guide pulses red on bad head pose. Layout is responsive on mobile + desktop.  
**Independent test**: Load all 5 pages in Chrome and Firefox on desktop and mobile viewport in DevTools. Visually confirm colour palette, fonts, scanline, and glow on buttons.

- [X] T061 Write `src/PoFace.Client/wwwroot/css/terminal.css` — define: `background: #0a0a0a`, `color: #00ff00`, monospaced font stack (`Courier New, Courier, monospace`), `.crt-scanline` keyframe animation, `.glow` utility class (`box-shadow: 0 0 8px #00ff00`), `.text-flicker` animation, CSS custom properties `--color-primary`, `--color-bg`, `--accent`
- [X] T062 Referencing `terminal.css` in `src/PoFace.Client/wwwroot/index.html` — add `<link rel="stylesheet" href="css/terminal.css">` and global `<div class="crt-scanline">` wrapper; import both `webcam.js` and `audio.js` as ES modules
- [X] T063 Write `src/PoFace.Client/Shared/ThemeService.cs` — calls `RadzenThemeService.SetTheme("material-dark")` on init; injects CSS variable overrides to remap Radzen primary/accent colours to `#00ff00` / `#0a0a0a`
- [X] T064 Write `src/PoFace.Client/Shared/MainLayout.razor` — terminal header with ASCII logo, auth status (display name when logged in), nav links; footer with PoVerify link
- [X] T065 [P] Write `src/PoFace.Client/Features/Home/HomePage.razor` — ASCII-art logo block, "System Status: Active" indicator, "How To Play" section (5-round loop explanation), "Start Game" CTA button (guarded by auth)
- [X] T066 [P] Write `src/PoFace.Client/Features/Arena/arena.module.css` — Blazor CSS isolation for Arena-only animations: shutter-flash keyframe (white 150 ms), slide-in/slide-out horizontal transition, face guide red pulse animation
- [X] T067 [US4] Write `tests/PoFace.E2ETests/tests/aesthetics.spec.ts` — Playwright test; for each of the 5 pages assert: `background-color` is `rgb(10, 10, 10)`; primary text `color` is `rgb(0, 255, 0)`; at least one element has `font-family` containing `monospace`; scanline element exists

---

## Phase 7 — User Story 5: Programmatic Audio & Haptic Feedback (Priority: P5)

**Story goal**: All sounds produced via Web Audio API oscillators (no file loads). Countdown ticks → 880 Hz square blip; shutter → white noise burst; score ≥ 8 → C major chord chime. Mobile vibration at ticks and shutter.  
**Independent test**: Play a session with Chrome DevTools Network tab open — confirm zero audio file requests (.mp3/.wav/.ogg). Verify three distinct sounds fire at correct game moments.

- [X] T068 Write `src/PoFace.Client/wwwroot/js/audio.js` — export `window.audioInterop` with `playBlip()` (880 Hz square, 80 ms), `playShutter()` (white noise buffer, 120 ms), `playSuccessChime()` (C5/E5/G5 sine chord, 600 ms), `vibrateDevice(pattern[])` (guards with `if (navigator.vibrate)`); lazy-init `AudioContext` on first call; silence errors silently
- [X] T069 Write `src/PoFace.Client/Services/AudioService.cs` — C# wrapper calling `IJSRuntime.InvokeVoidAsync("audioInterop.playBlip" / "playShutter" / "playSuccessChime" / "vibrateDevice")`; all calls wrapped in `try/catch` that swallows `JSException` to ensure game continues without audio
- [X] T070 [P] [US5] Write `tests/PoFace.UnitTests/Audio/AudioServiceTests.cs` — mock `IJSRuntime`; verify correct JS function name is called for each audio event; verify swallowed exception does not propagate
- [X] T071 [US5] **Edit** (do NOT recreate) `tests/PoFace.E2ETests/tests/aesthetics.spec.ts` created in T067 — add a new test block asserting zero network requests to `.mp3|.wav|.ogg|.flac` files during a full session, using a `page.on('request', ...)` listener registered before navigation

---

## Phase 8 — User Story 6: System Diagnostics (PoVerify) (Priority: P6)

**Story goal**: Authenticated users can navigate to `/diagnostics` and see real-time green/red health for all three backing services, masked config key display, and a JSON version + region block.  
**Independent test**: Navigate to `/diagnostics` — verify all service indicators present, config values masked, JSON block shows version string and region.

### Backend — Diagnostics Slice

- [X] T072 Write `src/PoFace.Api/Features/Diagnostics/ConfigMaskingService.cs` — `Mask(string key)` returns first 4 chars + `"****"` + last 4 chars; handles strings shorter than 8 chars safely
- [X] T073 Write `src/PoFace.Api/Features/Diagnostics/DiagnosticsQuery.cs` + handler — probes `BlobServiceClient.GetPropertiesAsync()`, `TableServiceClient.GetPropertiesAsync()`, Face API ping; builds `DiagnosticsReport` with masked config values, `Assembly.GetEntryAssembly().GetName().Version`, `Environment.GetEnvironmentVariable("WEBSITE_REGION")`; requires auth
- [X] T074 Write `src/PoFace.Api/Features/Diagnostics/DiagnosticsEndpoints.cs` — `GET /api/diag` — requires auth; always returns HTTP 200; delegates to `DiagnosticsQuery`; returns JSON per [contracts/api-endpoints.md](contracts/api-endpoints.md)
- [X] T075 [P] [US6] Write `tests/PoFace.UnitTests/Diagnostics/ConfigMaskingServiceTests.cs` — test: 8-char key → "ABCD****WXYZ"; 6-char key → safe output; empty string → safe output
- [X] T076 [P] [US6] Write `tests/PoFace.IntegrationTests/Diagnostics/DiagnosticsEndpointTests.cs` — use `PoFaceWebAppFactory` + Azurite; assert 200 when authenticated; assert 401 when no auth header; assert all three service statuses in response body

### Client — Diagnostics Page

- [X] T077 [US6] Write `src/PoFace.Client/Features/Diagnostics/DiagnosticsPage.razor` — requires auth (`[Authorize]`); calls `GET /api/diag`; renders three status indicators (green/red dot + service name); renders masked config keys; renders raw JSON block with version + region; auto-refreshes every 5 seconds

---

## Phase 9 — Quality & Coverage Gaps (Cross-Cutting)

**Goal**: Close every coverage gap and constitution violation identified in the analysis report before the first production deployment.

- [X] T078 [M4] Write `tests/PoFace.E2ETests/fixtures/authFixture.ts` — Playwright reusable fixture that calls `page.setExtraHTTPHeaders({ 'X-Test-User-Id': ..., 'X-Test-Display-Name': ... })`; export as `test` base that all other E2E spec files import; update `playwright.config.ts` to pick up the `fixtures/` folder
- [X] T079 [M5] Delete all `dotnet new blazorwasm` boilerplate files from `src/PoFace.Client`: `Pages/Counter.razor`, `Pages/FetchData.razor`, `Shared/SurveyPrompt.razor`, `wwwroot/sample-data/`, any generated `WeatherForecast` classes; remove their route entries from `App.razor` (constitution Principle I — Zero-Waste)
- [X] T080 [M1] Configure Azure Blob Storage lifecycle management policy — create ARM/CLI script (or Bicep module) in `infra/storage-lifecycle.json` that: moves blobs in `poface-captures` container to Cool tier after 180 days of inactivity, Archive tier after 365 days, and deletes non-best-match blobs after 2 days (FR-030 + data-model.md lifecycle table); add a task note in `quickstart.md` pointing to the script
- [X] T081 [M6] Write `tests/PoFace.UnitTests/Leaderboard/GetLeaderboardQueryTests.cs` — test cases: empty table → empty list; single entry → returned; two entries equal score, older then newer → newer ranked first (tie-break desc); two entries different scores → higher score ranked first; `top` param = 1 → only 1 entry returned; `top` > 500 → clamped to 500
- [X] T082 [M6] Write `tests/PoFace.UnitTests/Recap/GetRecapQueryTests.cs` — test cases: all 5 blobs exist → 200 with 5 panels; session `ExpiresAt` in the past → 410; session not found → 404; one blob missing → panel shows placeholder URL constant
- [X] T083 [M7] [SC-008] Extend `tests/PoFace.E2ETests/tests/game-loop.spec.ts` — add a slow-network test variant using Playwright CDP `Network.emulateNetworkConditions` (3G profile: `downloadThroughput: 375000, uploadThroughput: 125000`); play round 1; assert frozen frame renders within 3 000 ms of shutter event; assert JPEG capture quality reduction is triggered (verify request payload size < 40 KB)
- [X] T084 [L3] [FR-033] Extend `tests/PoFace.IntegrationTests/Leaderboard/LeaderboardEndpointTests.cs` — add assertion that `GET /api/leaderboard` response JSON contains no fields beyond `rank`, `displayName`, `totalScore`, `deviceType`, `sessionId`, `achievedAt`; assert no email address, OID, or access token appears in any serialised field
- [X] T085 [L4] Write `tests/PoFace.UnitTests/Shared/ThemeServiceTests.cs` — mock `RadzenThemeService`; verify `SetTheme("material-dark")` is called exactly once on `ThemeService` initialization; verify CSS custom property injection method is called with `--color-primary: #00ff00` and `--color-bg: #0a0a0a`
- [X] T086 [L5] Extend T067's `tests/PoFace.E2ETests/tests/aesthetics.spec.ts` — add a responsive-layout test block: for each of the 5 pages, set viewport to 390×844 (iPhone 14), assert `document.body.scrollWidth <= 390` (no horizontal overflow), assert all `<button>` elements have `offsetHeight >= 44` and `offsetWidth >= 44` (minimum touch target per SC-010)

---

## Dependencies

```text
Phase 1 (Setup)
  └─► Phase 2 (Foundational Infrastructure)
        ├─► Phase 3 (US1: Core Game Loop)     ← MVP delivery gate
        │     └─► Phase 4 (US2: Auth + Leaderboard)
        │               └─► Phase 5 (US3: Recap Gallery)
        │                         ├─► Phase 6 (US4: Terminal UI)     ← can start after Phase 3
        │                         ├─► Phase 7 (US5: Audio)           ← can start after Phase 3
        │                         └─► Phase 8 (US6: Diagnostics)     ← can start after Phase 2
        └─► Phase 8 (US6: Diagnostics)        ← independent of Phases 3-7
```

**US4 (Terminal UI) and US5 (Audio) are parallel to each other** — both depend only on Phase 3 being complete. They do not depend on US2 or US3.  
**US6 (Diagnostics) is independent** of all gameplay stories and can be implemented any time after Phase 2.

---

## Parallel Execution Opportunities

| Can run in parallel | Condition |
|---|---|
| T023, T028, T031 (backend scoring + session commands + unit tests) | All in different files, no dependencies on each other |
| T034 (webcam.js) and T038/T039/T040 (Blazor overlay components) | Different files; both feed into T037 (ArenaPage) |
| T044, T046 (Leaderboard repository + query handler) | Different handlers in same slice |
| T054, T055 (Recap blob repo + query handler) | Different files in Recap slice |
| T061, T063, T064, T065 (CSS + ThemeService + Layout + HomePage) | All pure UI files; no inter-dependencies |
| T068 (audio.js) and T069 (AudioService.cs) | Different languages/files; both feed into GameOrchestrator |
| T072, T073 (masking + diagnostics query) | Different classes in Diagnostics slice |

---

## Implementation Strategy

**MVP Scope (Phase 1–3 only)**:  
Complete Phases 1, 2, and 3 first — this delivers a fully playable, tested game loop with scoring. Everything in Phase 4+ builds on this working foundation.

1. **Phase 1** — Scaffold once; takes ~30 minutes; unblocks all other phases
2. **Phase 2** — Do not skip; the test infrastructure (AzuriteFixture, PoFaceWebAppFactory) is load-bearing for all integration tests
3. **Phase 3** — Largest phase; complete backend before client; use unit tests (T028-T030) to drive `ScoreRoundHandler` before wiring up the Blazor client
4. **Phases 4-7** — Deliver in parallel pairs: (4+5 together) then (6+7 together); US6 can be threaded in at any point
5. **Throughout** — Run `dotnet test` after completing each backend task; run `npx playwright test` after completing the relevant E2E spec file

**Task count per user story**:
| Story | Tasks | Complexity |
|---|---|---|
| Setup + Infra | T001–T022 | 22 tasks — one-time investment |
| US1: Core Game Loop | T023–T041 | 19 tasks — largest; highest complexity |
| US2: Auth + Leaderboard | T042–T053 | 12 tasks |
| US3: Recap Gallery | T054–T060 | 7 tasks |
| US4: Terminal UI | T061–T067 | 7 tasks |
| US5: Audio | T068–T071 | 4 tasks |
| US6: Diagnostics | T072–T077 | 6 tasks |
| Quality & Coverage | T078–T086 | 9 tasks — close analysis-report gaps |
| **Total** | **T001–T086** | **86 tasks** |
