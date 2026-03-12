using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using PoFace.Api.Infrastructure.Configuration;

namespace PoFace.Api.Infrastructure.KeyVault;

public static class KeyVaultSecretLoader
{
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
            Console.WriteLine($"[KeyVault] Bootstrapping from {vaultUri}");
            // In Production on Azure App Service, use ManagedIdentityCredential directly
            // to avoid DefaultAzureCredential exhausting the full credential chain which
            // can hang for minutes on cold start (EnvironmentCredential, WorkloadIdentity, etc.)
            TokenCredential credential = environment.IsProduction() || environment.IsStaging()
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new DefaultAzureCredential();
            configuration.AddAzureKeyVault(new Uri(vaultUri), credential);
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
