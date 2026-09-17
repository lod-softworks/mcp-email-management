namespace Lod.EmailManagement.Mcp.Models;

public record class EmailAttachmentContent(
    string Id,
    string FileName,
    string ContentType,
    long SizeInBytes,
    string ContentBase64);
