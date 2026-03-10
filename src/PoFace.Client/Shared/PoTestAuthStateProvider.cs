using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PoFace.Client.Shared;

public sealed class PoTestAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private Task<AuthenticationState>? _cachedState;

    public PoTestAuthStateProvider(HttpClient httpClient)
        => _httpClient = httpClient;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _cachedState ??= LoadStateAsync();

    private async Task<AuthenticationState> LoadStateAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/auth/me");
            if (response.StatusCode != HttpStatusCode.OK)
                return Anonymous();

            var profile = await response.Content.ReadFromJsonAsync<TestUserProfile>();
            if (profile is null || string.IsNullOrWhiteSpace(profile.UserId))
                return Anonymous();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, profile.UserId),
                new Claim(ClaimTypes.Name, profile.DisplayName ?? profile.UserId),
                new Claim("sub", profile.UserId)
            };

            var identity = new ClaimsIdentity(claims, authenticationType: "PoTestAuth");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Anonymous();
        }
    }

    private static AuthenticationState Anonymous()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private sealed record TestUserProfile(string UserId, string DisplayName);
}