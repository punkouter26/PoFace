# Research: PoFace — Arcade Emotion-Matching Platform

**Phase**: 0 — Pre-design research  
**Branch**: `001-poface-arcade-platform`  
**Date**: 2026-03-09

All NEEDS CLARIFICATION items from the Technical Context are resolved below. Each decision records what was chosen, the rationale, and what alternatives were considered.

---

## 1. Azure Face API — `Detection_03` Emotion & HeadPose Analysis

**Decision**: Use `Azure.AI.Vision.Face` SDK v1.0.0-beta.1 with `FaceDetectionModel.Detection03` and `FaceRecognitionModel.Recognition04`. Request `FaceAttributeType.ModelDetection03.HeadPose` and `FaceAttributeType.ModelDetection03.QualityForRecognition` alongside `FaceAttributeType.ModelDetection03.FaceOccluded`. Emotion is inferred via `FaceAttributeType.ModelDetection03.Smile` (0–1 float) combined with the `HeadPose` angles; the full emotion classification (Happiness/Surprise/Anger/Sadness/Fear) is available via `FaceAttributeType.ModelDetection03.Emotion` which returns a `FaceEmotionProperties` object with a float confidence per emotion.

**API call shape**:
```csharp
var result = await faceClient.DetectAsync(
    BinaryData.FromBytes(imageBytes),
    FaceDetectionModel.Detection03,
    FaceRecognitionModel.Recognition04,
    returnFaceId: false,
    returnFaceAttributes: [
        FaceAttributeType.ModelDetection03.HeadPose,
        FaceAttributeType.ModelDetection03.Emotion,
        FaceAttributeType.ModelDetection03.QualityForRecognition,
    ]
);
```

**Score mapping**: The target emotion's confidence (0.0–1.0) is multiplied by 10 and rounded to the nearest integer (0–10). Head-pose penalty: if `Math.Abs(headPose.Yaw) > 20 || Math.Abs(headPose.Pitch) > 20`, score is forced to 0 before returning.

**Rationale**: `Detection_03` is the highest-fidelity model available in the current SDK and supports the full emotion set. `Recognition04` is required when `Detection_03` is used. `returnFaceId: false` avoids persisting biometric identifiers in Azure (GDPR / privacy hygiene).

**Alternatives considered**:
- Custom ML model (rejected — maintenance burden, training data requirements, no advantage over Face API for standard expressions)
- Azure AI Content Safety (rejected — no emotion classification capability)
- `Detection_01`/`Detection_02` (rejected — lower fidelity, no full emotion set in `Detection_03`-tier attributes)

---

## 2. Testcontainers + Azurite for Integration Testing

**Decision**: Use `Testcontainers.Azurite` NuGet package (wraps `mcr.microsoft.com/azure-storage/azurite` Docker image). Spin up a single `AzuriteContainer` per test collection via `IAsyncLifetime`; share the container for both Blob and Table tests using the built-in multi-service mode. Use `UseDevelopmentStorage=true` connection string via the Azurite connection string property.

**AzuriteFixture pattern**:
```csharp
public class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder().Build();
    public string ConnectionString => _container.GetConnectionString();
    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.StopAsync();
}
```

**`WebApplicationFactory` wiring**: Override `ConfigureServices` to replace `TableServiceClient` and `BlobServiceClient` registrations with Azurite-backed instances using the fixture connection string. Use `AddPoTestAuth` to bypass MSAL for authenticated endpoint tests.

**Rationale**: Azurite is Microsoft's official local emulator for Azure Storage. Testcontainers manages Docker lifecycle automatically — no manual container setup in CI. Using real storage emulation catches serialization bugs, partition key issues, and blob URL construction problems that mocks would miss.

**Alternatives considered**:
- In-memory mocks via `Moq`/`NSubstitute` (rejected — misses real Table entity serialization and blob URL format issues)
- Azure Storage emulator (deprecated — replaced by Azurite)
- Real Azure Storage in CI (rejected — requires cloud credentials in CI, costs money per test run, non-deterministic)

