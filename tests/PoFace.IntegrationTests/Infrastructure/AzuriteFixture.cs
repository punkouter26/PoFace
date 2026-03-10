using DotNet.Testcontainers.Builders;
using Testcontainers.Azurite;

namespace PoFace.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a local Azurite (Azure Storage emulator) container for integration tests.
/// Implements IAsyncLifetime so xUnit starts it before any test in the class and
/// disposes it cleanly afterwards.
/// </summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder()
        .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
