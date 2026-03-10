using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PoFace.IntegrationTests.Infrastructure;

namespace PoFace.IntegrationTests.Diagnostics;

public sealed class DiagnosticsEndpointTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    public DiagnosticsEndpointTests(AzuriteFixture azurite) => _azurite = azurite;

    [Fact]
    public async Task GetDiag_Authenticated_Returns200WithServiceStatuses()
    {
        await using var factory = new PoFaceWebAppFactory(_azurite.ConnectionString);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "diag-user");
        client.DefaultRequestHeaders.Add("X-Test-Display-Name", "Diag User");

        var response = await client.GetAsync("/api/diag");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var services = body.GetProperty("services");

        services.GetProperty("faceApi").GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        services.GetProperty("blobStorage").GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        services.GetProperty("tableStorage").GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetDiag_WithoutAuth_Returns401()
    {
        await using var factory = new PoFaceWebAppFactory(_azurite.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/diag");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
