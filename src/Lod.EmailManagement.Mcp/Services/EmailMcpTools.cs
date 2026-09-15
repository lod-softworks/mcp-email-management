using System.ComponentModel;
using Lod.EmailManagement.Mcp.Models;
using ModelContextProtocol.Server;

namespace Lod.EmailManagement.Mcp.Services;

[McpServerToolType]
public class EmailMcpTools(IMailboxService mailboxService)
{
    [McpServerTool, Description("Lists all configured mailboxes available in the email management service.")]
    public async Task<IReadOnlyList<MailboxSummary>> ListMailboxes(CancellationToken cancellationToken = default)
    {
        return await mailboxService.ListMailboxes(cancellationToken);
    }

    [McpServerTool, Description("Lists all folders and folder hierarchies for a specified mailbox.")]
    public async Task<IReadOnlyList<MailboxFolder>> ListFolders(
        [Description("The unique identifier of the mailbox.")] string mailboxId,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.ListFolders(mailboxId, cancellationToken);
    }

    [McpServerTool, Description("Lists email summaries in a specified mailbox folder with pagination and optional unread filter.")]
    public async Task<IReadOnlyList<EmailSummary>> ListFolderItems(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("The folder path or ID (e.g. 'INBOX').")] string folderId,
        [Description("Maximum number of items to return (default: 50).")] int limit = 50,
        [Description("Offset/index for pagination (default: 0).")] int offset = 0,
        [Description("Whether to only return unread emails (default: false).")] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.ListFolderItems(mailboxId, folderId, limit, offset, unreadOnly, cancellationToken);
    }

    [McpServerTool, Description("Retrieves full email details including headers, plain text and HTML bodies, and attachment metadata.")]
    public async Task<EmailDetail?> GetItem(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("The folder path or ID.")] string folderId,
        [Description("The email message unique ID.")] string itemId,
        [Description("Whether to include HTML body in response (default: true).")] bool includeBodyHtml = true,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.GetItem(mailboxId, folderId, itemId, includeBodyHtml, cancellationToken);
    }

    [McpServerTool, Description("Moves an email message from a source folder to a target folder.")]
    public async Task<OperationResult> MoveItem(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("Current folder path of the item.")] string sourceFolderId,
        [Description("Destination folder path.")] string targetFolderId,
        [Description("The email message unique ID.")] string itemId,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.MoveItem(mailboxId, sourceFolderId, targetFolderId, itemId, cancellationToken);
    }

    [McpServerTool, Description("Moves an email message to the designated Trash folder for the mailbox.")]
    public async Task<OperationResult> TrashItem(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("Current folder path of the item.")] string folderId,
        [Description("The email message unique ID.")] string itemId,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.TrashItem(mailboxId, folderId, itemId, cancellationToken);
    }

    [McpServerTool, Description("Moves an email message to the designated Archive folder for the mailbox.")]
    public async Task<OperationResult> ArchiveItem(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("Current folder path of the item.")] string folderId,
        [Description("The email message unique ID.")] string itemId,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.ArchiveItem(mailboxId, folderId, itemId, cancellationToken);
    }

    [McpServerTool, Description("Sends an email message from the specified mailbox.")]
    public async Task<OperationResult> SendEmail(
        [Description("The mailbox ID to send from.")] string mailboxId,
        [Description("List of recipient email addresses.")] IReadOnlyList<string> to,
        [Description("The email subject line.")] string subject,
        [Description("The plain text email body content.")] string bodyText,
        [Description("Optional HTML email body content.")] string? bodyHtml = null,
        [Description("Optional list of CC recipient email addresses.")] IReadOnlyList<string>? cc = null,
        [Description("Optional list of BCC recipient email addresses.")] IReadOnlyList<string>? bcc = null,
        CancellationToken cancellationToken = default)
    {
        SendEmailRequest request = new(to, subject, bodyText, bodyHtml, cc, bcc);
        return await mailboxService.SendEmail(mailboxId, request, cancellationToken);
    }

    [McpServerTool, Description("Creates a new folder or directory in the specified mailbox.")]
    public async Task<OperationResult> CreateFolder(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("The name of the new folder to create.")] string folderName,
        [Description("Optional parent folder path or ID. If not specified, the folder is created at the top level.")] string? parentFolderId = null,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.CreateFolder(mailboxId, folderName, parentFolderId, cancellationToken);
    }

    [McpServerTool, Description("Marks an email message as read.")]
    public async Task<OperationResult> MarkItemRead(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("Current folder path of the item.")] string folderId,
        [Description("The email message unique ID.")] string itemId,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.SetItemReadStatus(mailboxId, folderId, itemId, isRead: true, cancellationToken);
    }

    [McpServerTool, Description("Marks an email message as unread.")]
    public async Task<OperationResult> MarkItemUnread(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("Current folder path of the item.")] string folderId,
        [Description("The email message unique ID.")] string itemId,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.SetItemReadStatus(mailboxId, folderId, itemId, isRead: false, cancellationToken);
    }

    [McpServerTool, Description("Marks an email message as flagged (starred/important).")]
    public async Task<OperationResult> MarkItemFlagged(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("Current folder path of the item.")] string folderId,
        [Description("The email message unique ID.")] string itemId,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.SetItemFlaggedStatus(mailboxId, folderId, itemId, isFlagged: true, cancellationToken);
    }

    [McpServerTool, Description("Removes the flagged (starred/important) flag from an email message.")]
    public async Task<OperationResult> MarkItemUnflagged(
        [Description("The mailbox ID.")] string mailboxId,
        [Description("Current folder path of the item.")] string folderId,
        [Description("The email message unique ID.")] string itemId,
        CancellationToken cancellationToken = default)
    {
        return await mailboxService.SetItemFlaggedStatus(mailboxId, folderId, itemId, isFlagged: false, cancellationToken);
    }
}
