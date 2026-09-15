namespace Lod.EmailManagement.Mcp.Models;

public record class MailboxFolder(
    string Id,
    string Name,
    string FullPath,
    string? SpecialRole,
    int TotalCount,
    int UnreadCount,
    IReadOnlyList<MailboxFolder> Children);
