using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using PoFace.Client;
using PoFace.Client.Services;
using PoFace.Client.Shared;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── MSAL authentication ───────────────────────────────────────────────────────
if (string.Equals(builder.Configuration["AuthMode"], "PoTestAuth", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddAuthorizationCore();
    builder.Services.AddScoped<AuthenticationStateProvider, PoTestAuthStateProvider>();

    builder.Services.AddScoped(sp =>
        new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
}
else
{
    builder.Services.AddMsalAuthentication(options =>
    {
        builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
        var apiScope = builder.Configuration["ApiScope"];
        if (!string.IsNullOrWhiteSpace(apiScope))
            options.ProviderOptions.DefaultAccessTokenScopes.Add(apiScope);
    });

    // HTTP client that attaches the MSAL bearer token to every API request.
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
    var apiScope   = builder.Configuration["ApiScope"] ?? string.Empty;
    builder.Services.AddScoped<ApiClient>(sp =>
    {
        var provider   = sp.GetRequiredService<Microsoft.AspNetCore.Components.WebAssembly.Authentication.IAccessTokenProvider>();
        var navigation = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        var authHandler = new Microsoft.AspNetCore.Components.WebAssembly.Authentication.AuthorizationMessageHandler(provider, navigation)
            .ConfigureHandler(
                authorizedUrls: [apiBaseUrl],
                scopes: string.IsNullOrWhiteSpace(apiScope) ? null : [apiScope]);
        authHandler.InnerHandler = new HttpClientHandler();
        return new ApiClient(new HttpClient(authHandler) { BaseAddress = new Uri(apiBaseUrl) });
    });
}

// ── Radzen UI components ──────────────────────────────────────────────────────
builder.Services.AddRadzenComponents();

// ── Application services ──────────────────────────────────────────────────────
// ApiClient is registered via AddHttpClient<ApiClient> in the MSAL branch above;
// in PoTestAuth mode it's registered here as a plain scoped service.
if (string.Equals(builder.Configuration["AuthMode"], "PoTestAuth", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AudioService>();
builder.Services.AddScoped<GameOrchestrator>();
builder.Services.AddScoped<IRadzenThemeBridge, RadzenThemeBridge>();
builder.Services.AddScoped<PoFace.Client.Shared.ThemeService>();

await builder.Build().RunAsync();
