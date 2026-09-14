using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Lod.EmailManagement.Mcp.Configuration;
using Microsoft.Extensions.Options;

namespace Lod.EmailManagement.Mcp.Services;

public class KeyVaultSecretService(
    IOptions<KeyVaultOptions> keyVaultOptions,
    IConfiguration configuration,
    ILogger<KeyVaultSecretService> logger) : ISecretService
{
    private readonly SecretClient? _secretClient = !string.IsNullOrWhiteSpace(keyVaultOptions.Value.VaultUri)
        ? new(new Uri(keyVaultOptions.Value.VaultUri), new DefaultAzureCredential())
        : null;

    public async Task<string?> GetSecret(string secretName, CancellationToken cancellationToken = default)
    {
        if (_secretClient is not null)
        {
            try
            {
                // Azure Key Vault names use hyphens instead of colons or double underscores
                string sanitizedSecretName = secretName.Replace(":", "-").Replace("--", "-");
                Azure.Response<KeyVaultSecret> response = await _secretClient.GetSecretAsync(sanitizedSecretName, cancellationToken: cancellationToken);
                if (response?.Value?.Value is not null)
                {
                    return response.Value.Value;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to retrieve secret '{SecretName}' from Azure Key Vault. Falling back to local configuration.", secretName);
            }
        }

        string? configValue = configuration[secretName];
        if (configValue is not null)
        {
            return configValue;
        }

        string altConfigName = secretName.Replace("-", ":").Replace("--", ":");
        return configuration[altConfigName];
    }
}