---

## 3. Microsoft Identity Web + Blazor WASM + `AddPoTestAuth`

**Decision**:
- **Production**: Backend uses `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApiAuthentication`). Frontend uses `Microsoft.Authentication.WebAssembly.Msal` with `builder.Services.AddMsalAuthentication(...)` pointed at the registered Azure App Registration (Entra ID tenant).
- **Development/Test**: Backend registers `AddPoTestAuth` — a custom `IAuthenticationHandler` that reads identity from a request header (e.g., `X-Test-User-Id` + `X-Test-Display-Name`). A `/dev-login` endpoint sets these headers as a cookie for browser-based local dev. This extension is registered **only** when `ASPNETCORE_ENVIRONMENT` is `Development` or `Testing` and **never** compiled into the Release build (guarded by `#if DEBUG` or environment check).
- **`AddPoTestAuth` implementation**: Implements `AuthenticationHandler<AuthenticationSchemeOptions>`, reads the specified header, constructs a `ClaimsPrincipal` with `sub` and `name` claims matching real MSAL token shape, returns `AuthenticateResult.Success(ticket)`.

**Playwright E2E**: Tests inject the `X-Test-User-Id` / `X-Test-Display-Name` headers via `authFixture.ts` which sets them in `extraHTTPHeaders` on the Playwright `Browser` context. E2E tests target the API directly via `request` fixtures when testing API behavior; they use the full WASM app with header injection for full-flow tests.

**Rationale**: Real MSAL in headless CI requires a registered test user, a real Entra ID tenant, and a browser automation flow for interactive login — fragile, slow, and requires secret management in CI. `AddPoTestAuth` is the standard ASP.NET Core integration-test identity pattern endorsed by Microsoft's integration testing docs.

**Alternatives considered**:
- Real MSAL in CI (rejected — see above)
- No auth in tests (rejected — auth is required to play; tests must cover authenticated paths)
- JWT self-signed token generation (rejected — `AddPoTestAuth` is simpler, more readable, and is the established pattern)

---

## 4. Web Audio API — Programmatic 8-bit Sound Design

**Decision**: All sounds use the `OscillatorNode` + `GainNode` pattern via the browser's `AudioContext`. No audio files are loaded.

| Sound | Type | Frequency | Duration | Notes |
|---|---|---|---|---|
| Countdown blip (3, 2, 1) | `square` oscillator | 880 Hz | 80 ms | Linear ramp gain 0→0.3→0 |
| Shutter capture | White noise (`AudioBuffer` random fill) | Broadband | 120 ms | Rapid gain 0→0.5→0 (frequency sweep feel) |
| Success chime (score ≥ 8) | Three `sine` oscillators | 523 Hz (C5) + 659 Hz (E5) + 784 Hz (G5) | 600 ms | Harmonic C major chord; slow gain decay |

**JS Interop interface** (`audio.js`):
```javascript
export function playBlip() { ... }
export function playShutter() { ... }
export function playSuccessChime() { ... }
```

Called from `AudioService.cs` via `IJSRuntime.InvokeVoidAsync("audioInterop.playBlip")` etc. The `AudioContext` is created lazily on first user gesture to comply with browser autoplay policy.

**Haptics**: `navigator.vibrate([80])` on countdown ticks; `navigator.vibrate([200])` on shutter; wrapped in a try/catch to silently skip on unsupported devices.

**Rationale**: Web Audio API oscillators are universally supported in 2026 browsers, produce authentic 8-bit arcade sounds, and add zero bytes to the download bundle. Autoplay policy requires the `AudioContext` to be created or resumed after a user gesture (the "Start Game" click satisfies this).

**Alternatives considered**:
- Audio sprite (rejected — violates zero-asset philosophy)
- Tone.js library (rejected — adds bundle weight for functionality we can achieve natively)
- `<audio>` elements with data URIs (rejected — still counts as audio files logically and adds base64 payload to the bundle)

---

