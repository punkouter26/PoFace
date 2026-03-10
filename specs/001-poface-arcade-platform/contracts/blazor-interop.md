# Blazor JS Interop Contracts — PoFace Client

**Branch**: `001-poface-arcade-platform`  
**Date**: 2026-03-09  
**JS module**: `wwwroot/js/webcam.js`, `wwwroot/js/audio.js`  
**C# callers**: `Interop/WebcamInterop.cs`, `Services/AudioService.cs`

---

## Webcam Capture (`webcam.js`)

### `initCamera(videoElementId: string): Promise<void>`

**Called by**: `ArenaPage.razor` — `OnAfterRenderAsync` on first render  
**Purpose**: Call `navigator.mediaDevices.getUserMedia({ video: true })` and attach the stream to the `<video>` element with the given ID. Stores the stream reference for cleanup.

**C# call**:
```csharp
await JS.InvokeVoidAsync("webcamInterop.initCamera", "webcam-preview");
```

**Returns**: `Promise<void>` — resolves when the video is playing; rejects if camera permission is denied  
**Blazor error mapping**: Exception message surfaced in UI as "Camera permission denied — please enable your camera and refresh."

---

### `captureFrame(videoElementId: string): Promise<string>`

**Called by**: `ArenaPage.razor` — triggered at the shutter moment  
**Purpose**: Draw the current video frame to a hidden `<canvas>` element (640×480 px), encode as JPEG, return Base64 data URL.

**C# call**:
```csharp
var base64DataUrl = await JS.InvokeAsync<string>("webcamInterop.captureFrame", "webcam-preview");
```

**Returns**: Base64 data URL string `"data:image/jpeg;base64,/9j/4AAQ..."` — caller strips the prefix before sending to the API  
**JPEG quality**: `0.85` normally; `0.60` if `navigator.connection?.downlink < 1.0` — selection is made inside `captureFrame` using `navigator.connection` API

**Implementation note**: The canvas element is created once on `initCamera` and reused across all 5 rounds. `ctx.drawImage(videoElement, 0, 0, 640, 480)` scales/crops from whatever native camera resolution is available.

---

### `releaseCamera(): void`

**Called by**: `ArenaPage.razor` — `IAsyncDisposable.DisposeAsync`  
**Purpose**: Stop all tracks on the stored `MediaStream` to release the camera hardware.

**C# call**:
```csharp
await JS.InvokeVoidAsync("webcamInterop.releaseCamera");
```

---

### `flashShutter(overlayElementId: string): Promise<void>`

**Called by**: `ArenaPage.razor` — after `captureFrame` returns  
**Purpose**: Apply the white-flash CSS animation to the `<div>` with the given ID (the `FrozenFrameOverlay`).

**C# call**:
```csharp
await JS.InvokeVoidAsync("webcamInterop.flashShutter", "frozen-frame-overlay");
```

---

## Web Audio (`audio.js`)

All audio functions share a lazily-initialized `AudioContext`. The context is created on first call (guaranteed to be after a user gesture since the game starts on a button click).

### `playBlip(countdownNumber: number): void`

**Called by**: `AudioService.cs` — each countdown tick (3, 2, 1)  
**Purpose**: Play an 80 ms square-wave oscillator at 880 Hz with linear gain ramp.

**C# call**:
```csharp
await JS.InvokeVoidAsync("audioInterop.playBlip", countdownNumber);
```

---

### `playShutter(): void`

**Called by**: `AudioService.cs` — at the shutter moment  
**Purpose**: Play a 120 ms white-noise burst (buffer of random float32 samples) with rapid gain envelope `0 → 0.5 → 0`.

**C# call**:
```csharp
await JS.InvokeVoidAsync("audioInterop.playShutter");
```

---

### `playSuccessChime(): void`

**Called by**: `AudioService.cs` — when `score >= 8` after score reveal  
**Purpose**: Play a 600 ms harmonic C major chord: three `sine` oscillators at 523 Hz (C5), 659 Hz (E5), 784 Hz (G5). Slow gain decay envelope.

**C# call**:
```csharp
await JS.InvokeVoidAsync("audioInterop.playSuccessChime");
```

---

### `vibrateDevice(pattern: number[]): void`

**Called by**: `AudioService.cs` — countdown ticks and shutter  
**Purpose**: Call `navigator.vibrate(pattern)` for haptic feedback on supported mobile devices. Silently no-ops on unsupported platforms.

**C# call**:
```csharp
await JS.InvokeVoidAsync("audioInterop.vibrateDevice", new[] { 80 }); // countdown
await JS.InvokeVoidAsync("audioInterop.vibrateDevice", new[] { 200 }); // shutter
```

---

## Module Loading

Both JS files are loaded as ES modules. In `wwwroot/index.html`:
```html
<script type="module" src="js/webcam.js"></script>
<script type="module" src="js/audio.js"></script>
```

Each module exports a plain object attached to `window` for DotNet Interop:
```javascript
// webcam.js
window.webcamInterop = { initCamera, captureFrame, releaseCamera, flashShutter };

// audio.js
window.audioInterop = { playBlip, playShutter, playSuccessChime, vibrateDevice };
```

---

## Error Handling

| JS function | Error scenario | Blazor handling |
|---|---|---|
| `initCamera` | `NotAllowedError` — camera permission denied | Show error banner: "Camera access required to play. Enable in browser settings." |
| `initCamera` | `NotFoundError` — no camera device | Show error banner: "No camera detected. Please connect a webcam." |
| `captureFrame` | Video not ready (race condition) | Returns an empty string `""`; Blazor detects empty and retries once after 200 ms delay |
| All audio functions | `AudioContext` creation fails | Catch in C# `InvokeVoidAsync`; swallow silently — game continues without sound |
| `vibrateDevice` | `navigator.vibrate` not a function | Wrapped in `if (navigator.vibrate)` guard in JS |
