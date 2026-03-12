using Microsoft.JSInterop;

namespace PoFace.Client.Services;

public interface IRadzenThemeBridge
{
    void SetTheme(string themeName);
}

public sealed class RadzenThemeBridge : IRadzenThemeBridge
{
    private readonly Radzen.ThemeService _themeService;

    public RadzenThemeBridge(Radzen.ThemeService themeService) => _themeService = themeService;

    public void SetTheme(string themeName) => _themeService.SetTheme(themeName, triggerChange: true);
}

/// <summary>
/// Applies the terminal palette and activates the Radzen material-dark base theme.
/// </summary>
public sealed class ThemeService
{
    private readonly IRadzenThemeBridge _themeBridge;
    private readonly IJSRuntime _js;
    private bool _initialized;

    public ThemeService(IRadzenThemeBridge themeBridge, IJSRuntime js)
    {
        _themeBridge = themeBridge;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _themeBridge.SetTheme("material-dark");
        await ApplyTerminalVariablesAsync();
        _initialized = true;
    }

    public async Task ApplyTerminalVariablesAsync()
    {
        await _js.InvokeVoidAsync("setTerminalVars");
    }
}
