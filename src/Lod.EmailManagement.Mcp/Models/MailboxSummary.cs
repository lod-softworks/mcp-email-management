namespace Lod.EmailManagement.Mcp.Models;

public record class MailboxSummary(
    string Id,
    string DisplayName,
    string EmailAddress,
    bool IsActive);
