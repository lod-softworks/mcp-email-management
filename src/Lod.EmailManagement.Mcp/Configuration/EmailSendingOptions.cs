namespace Lod.EmailManagement.Mcp.Configuration;

public record class EmailSendingOptions
{
    public const string SectionName = "EmailSending";

    public bool Enabled { get; set; }
}
