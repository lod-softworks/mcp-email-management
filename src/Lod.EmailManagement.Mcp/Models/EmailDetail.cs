namespace Lod.EmailManagement.Mcp.Models;

public record class EmailDetail(
    string Id,
    string Subject,
    EmailAddress From,
    IReadOnlyList<EmailAddress> To,
    IReadOnlyList<EmailAddress> Cc,
    IReadOnlyList<EmailAddress> Bcc,
    DateTimeOffset Date,
    string TextBody,
    string? HtmlBody,
    bool IsRead,
    bool IsFlagged,
    IReadOnlyList<EmailAttachmentMetadata> Attachments);
