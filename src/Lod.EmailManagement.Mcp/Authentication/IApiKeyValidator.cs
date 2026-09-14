namespace Lod.EmailManagement.Mcp.Authentication;

public interface IApiKeyValidator
{
    Task<bool> ValidateKey(string apiKey, CancellationToken cancellationToken = default);
}
