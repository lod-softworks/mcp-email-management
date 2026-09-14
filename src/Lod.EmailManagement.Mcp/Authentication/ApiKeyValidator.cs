using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Lod.EmailManagement.Mcp.Authentication;

public class ApiKeyValidator(
    ISecretService secretService,
    IConfiguration configuration,
    IOptions<ClientApiKeyOptions> options,
    IMemoryCache memoryCache,
    ILogger<ApiKeyValidator> logger) : IApiKeyValidator
{
    private const string CacheKey = "Lod_Valid_Api_Keys";

    public async Task<bool> ValidateKey(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        HashSet<string> validKeys = await memoryCache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes > 0 ? options.Value.CacheDurationMinutes : 5);
            return await LoadValidKeys(cancellationToken);
        }) ?? [];

        byte[] providedKeyBytes = Encoding.UTF8.GetBytes(apiKey);

        foreach (string validKey in validKeys)
        {
            byte[] validKeyBytes = Encoding.UTF8.GetBytes(validKey);
            if (CryptographicOperations.FixedTimeEquals(providedKeyBytes, validKeyBytes))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<HashSet<string>> LoadValidKeys(CancellationToken cancellationToken)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);

        // 1. Check Azure Key Vault secret 'ApiKeys'
        try
        {
            string? secretValue = await secretService.GetSecret("ApiKeys", cancellationToken);
            if (!string.IsNullOrWhiteSpace(secretValue))
            {
                string trimmed = secretValue.Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    List<string>? deserialized = JsonSerializer.Deserialize<List<string>>(trimmed);
                    if (deserialized is not null)
                    {
                        foreach (string key in deserialized)
                        {
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                keys.Add(key.Trim());
                            }
                        }
                    }
                }
                else
                {
                    string[] split = trimmed.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
                    foreach (string key in split)
                    {
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            keys.Add(key.Trim());
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load 'ApiKeys' secret from Key Vault.");
        }

        // 2. Check indexed Key Vault secrets (e.g. ApiKeys--0, ApiKeys--1 or ApiKeys-0)
        int index = 0;
        while (index < 50)
        {
            try
            {
                string? indexedSecret = await secretService.GetSecret($"ApiKeys--{index}", cancellationToken);
                if (string.IsNullOrWhiteSpace(indexedSecret))
                {
                    break;
                }

                keys.Add(indexedSecret.Trim());
                index++;
            }
            catch
            {
                break;
            }
        }

        // 3. Fallback / merge with local configuration (e.g. appsettings.json for local dev)
        List<string>? configKeys = configuration.GetSection("ApiKeys").Get<List<string>>();
        if (configKeys is not null)
        {
            foreach (string key in configKeys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    keys.Add(key.Trim());
                }
            }
        }

        if (options.Value.Keys.Count > 0)
        {
            foreach (string key in options.Value.Keys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    keys.Add(key.Trim());
                }
            }
        }

        logger.LogInformation("Loaded {Count} authorized API key(s) into memory cache.", keys.Count);
        return keys;
    }
}
