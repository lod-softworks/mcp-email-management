using Lod.EmailManagement.Mcp.Models;

namespace Lod.EmailManagement.Mcp.Services;

public interface IMailboxService
{
    Task<IReadOnlyList<MailboxSummary>> ListMailboxes(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MailboxFolder>> ListFolders(string mailboxId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailSummary>> ListFolderItems(
        string mailboxId,
        string folderId,
        int limit = 50,
        int offset = 0,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<EmailDetail?> GetItem(
        string mailboxId,
        string folderId,
        string itemId,
        bool includeBodyHtml = true,
        CancellationToken cancellationToken = default);

    Task<OperationResult> MoveItem(
        string mailboxId,
        string sourceFolderId,
        string targetFolderId,
        string itemId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> TrashItem(
        string mailboxId,
        string folderId,
        string itemId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ArchiveItem(
        string mailboxId,
        string folderId,
        string itemId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SendEmail(
        string mailboxId,
        SendEmailRequest request,
        CancellationToken cancellationToken = default);
}
