namespace Lod.EmailManagement.Mcp.Configuration;

public record class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    public string? VaultUri { get; init; }
}
