using Microsoft.JSInterop;

namespace PoFace.Client.Services;

/// <summary>
/// State names for the per-round game-loop FSM.
///
/// Transition map (Phase 3):
///   Idle → GetReady → Countdown → Capturing → Analyzing → ScoreReveal → [back to GetReady | GameOver]
/// </summary>
public enum GameState
{
    Idle,
    GetReady,       // "GET READY" banner visible for 2 s
    Countdown,      // 3 → 2 → 1 countdown ticks (1 s each)
    Capturing,      // shutter fires, frame extracted from webcam
    Analyzing,      // frozen frame shown while API call is in flight
    ScoreReveal,    // score displayed for 2 s
    GameOver        // all 5 rounds completed
}

/// <summary>Per-round result stored inside the orchestrator.</summary>
public sealed record RoundResult(
    int     RoundNumber,
    string  TargetEmotion,
    int     Score,
    string  ImageUrl,
    bool    FaceDetected,
    bool    HeadPoseValid,
    double  RawConfidence,
    string  QualityLabel,
    double? HeadPoseYaw,
    double? HeadPosePitch,
    double? HeadPoseRoll,
    // Google Cloud Vision attributes
    double  DetectionConfidence,
    double  LandmarkingConfidence,
    string  HeadwearLikelihood,
    string  JoyLikelihood,
    string  SorrowLikelihood,
    string  AngerLikelihood,
    string  SurpriseLikelihood,
    string  BlurLevel,
    string  ExposureLevel,
    bool    IsNetworkError = false);

/// <summary>
/// Drives the 5-round game state machine for the Arena page.
/// Raises <see cref="StateChanged"/> whenever the state transitions so Blazor
/// components can call <c>StateHasChanged()</c>.
/// </summary>
public sealed class GameOrchestrator : IAsyncDisposable
{
    private readonly ApiClient    _api;
    private readonly AudioService _audio;
    private readonly IJSRuntime   _js;

    public GameState          State         { get; private set; } = GameState.Idle;
    public int                CurrentRound  { get; private set; } = 1;
    public int                CountdownTick { get; private set; } = 3;
    public int                TotalScore    => _results.Sum(r => r.Score);
    public bool               IsPersonalBest { get; private set; }
    public IReadOnlyList<RoundResult> Results => _results;
    public string?            CurrentTargetEmotion => _roundDefs.ElementAtOrDefault(CurrentRound - 1)?.TargetEmotion;
    public string?            SessionId     { get; private set; }

    private readonly List<RoundResult>        _results   = new(5);
    private List<RoundDefinition>             _roundDefs = new(5);

    public event Action? StateChanged;

    public GameOrchestrator(ApiClient api, AudioService audio, IJSRuntime js)
    {
        _api   = api;
        _audio = audio;
        _js    = js;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>Start a fresh game from the server and immediately begin Round 1.</summary>
    public async Task StartGameAsync(CancellationToken ct = default)
    {
        _results.Clear();
        CurrentRound = 1;

        var session  = await _api.StartSessionAsync(ct);
        SessionId    = session.SessionId;
        _roundDefs   = session.Rounds.OrderBy(r => r.RoundNumber).ToList();

        await RunRoundAsync(ct);
    }

    // ── Round state machine ───────────────────────────────────────────────────

    private async Task RunRoundAsync(CancellationToken ct)
    {
        await TransitionAsync(GameState.GetReady);
        await Task.Delay(2_000, ct);

        await TransitionAsync(GameState.Countdown);
        for (var tick = 3; tick >= 1; tick--)
        {
            CountdownTick = tick;
            Notify();
            await Task.Delay(1_000, ct);
        }

        await TransitionAsync(GameState.Capturing);
        await _audio.VibrateDeviceAsync([200]);
        await _audio.PlayShutterAsync();

        // Capture frame from webcam via JS interop.
        var dataUrl = await _js.InvokeAsync<string>("webcamInterop.captureFrame", ct, "webcam-preview");
        await _js.InvokeVoidAsync("webcamInterop.flashShutter", ct, "frozen-frame-overlay");

        var jpegBytes = DataUrlToBytes(dataUrl);

        await TransitionAsync(GameState.Analyzing);

        ScoreRoundResponse roundResult;
        var networkError = false;
        try
        {
            roundResult = await _api.ScoreRoundAsync(SessionId!, CurrentRound, jpegBytes, ct);
        }
        catch
        {
            // Network / server failure → score 0 so the game can finish.
            networkError = true;
            roundResult = new ScoreRoundResponse(CurrentRound, CurrentTargetEmotion ?? "", 0, 0, "Unknown",
                false, null, null, null, false, "", null);
        }

        if (roundResult.Score >= 8)
            await _audio.PlaySuccessChimeAsync();

        _results.Add(new RoundResult(
            CurrentRound,
            roundResult.TargetEmotion,
            roundResult.Score,
            roundResult.ImageUrl,
            roundResult.FaceDetected,
            roundResult.HeadPoseValid,
            roundResult.RawConfidence,
            roundResult.QualityLabel,
            roundResult.HeadPoseYaw,
            roundResult.HeadPosePitch,
            roundResult.HeadPoseRoll,
            roundResult.Diagnostics?.DetectionConfidence   ?? 0,
            roundResult.Diagnostics?.LandmarkingConfidence ?? 0,
            roundResult.Diagnostics?.HeadwearLikelihood    ?? "",
            roundResult.Diagnostics?.JoyLikelihood         ?? "",
            roundResult.Diagnostics?.SorrowLikelihood      ?? "",
            roundResult.Diagnostics?.AngerLikelihood       ?? "",
            roundResult.Diagnostics?.SurpriseLikelihood    ?? "",
            roundResult.Diagnostics?.BlurLevel             ?? "",
            roundResult.Diagnostics?.ExposureLevel         ?? "",
            IsNetworkError: networkError));

        await TransitionAsync(GameState.ScoreReveal);
        await Task.Delay(2_000, ct);

        if (CurrentRound < 5)
        {
            CurrentRound++;
            await RunRoundAsync(ct);
        }
        else
        {
            await FinishGameAsync(ct);
        }
    }

    private async Task FinishGameAsync(CancellationToken ct)
    {
        try
        {
            var result   = await _api.CompleteSessionAsync(SessionId!, ct);
            IsPersonalBest = result.IsPersonalBest;
        }
        catch { /* best-effort — show local total if server fails */ }

        await TransitionAsync(GameState.GameOver);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task TransitionAsync(GameState next)
    {
        State = next;
        Notify();
        await Task.CompletedTask;
    }

    private void Notify() => StateChanged?.Invoke();

    private static byte[] DataUrlToBytes(string dataUrl)
    {
        // Format: "data:image/jpeg;base64,<payload>"
        var commaIdx = dataUrl.IndexOf(',');
        var base64   = commaIdx >= 0 ? dataUrl[(commaIdx + 1)..] : dataUrl;
        return Convert.FromBase64String(base64);
    }

    public ValueTask DisposeAsync()
    {
        // Release the webcam if the component is torn down mid-game.
        return _js.InvokeVoidAsync("webcamInterop.releaseCamera");
    }
}
