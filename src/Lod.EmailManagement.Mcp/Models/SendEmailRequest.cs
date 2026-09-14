namespace Lod.EmailManagement.Mcp.Models;

public record class SendEmailRequest(
    IReadOnlyList<string> To,
    string Subject,
    string BodyText,
    string? BodyHtml = null,
    IReadOnlyList<string>? Cc = null,
    IReadOnlyList<string>? Bcc = null);