## 5. Bitrate Adaptation — Connection Speed Detection

**Decision**: Use `navigator.connection.downlink` (Network Information API) before round 1 to estimate upload bandwidth (downlink is a reasonable proxy for upload on most connections). If `downlink < 1.0` (Mbps), reduce JPEG quality from `0.85` to `0.60` in the canvas `toDataURL` call. If the API is unavailable (Safari, Firefox — Network Information API is Chromium-only), default to `0.85`.

**Canvas capture shape**:
```javascript
// webcam.js
const canvas = document.createElement('canvas');
canvas.width = 640; canvas.height = 480;
const ctx = canvas.getContext('2d');
ctx.drawImage(videoElement, 0, 0, 640, 480);
const quality = navigator.connection?.downlink < 1.0 ? 0.60 : 0.85;
return canvas.toDataURL('image/jpeg', quality);
```

**Rationale**: The Network Information API is available in Chrome/Edge (the dominant mobile browsers) and provides a real-time downlink estimate. Falling back to 0.85 on non-supporting browsers is safe — those are typically desktop environments where bandwidth is less of a concern. The fixed 640×480 capture resolution is enforced by the canvas dimensions, ensuring fairness on the leaderboard regardless of camera hardware.

**Alternatives considered**:
- Server-side quality reduction (rejected — the bottleneck is the upload from client to server; reducing after transmission doesn't help latency)
- Measuring actual upload speed via a test byte sequence (rejected — adds 500–800 ms overhead before round 1 starts; `navigator.connection` is near-instant)
- Always use 0.60 (rejected — degrades image quality unnecessarily for users on fast connections, affecting Face API analysis accuracy)

---

## 6. OpenTelemetry + Azure Monitor — Custom Metrics

**Decision**: Register OTel in `Program.cs` with `AddOpenTelemetry()` → `WithMetrics()` → `AddAzureMonitorMetricExporter()`. Define a single named `Meter` (`PoFace.Api.Metrics`) with two instruments:
- `emotion.intensity.average` — `ObservableGauge<double>` updated per scoring call, tagged with `emotion` dimension
- `session.completion.count` — `Counter<long>` incremented on every completed session

Azure Monitor exporter uses `APPLICATIONINSIGHTS_CONNECTION_STRING` from Key Vault (via `AddEnvironmentVariables` populated by Key Vault at startup).

**Rationale**: OTel is the 2026 standard for instrumentation; Azure Monitor exporter is the native sink for Punkouter26 subscription monitoring. Custom metrics with an `emotion` dimension enable the "Average Intensity per Emotion" dashboard widget described in the blueprint.

**Alternatives considered**:
- `TelemetryClient.TrackMetric` directly (rejected — deprecated pattern; OTel is the forward-compatible approach)
- Prometheus exporter (rejected — no Prometheus infrastructure in the Punkouter26 subscription; Azure Monitor is already in use)

---

## 7. Blazor CSS Isolation + Radzen ThemeService

**Decision**:
- **Global terminal styles** live in `wwwroot/css/terminal.css` (imported in `index.html`). This covers: `background-color: #0a0a0a`, `color: #00ff00`, monospaced font stack, CRT scanline `@keyframes` animation, `.glow` box-shadow utility class, and text flicker animation.
- **Component-scoped styles** (arena animations — shutter flash, slide transition) use Blazor CSS isolation (`.razor.css` files) to avoid leaking into other components.
- **Radzen theming**: `ThemeService.cs` calls `RadzenThemeService.SetTheme("material-dark")` on startup and injects CSS variable overrides in `terminal.css` to remap Radzen's primary/accent colors to `#00ff00` / `#0a0a0a`. Radzen's own component JS is loaded via `_content/Radzen.Blazor/Radzen.Blazor.js` in `index.html`.

**DataGrid**: The Leaderboard uses `<RadzenDataGrid>` with a custom `<Template>` for the device icon column and a programmatic `LoadData` handler for server-side sorting.

**Rationale**: CSS isolation prevents arena-specific high-frequency animations (shutter flash at 150ms, slide transition) from inadvertently applying to other pages. Global terminal aesthetic styles must apply everywhere so they stay in `wwwroot/css`. Radzen CSS variable override is the recommended theming approach in Radzen docs and avoids forking Radzen source.

**Alternatives considered**:
- Full custom CSS grid instead of Radzen DataGrid (rejected — spec explicitly requires Radzen for advanced UI; built-in Blazor WASM doesn't include a feature-complete sortable/pageable grid)
- MudBlazor (rejected — spec mandates Radzen)
- Tailwind CSS (rejected — adds build tooling complexity; all needed styles can be achieved with vanilla CSS given the minimal palette)

---

## 8. Correlation ID Propagation

**Decision**: `CorrelationIdMiddleware` runs first in the pipeline. On every request, it reads `X-Correlation-Id` header (if present, e.g., from a retry) or generates a new `Guid`. It stores the ID in `IHttpContextAccessor` and pushes it to the Serilog `LogContext` via `LogContext.PushProperty("CorrelationId", correlationId)`. The same ID is added to the HTTP response header `X-Correlation-Id`. All log statements within a request automatically include the correlation ID via Serilog's enrichment pipeline. The ID is also returned in `ProblemDetails.Extensions["correlationId"]` on error responses.

**Rationale**: Correlation IDs are essential for tracing a single image upload → Face API call → Table Storage write across potentially multiple log entries. Without them, debugging multi-step failures in App Insights is impractical. Serilog's `LogContext.PushProperty` is the correct thread-safe mechanism for adding per-request context to all log messages in that request's execution path.

---

## 9. Upload Speed Proxy — `navigator.connection.downlink`

**Decision**: The spec requires a "slow upload speed" check before round 1. The browser does not expose a direct upload speed measurement API. `navigator.connection.downlink` (Network Information API) provides an estimated **downlink** speed in Mbps and is used as a proxy. Threshold: `< 1.0 Mbps` → set JPEG quality to 0.60; `>= 1.0 Mbps` → use 0.85.

**Rationale**: Upload and download speeds are strongly correlated on residential and mobile connections. The `navigator.connection.downlink` value is available synchronously before the first capture and requires no round-trip. On browsers that do not implement the Network Information API (`navigator.connection` is undefined), the code MUST default to quality 0.85 (no adaptation). This is the same pattern used by adaptive streaming libraries (e.g., hls.js bandwidth estimation fallback).

**Alternatives considered**: A pre-game upload speed test (sending a small blob to `/api/ping` and timing it) was rejected because it adds a visible delay before round 1 and requires a backend endpoint purely for probing.

---

## Summary of All Decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Azure Face SDK | `Detection_03` + `Recognition04`; emotion confidence × 10 → score; head-pose penalty at ±20° |
| 2 | Integration tests | Testcontainers Azurite (`AzuriteBuilder`) + `WebApplicationFactory` + `AddPoTestAuth` |
| 3 | Auth (dev/test) | `AddPoTestAuth` header-bypass; guards: env check only; production always uses `Microsoft.Identity.Web` |
| 4 | Programmatic audio | Web Audio API oscillators: square 880 Hz blip, white noise shutter, C major chord chime |
| 5 | Bitrate adaptation | `navigator.connection.downlink < 1.0 Mbps` → JPEG quality 0.60; else 0.85; 640×480 always fixed |
| 6 | OTel metrics | `PoFace.Api.Metrics` meter; `emotion.intensity.average` gauge + `session.completion.count` counter → Azure Monitor |
| 7 | CSS + Radzen | Global `terminal.css` + Blazor CSS isolation for arena animations; Radzen CSS variable overrides for theme |
| 8 | Correlation ID | `CorrelationIdMiddleware` → `LogContext.PushProperty` → Serilog; surfaced in `ProblemDetails.Extensions` |
| 9 | Upload speed proxy | `navigator.connection.downlink` used as downlink proxy; default 0.85 if API unavailable; no backend probe |
