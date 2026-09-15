using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace Lod.EmailManagement.Mcp.Authentication;

public interface IApiKeyValidator
{
    Task<bool> ValidateKey(string apiKey, CancellationToken cancellationToken = default);
}

public class ApiKeyValidator(IConfiguration configuration) : IApiKeyValidator
{
    public async Task<bool> ValidateKey(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        IEnumerable<string> validKeys = configuration.GetSection("Authentication:ApiKeys").Get<IEnumerable<string>>() ?? [];

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
}
