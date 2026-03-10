namespace PoFace.Api.Infrastructure.Configuration;

public static class PoFaceConfiguration
{
    public static string? GetKeyVaultUri(this IConfiguration configuration)
        => FirstNonEmpty(
            configuration["KeyVault:Uri"],
            configuration["KeyVault:VaultUri"]);

    public static string? GetAppInsightsConnectionString(this IConfiguration configuration)
        => FirstNonEmpty(
            configuration["ApplicationInsights:ConnectionString"],
            configuration["ApplicationInsightsConnectionString"],
            Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING"));

    public static string GetRequiredStorageConnectionString(
        this IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = FirstNonEmpty(configuration["StorageConnectionString"]);
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        if (environment.IsDevelopment() ||
            string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
            return "UseDevelopmentStorage=true";

        throw new InvalidOperationException(
            "StorageConnectionString must be configured via Azure Key Vault or environment variables.");
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}