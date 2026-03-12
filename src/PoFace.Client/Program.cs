using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using PoFace.Client;
using PoFace.Client.Services;
using PoFace.Client.Shared;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
var apiScope   = builder.Configuration["ApiScope"] ?? string.Empty;
var hasAzureAd = !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientId"]);

if (hasAzureAd)
{
    builder.Services.AddScoped(sp =>
    {
        var provider = sp.GetRequiredService<Microsoft.AspNetCore.Components.WebAssembly.Authentication.IAccessTokenProvider>();
        var authHandler = new OptionalAccessTokenHandler(
            provider,
            string.IsNullOrWhiteSpace(apiScope) ? [] : [apiScope])
        {
            InnerHandler = new HttpClientHandler()
        };

        return new HttpClient(authHandler) { BaseAddress = new Uri(apiBaseUrl) };
    });

    builder.Services.AddMsalAuthentication(options =>
    {
        builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
        if (!string.IsNullOrWhiteSpace(apiScope))
            options.ProviderOptions.DefaultAccessTokenScopes.Add(apiScope);
    });
}
else
{
    builder.Services.AddAuthorizationCore();
    builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
}

// ── Radzen UI components ──────────────────────────────────────────────────────
builder.Services.AddRadzenComponents();

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ApiClient>(sp => new ApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<AudioService>();
builder.Services.AddScoped<GameOrchestrator>();
builder.Services.AddScoped<IRadzenThemeBridge, RadzenThemeBridge>();
builder.Services.AddScoped<PoFace.Client.Services.ThemeService>();

await builder.Build().RunAsync();
