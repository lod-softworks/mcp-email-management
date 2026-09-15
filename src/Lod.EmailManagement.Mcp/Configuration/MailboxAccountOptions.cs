namespace Lod.EmailManagement.Mcp.Configuration;

public record class MailboxAccountOptions
{
    public const string SectionName = "Mailboxes";

    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string EmailAddress { get; init; } = string.Empty;

    public string ImapHost { get; init; } = string.Empty;

    public int ImapPort { get; init; } = 993;

    public bool ImapUseSsl { get; init; } = true;

    public string ImapUserName { get; init; } = string.Empty;

    public string SmtpHost { get; init; } = string.Empty;

    public int SmtpPort { get; init; } = 587;

    public bool SmtpUseSsl { get; init; } = true;

    public string? SmtpUserName { get; init; }

    public bool IsActive { get; init; } = true;
}
