using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using PoFace.Api.Infrastructure.Configuration;

namespace PoFace.Api.Infrastructure.KeyVault;

public static class KeyVaultSecretLoader
{
    // Only load secrets that belong to PoFace, to avoid fetching ~100 unrelated
    // secrets from the shared Key Vault on every cold start.
    internal const string SecretPrefix = "PoFace--";

    public static void AddPoFaceKeyVault(
        this ConfigurationManager configuration,
        IHostEnvironment environment)
    {
        var vaultUri = configuration.GetKeyVaultUri();
        if (string.IsNullOrWhiteSpace(vaultUri))
            return; // Not configured — local dev without Key Vault

        if ((environment.IsDevelopment() ||
             string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase)) &&
            !string.Equals(Environment.GetEnvironmentVariable("POFACE_ENABLE_KEYVAULT"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Skipping Azure Key Vault bootstrap for local environment. Set POFACE_ENABLE_KEYVAULT=true to enable it.");
            return;
        }

        try
        {
            Console.WriteLine($"[KeyVault] Bootstrapping from {vaultUri} (prefix filter: {SecretPrefix})");
            // In Production on Azure App Service, use ManagedIdentityCredential directly
            // to avoid DefaultAzureCredential exhausting the full credential chain which
            // can hang for minutes on cold start (EnvironmentCredential, WorkloadIdentity, etc.)
            TokenCredential credential = environment.IsProduction() || environment.IsStaging()
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new DefaultAzureCredential();

            // Use a short network timeout and no retries so a transient IMDS or KV
            // hiccup on cold start does not block the app indefinitely.
            var clientOptions = new SecretClientOptions
            {
                Retry =
                {
                    MaxRetries = 2,
                    NetworkTimeout = TimeSpan.FromSeconds(10),
                }
            };
            var secretClient = new SecretClient(new Uri(vaultUri), credential, clientOptions);

            configuration.AddAzureKeyVault(secretClient, new PoFaceKeyVaultSecretManager());
            Console.WriteLine("[KeyVault] Bootstrap complete.");
        }
        catch (Exception ex) when (environment.IsDevelopment() ||
                                   string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Skipping Azure Key Vault bootstrap for local environment: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[KeyVault] FATAL: Failed to load Key Vault secrets from {vaultUri}: {ex}");
            throw;
        }
    }
}

/// <summary>
/// Filters Key Vault secrets to the "PoFace--" prefix and strips it when
/// mapping secret names to configuration keys, so that
/// "PoFace--StorageConnectionString" → "StorageConnectionString".
/// </summary>
internal sealed class PoFaceKeyVaultSecretManager : KeyVaultSecretManager
{
    public override bool Load(SecretProperties secret)
        => secret.Name.StartsWith(KeyVaultSecretLoader.SecretPrefix, StringComparison.OrdinalIgnoreCase);

    public override string GetKey(KeyVaultSecret secret)
        => secret.Name[KeyVaultSecretLoader.SecretPrefix.Length..]
                  .Replace("--", ConfigurationPath.KeyDelimiter);
}

