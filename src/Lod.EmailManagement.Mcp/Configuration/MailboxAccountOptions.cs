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

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string? TrashFolderName { get; init; }

    public bool IsActive { get; init; } = true;
}
