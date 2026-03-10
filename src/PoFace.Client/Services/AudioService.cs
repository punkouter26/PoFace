using Microsoft.JSInterop;

namespace PoFace.Client.Services;

/// <summary>
/// C# wrapper over <c>window.audioInterop</c> (audio.js).
/// All calls are fire-and-forget with swallowed JSException so the game
/// continues even when the browser blocks audio (e.g. autoplay policy).
///
/// Full implementation lives in Phase 7 (T068-T069).
/// This stub wires up the correct JS function names for Phase 3 use.
/// </summary>
public sealed class AudioService
{
    private readonly IJSRuntime _js;

    public AudioService(IJSRuntime js) => _js = js;

    /// <summary>Plays the countdown blip for the given countdown number (3, 2, or 1).</summary>
    public async Task PlayBlipAsync(int countdownNumber)
    {
        try
        {
            await _js.InvokeVoidAsync("audioInterop.playBlip", countdownNumber);
        }
        catch (JSException) { /* audio failures MUST NOT crash the game */ }
    }

    /// <summary>Plays the shutter capture sound.</summary>
    public async Task PlayShutterAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("audioInterop.playShutter");
        }
        catch (JSException) { }
    }

    /// <summary>Plays the success chime when score &gt;= 8.</summary>
    public async Task PlaySuccessChimeAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("audioInterop.playSuccessChime");
        }
        catch (JSException) { }
    }

    /// <summary>Triggers device vibration with the given pattern in milliseconds.</summary>
    public async Task VibrateDeviceAsync(int[] pattern)
    {
        try
        {
            await _js.InvokeVoidAsync("audioInterop.vibrateDevice", pattern);
        }
        catch (JSException) { }
    }
}
