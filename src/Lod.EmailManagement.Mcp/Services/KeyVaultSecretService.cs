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
            // 1. Try sanitized secret name for Key Vault (Key Vault only permits 0-9, a-z, A-Z, -)
            string sanitizedSecretName = SanitizeKeyVaultSecretName(secretName);
            string? secretValue = await TryFetchFromKeyVault(sanitizedSecretName, cancellationToken);
            if (!string.IsNullOrWhiteSpace(secretValue))
            {
                return secretValue;
            }

            // 2. If looking up Passwords--email or Passwords:email, also check without the Passwords prefix
            string? emailKey = null;
            if (secretName.StartsWith("Passwords--", StringComparison.OrdinalIgnoreCase))
            {
                emailKey = secretName["Passwords--".Length..];
            }
            else if (secretName.StartsWith("Passwords:", StringComparison.OrdinalIgnoreCase))
            {
                emailKey = secretName["Passwords:".Length..];
            }

            if (!string.IsNullOrWhiteSpace(emailKey))
            {
                string sanitizedKey = SanitizeKeyVaultSecretName(emailKey);
                secretValue = await TryFetchFromKeyVault(sanitizedKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(secretValue))
                {
                    return secretValue;
                }
            }
        }

        // 3. Local configuration direct lookup
        string? configValue = configuration[secretName];
        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return configValue;
        }

        // 4. Local configuration with colon replacement (e.g. Passwords:email1@domain.com)
        string altConfigName = secretName.Replace("--", ":");
        configValue = configuration[altConfigName];
        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return configValue;
        }

        // 5. Look up in Passwords section dictionary (e.g. Passwords["email1@domain.com"])
        if (secretName.StartsWith("Passwords--", StringComparison.OrdinalIgnoreCase))
        {
            string email = secretName["Passwords--".Length..];
            string? pass = configuration.GetSection("Passwords")[email];
            if (!string.IsNullOrWhiteSpace(pass))
            {
                return pass;
            }
        }
        else if (secretName.StartsWith("Passwords:", StringComparison.OrdinalIgnoreCase))
        {
            string email = secretName["Passwords:".Length..];
            string? pass = configuration.GetSection("Passwords")[email];
            if (!string.IsNullOrWhiteSpace(pass))
            {
                return pass;
            }
        }

        return null;
    }

    private async Task<string?> TryFetchFromKeyVault(string secretName, CancellationToken cancellationToken)
    {
        try
        {
            Azure.Response<KeyVaultSecret> response = await _secretClient!.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return response?.Value?.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve secret '{SecretName}' from Azure Key Vault.", secretName);
            return null;
        }
    }

    internal static string SanitizeKeyVaultSecretName(string name)
    {
        string normalized = name.Replace(":", "--");
        char[] chars = normalized.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars);
    }
}
