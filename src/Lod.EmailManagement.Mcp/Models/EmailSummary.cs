namespace Lod.EmailManagement.Mcp.Models;

public record class EmailSummary(
    string Id,
    string Subject,
    EmailAddress From,
    IReadOnlyList<EmailAddress> To,
    DateTimeOffset Date,
    bool IsRead,
    bool IsFlagged,
    long SizeInBytes);
