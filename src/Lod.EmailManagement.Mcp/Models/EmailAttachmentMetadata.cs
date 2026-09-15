namespace Lod.EmailManagement.Mcp.Models;

public record class EmailAttachmentMetadata(
    string Id,
    string FileName,
    string ContentType,
    long SizeInBytes);
