namespace Lod.EmailManagement.Mcp.Services;

public interface ISecretService
{
    Task<string?> GetSecret(string secretName, CancellationToken cancellationToken = default);
}
