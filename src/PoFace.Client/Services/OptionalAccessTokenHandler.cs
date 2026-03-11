using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace PoFace.Client.Services;

/// <summary>
/// Adds a bearer token when one is available, but never blocks anonymous requests.
/// This keeps gameplay endpoints usable for anonymous users while still supporting
/// authenticated API calls when the user is signed in.
/// </summary>
public sealed class OptionalAccessTokenHandler : DelegatingHandler
{
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly string[] _scopes;

    public OptionalAccessTokenHandler(IAccessTokenProvider tokenProvider, string[] scopes)
    {
        _tokenProvider = tokenProvider;
        _scopes = scopes;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var tokenResult = _scopes.Length == 0
            ? await _tokenProvider.RequestAccessToken()
            : await _tokenProvider.RequestAccessToken(new AccessTokenRequestOptions { Scopes = _scopes });

        if (tokenResult.TryGetToken(out var token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        return await base.SendAsync(request, cancellationToken);
    }
}
