namespace Lod.EmailManagement.Mcp.Configuration;

public record class ClientApiKeyOptions
{
    public const string SectionName = "Authentication:ApiKey";

    public List<string> Keys { get; init; } = [];

    public int CacheDurationMinutes { get; init; } = 5;
}
