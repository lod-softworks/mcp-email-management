using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using Microsoft.Extensions.Options;
using MimeKit;
using Lod.EmailManagement.Mcp.Configuration;
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

    Task<EmailAttachmentContent?> GetAttachment(
        string mailboxId,
        string folderId,
        string itemId,
        string attachmentId,
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

    Task<OperationResult> CreateFolder(
        string mailboxId,
        string folderName,
        string? parentFolderId = null,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SetItemReadStatus(
        string mailboxId,
        string folderId,
        string itemId,
        bool isRead,
        CancellationToken cancellationToken = default);

    Task<OperationResult> SetItemFlaggedStatus(
        string mailboxId,
        string folderId,
        string itemId,
        bool isFlagged,
        CancellationToken cancellationToken = default);
}


public class ImapMailboxService(
    IImapClientFactory clientFactory,
    ISmtpClientFactory smtpClientFactory,
    IOptions<EmailSendingOptions> emailSendingOptions,
    IConfiguration configuration,
    ILogger<ImapMailboxService> logger) : IMailboxService
{
    public Task<IReadOnlyList<MailboxSummary>> ListMailboxes(CancellationToken cancellationToken = default)
    {
        List<MailboxAccountOptions> accounts = configuration.GetSection(MailboxAccountOptions.SectionName).Get<List<MailboxAccountOptions>>() ?? [];
        List<MailboxSummary> summaries = accounts.Select(a => new MailboxSummary(
            a.Id,
            a.DisplayName,
            a.EmailAddress,
            a.IsActive)).ToList();

        return Task.FromResult<IReadOnlyList<MailboxSummary>>(summaries);
    }

    public async Task<IReadOnlyList<MailboxFolder>> ListFolders(string mailboxId, CancellationToken cancellationToken = default)
    {
        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        List<MailboxFolder> result = [];

        FolderNamespaceCollection personalNamespaces = client.PersonalNamespaces;
        foreach (FolderNamespace ns in personalNamespaces)
        {
            IMailFolder rootFolder = await client.GetFolderAsync(ns.Path, cancellationToken);
            IEnumerable<IMailFolder> subfolders = await rootFolder.GetSubfoldersAsync(false, cancellationToken);
            foreach (IMailFolder folder in subfolders)
            {
                if (!folder.IsOpen)
                {
                    await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
                }

                result.Add(await MapFolder(folder, cancellationToken));
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<EmailSummary>> ListFolderItems(
        string mailboxId,
        string folderId,
        int limit = 50,
        int offset = 0,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder folder = await client.GetFolderAsync(folderId, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        SearchQuery query = unreadOnly ? SearchQuery.NotSeen : SearchQuery.All;
        IList<UniqueId> matches = await folder.SearchAsync(query, cancellationToken);

        List<UniqueId> pageUids = matches
            .OrderByDescending(u => u.Id)
            .Skip(offset)
            .Take(limit)
            .ToList();

        if (pageUids.Count == 0)
        {
            return [];
        }

        IList<IMessageSummary> summaries = await folder.FetchAsync(
            pageUids,
            MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.Size,
            cancellationToken);

        List<EmailSummary> results = [];
        foreach (IMessageSummary summary in summaries)
        {
            EmailAddress from = summary.Envelope?.From?.Mailboxes.FirstOrDefault() is { } mb
                ? new EmailAddress(mb.Address, mb.Name)
                : new EmailAddress("unknown@unknown", null);

            List<EmailAddress> to = summary.Envelope?.To?.Mailboxes.Select(m => new EmailAddress(m.Address, m.Name)).ToList() ?? [];

            bool isRead = summary.Flags?.HasFlag(MessageFlags.Seen) ?? false;
            bool isFlagged = summary.Flags?.HasFlag(MessageFlags.Flagged) ?? false;
            long size = summary.Size ?? 0;
            DateTimeOffset date = summary.Envelope?.Date ?? DateTimeOffset.UtcNow;

            results.Add(new EmailSummary(
                summary.UniqueId.ToString(),
                summary.Envelope?.Subject ?? "(No Subject)",
                from,
                to,
                date,
                isRead,
                isFlagged,
                size));
        }

        return results;
    }

    public async Task<EmailDetail?> GetItem(
        string mailboxId,
        string folderId,
        string itemId,
        bool includeBodyHtml = true,
        CancellationToken cancellationToken = default)
    {
        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder folder = await client.GetFolderAsync(folderId, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        if (!UniqueId.TryParse(itemId, out UniqueId uid))
        {
            logger.LogWarning("Invalid UniqueId '{ItemId}' requested for folder '{FolderId}' in mailbox '{MailboxId}'", itemId, folderId, mailboxId);
            return null;
        }

        MimeMessage message = await folder.GetMessageAsync(uid, cancellationToken);
        if (message is null)
        {
            return null;
        }

        EmailAddress from = message.From.Mailboxes.FirstOrDefault() is { } sender
            ? new EmailAddress(sender.Address, sender.Name)
            : new EmailAddress("unknown@unknown", null);

        List<EmailAddress> to = message.To.Mailboxes.Select(m => new EmailAddress(m.Address, m.Name)).ToList();
        List<EmailAddress> cc = message.Cc.Mailboxes.Select(m => new EmailAddress(m.Address, m.Name)).ToList();
        List<EmailAddress> bcc = message.Bcc.Mailboxes.Select(m => new EmailAddress(m.Address, m.Name)).ToList();

        List<EmailAttachmentMetadata> attachments = [];
        int attachmentIndex = 0;
        foreach (MimeEntity attachment in message.Attachments)
        {
            string fileName = attachment.ContentDisposition?.FileName
                ?? attachment.ContentType?.Name
                ?? "attachment";
            string contentType = attachment.ContentType?.MimeType ?? "application/octet-stream";
            long attachmentSize = 0;
            if (attachment is MimePart part && part.Content?.Stream is not null)
            {
                attachmentSize = part.Content.Stream.Length;
            }

            string attachmentId = !string.IsNullOrWhiteSpace(attachment.ContentId)
                ? attachment.ContentId.Trim('<', '>')
                : attachmentIndex.ToString();

            attachments.Add(new EmailAttachmentMetadata(
                attachmentId,
                fileName,
                contentType,
                attachmentSize));

            attachmentIndex++;
        }

        // Check flags
        IList<IMessageSummary> summaryList = await folder.FetchAsync([uid], MessageSummaryItems.Flags, cancellationToken);
        bool isRead = summaryList.FirstOrDefault()?.Flags?.HasFlag(MessageFlags.Seen) ?? false;
        bool isFlagged = summaryList.FirstOrDefault()?.Flags?.HasFlag(MessageFlags.Flagged) ?? false;

        return new EmailDetail(
            uid.ToString(),
            message.Subject ?? "(No Subject)",
            from,
            to,
            cc,
            bcc,
            message.Date,
            message.TextBody ?? string.Empty,
            includeBodyHtml ? message.HtmlBody : null,
            isRead,
            isFlagged,
            attachments);
    }

    public async Task<EmailAttachmentContent?> GetAttachment(
        string mailboxId,
        string folderId,
        string itemId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder folder = await client.GetFolderAsync(folderId, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

        if (!UniqueId.TryParse(itemId, out UniqueId uid))
        {
            logger.LogWarning("Invalid UniqueId '{ItemId}' requested for folder '{FolderId}' in mailbox '{MailboxId}'", itemId, folderId, mailboxId);
            return null;
        }

        MimeMessage message = await folder.GetMessageAsync(uid, cancellationToken);
        if (message is null)
        {
            return null;
        }

        MimeEntity? matchedEntity = null;
        string resolvedId = attachmentId;
        List<MimeEntity> attachmentList = message.Attachments.ToList();

        // 1. Try matching by index if attachmentId is an integer
        if (int.TryParse(attachmentId, out int targetIndex) && targetIndex >= 0 && targetIndex < attachmentList.Count)
        {
            matchedEntity = attachmentList[targetIndex];
            resolvedId = targetIndex.ToString();
        }

        // 2. Try matching by ContentId (exact or trimmed of '<' and '>')
        if (matchedEntity is null)
        {
            for (int i = 0; i < attachmentList.Count; i++)
            {
                MimeEntity entity = attachmentList[i];
                if (!string.IsNullOrWhiteSpace(entity.ContentId))
                {
                    string trimmedCid = entity.ContentId.Trim('<', '>');
                    if (string.Equals(entity.ContentId, attachmentId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(trimmedCid, attachmentId, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedEntity = entity;
                        resolvedId = trimmedCid;
                        break;
                    }
                }
            }
        }

        // 3. Try matching by file name
        if (matchedEntity is null)
        {
            for (int i = 0; i < attachmentList.Count; i++)
            {
                MimeEntity entity = attachmentList[i];
                string? fileName = entity.ContentDisposition?.FileName ?? entity.ContentType?.Name;
                if (!string.IsNullOrWhiteSpace(fileName) && string.Equals(fileName, attachmentId, StringComparison.OrdinalIgnoreCase))
                {
                    matchedEntity = entity;
                    resolvedId = !string.IsNullOrWhiteSpace(entity.ContentId) ? entity.ContentId.Trim('<', '>') : i.ToString();
                    break;
                }
            }
        }

        if (matchedEntity is null)
        {
            logger.LogWarning("Attachment '{AttachmentId}' not found for message '{ItemId}' in folder '{FolderId}', mailbox '{MailboxId}'",
                attachmentId, itemId, folderId, mailboxId);
            return null;
        }

        string resolvedFileName = matchedEntity.ContentDisposition?.FileName
            ?? matchedEntity.ContentType?.Name
            ?? "attachment";
        string resolvedContentType = matchedEntity.ContentType?.MimeType ?? "application/octet-stream";

        using MemoryStream memoryStream = new();
        if (matchedEntity is MimePart mimePart)
        {
            if (mimePart.Content is not null)
            {
                await mimePart.Content.DecodeToAsync(memoryStream, cancellationToken);
            }
        }
        else if (matchedEntity is MessagePart messagePart)
        {
            if (messagePart.Message is not null)
            {
                await messagePart.Message.WriteToAsync(memoryStream, cancellationToken);
            }
        }
        else
        {
            await matchedEntity.WriteToAsync(memoryStream, cancellationToken);
        }

        byte[] contentBytes = memoryStream.ToArray();
        string contentBase64 = Convert.ToBase64String(contentBytes);

        return new EmailAttachmentContent(
            resolvedId,
            resolvedFileName,
            resolvedContentType,
            contentBytes.LongLength,
            contentBase64);
    }

    public async Task<OperationResult> MoveItem(
        string mailboxId,
        string sourceFolderId,
        string targetFolderId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder sourceFolder = await client.GetFolderAsync(sourceFolderId, cancellationToken);
        IMailFolder targetFolder = await client.GetFolderAsync(targetFolderId, cancellationToken);

        await sourceFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        if (!UniqueId.TryParse(itemId, out UniqueId uid))
        {
            return new OperationResult(false, $"Invalid item UniqueId '{itemId}'.");
        }

        UniqueId? movedUid = await sourceFolder.MoveToAsync(uid, targetFolder, cancellationToken);
        if (movedUid is not null)
        {
            return new OperationResult(true, $"Item {itemId} successfully moved to folder '{targetFolder.FullName}'.");
        }

        return new OperationResult(false, $"Failed to move item {itemId} to folder '{targetFolder.FullName}'.");
    }

    public async Task<OperationResult> TrashItem(
        string mailboxId,
        string folderId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder? trashFolder = ResolveTrashFolder(client);

        if (trashFolder is null)
        {
            return new OperationResult(false, $"Trash folder could not be located for mailbox '{mailboxId}'.");
        }

        if (string.Equals(folderId, trashFolder.FullName, StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult(false, $"Item {itemId} is already in the Trash folder '{trashFolder.FullName}'.");
        }

        IMailFolder sourceFolder = await client.GetFolderAsync(folderId, cancellationToken);
        await sourceFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        if (!UniqueId.TryParse(itemId, out UniqueId uid))
        {
            return new OperationResult(false, $"Invalid item UniqueId '{itemId}'.");
        }

        UniqueId? movedUid = await sourceFolder.MoveToAsync(uid, trashFolder, cancellationToken);
        if (movedUid is not null)
        {
            return new OperationResult(true, $"Item {itemId} successfully moved to Trash ('{trashFolder.FullName}').");
        }

        return new OperationResult(false, $"Failed to move item {itemId} to Trash ('{trashFolder.FullName}').");
    }

    public async Task<OperationResult> ArchiveItem(
        string mailboxId,
        string folderId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder? archiveFolder = ResolveArchiveFolder(client);

        if (archiveFolder is null)
        {
            return new OperationResult(false, $"Archive folder could not be located for mailbox '{mailboxId}'.");
        }

        if (string.Equals(folderId, archiveFolder.FullName, StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult(false, $"Item {itemId} is already in the Archive folder '{archiveFolder.FullName}'.");
        }

        IMailFolder sourceFolder = await client.GetFolderAsync(folderId, cancellationToken);
        await sourceFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        if (!UniqueId.TryParse(itemId, out UniqueId uid))
        {
            return new OperationResult(false, $"Invalid item UniqueId '{itemId}'.");
        }

        UniqueId? movedUid = await sourceFolder.MoveToAsync(uid, archiveFolder, cancellationToken);
        if (movedUid is not null)
        {
            return new OperationResult(true, $"Item {itemId} successfully moved to Archive ('{archiveFolder.FullName}').");
        }

        return new OperationResult(false, $"Failed to move item {itemId} to Archive ('{archiveFolder.FullName}').");
    }

    public async Task<OperationResult> SendEmail(
        string mailboxId,
        SendEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        bool isEnabled = emailSendingOptions?.Value?.Enabled ?? false;
        if (!isEnabled)
        {
            string recipients = string.Join(", ", request.To);
            logger.LogWarning(
                "Attempted to send email via mailbox '{MailboxId}' to '{Recipients}' with subject '{Subject}', but email sending is disabled by configuration.",
                mailboxId,
                recipients,
                request.Subject);

            throw new InvalidOperationException("Email sending is disabled by configuration.");
        }

        if (request.To is null || request.To.Count == 0 || request.To.All(string.IsNullOrWhiteSpace))
        {
            return new OperationResult(false, "At least one recipient must be specified.");
        }

        if (string.IsNullOrEmpty(request.BodyText) && string.IsNullOrEmpty(request.BodyHtml))
        {
            return new OperationResult(false, "Email body cannot be empty.");
        }

        List<MailboxAccountOptions> accounts = configuration.GetSection(MailboxAccountOptions.SectionName).Get<List<MailboxAccountOptions>>() ?? [];
        MailboxAccountOptions? account = accounts.FirstOrDefault(a => string.Equals(a.Id, mailboxId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Mailbox with ID '{mailboxId}' was not found in configuration.");
        if (!account.IsActive) throw new InvalidOperationException($"Mailbox with ID '{mailboxId}' is inactive in the configuration.");

        MimeMessage message = new();

        string senderName = !string.IsNullOrWhiteSpace(account.DisplayName) ? account.DisplayName : account.EmailAddress;
        message.From.Add(new MailboxAddress(senderName, account.EmailAddress));

        foreach (string to in request.To)
        {
            if (string.IsNullOrWhiteSpace(to)) continue;
            if (!MailboxAddress.TryParse(to, out MailboxAddress? address))
            {
                return new OperationResult(false, $"Invalid recipient email address '{to}'.");
            }
            message.To.Add(address);
        }

        if (request.Cc is not null)
        {
            foreach (string cc in request.Cc)
            {
                if (string.IsNullOrWhiteSpace(cc)) continue;
                if (!MailboxAddress.TryParse(cc, out MailboxAddress? address))
                {
                    return new OperationResult(false, $"Invalid CC recipient email address '{cc}'.");
                }
                message.Cc.Add(address);
            }
        }

        if (request.Bcc is not null)
        {
            foreach (string bcc in request.Bcc)
            {
                if (string.IsNullOrWhiteSpace(bcc)) continue;
                if (!MailboxAddress.TryParse(bcc, out MailboxAddress? address))
                {
                    return new OperationResult(false, $"Invalid BCC recipient email address '{bcc}'.");
                }
                message.Bcc.Add(address);
            }
        }

        message.Subject = request.Subject ?? string.Empty;

        BodyBuilder builder = new()
        {
            TextBody = request.BodyText ?? string.Empty
        };
        if (!string.IsNullOrWhiteSpace(request.BodyHtml))
        {
            builder.HtmlBody = request.BodyHtml;
        }
        message.Body = builder.ToMessageBody();

        try
        {
            using ISmtpClient client = await smtpClientFactory.CreateConnectedClient(mailboxId, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation(
                "Successfully sent email from mailbox '{MailboxId}' to '{Recipients}' with subject '{Subject}'.",
                mailboxId,
                string.Join(", ", request.To),
                message.Subject);

            return new OperationResult(true, $"Email successfully sent to {string.Join(", ", request.To)}.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send email via mailbox '{MailboxId}' to '{Recipients}' with subject '{Subject}'.",
                mailboxId,
                string.Join(", ", request.To),
                message.Subject);

            return new OperationResult(false, $"Failed to send email: {ex.Message}");
        }
    }

    public async Task<OperationResult> CreateFolder(
        string mailboxId,
        string folderName,
        string? parentFolderId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return new OperationResult(false, "Folder name cannot be empty.");
        }

        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder? parentFolder;
        if (!string.IsNullOrWhiteSpace(parentFolderId))
        {
            parentFolder = await client.GetFolderAsync(parentFolderId, cancellationToken);
        }
        else
        {
            string rootPath = client.PersonalNamespaces.FirstOrDefault()?.Path ?? string.Empty;
            parentFolder = await client.GetFolderAsync(rootPath, cancellationToken);
        }

        if (parentFolder is null)
        {
            return new OperationResult(false, $"Parent folder could not be found for mailbox '{mailboxId}'.");
        }

        IMailFolder? created = await parentFolder.CreateAsync(folderName, isMessageFolder: true, cancellationToken);
        if (created is null)
        {
            return new OperationResult(false, $"Failed to create folder '{folderName}'.");
        }

        return new OperationResult(true, $"Folder '{created.FullName}' successfully created.");
    }

    public async Task<OperationResult> SetItemReadStatus(
        string mailboxId,
        string folderId,
        string itemId,
        bool isRead,
        CancellationToken cancellationToken = default)
    {
        if (!UniqueId.TryParse(itemId, out UniqueId uid))
        {
            return new OperationResult(false, $"Invalid item UniqueId '{itemId}'.");
        }

        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder folder = await client.GetFolderAsync(folderId, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        if (isRead)
        {
            await folder.AddFlagsAsync(uid, MessageFlags.Seen, silent: true, cancellationToken);
            return new OperationResult(true, $"Item {itemId} marked as read.");
        }

        await folder.RemoveFlagsAsync(uid, MessageFlags.Seen, silent: true, cancellationToken);
        return new OperationResult(true, $"Item {itemId} marked as unread.");
    }

    public async Task<OperationResult> SetItemFlaggedStatus(
        string mailboxId,
        string folderId,
        string itemId,
        bool isFlagged,
        CancellationToken cancellationToken = default)
    {
        if (!UniqueId.TryParse(itemId, out UniqueId uid))
        {
            return new OperationResult(false, $"Invalid item UniqueId '{itemId}'.");
        }

        using IImapClient client = await clientFactory.CreateConnectedClient(mailboxId, cancellationToken);
        IMailFolder folder = await client.GetFolderAsync(folderId, cancellationToken);
        await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        if (isFlagged)
        {
            await folder.AddFlagsAsync(uid, MessageFlags.Flagged, silent: true, cancellationToken);
            return new OperationResult(true, $"Item {itemId} marked as flagged.");
        }

        await folder.RemoveFlagsAsync(uid, MessageFlags.Flagged, silent: true, cancellationToken);
        return new OperationResult(true, $"Item {itemId} marked as unflagged.");
    }

    private static IMailFolder? ResolveTrashFolder(IImapClient client)
    {
        try
        {
            IMailFolder? specialTrash = client.GetFolder(SpecialFolder.Trash);
            if (specialTrash is not null)
            {
                return specialTrash;
            }
        }
        catch
        {
            // Fall back to searching personal folders
        }

        foreach (FolderNamespace ns in client.PersonalNamespaces)
        {
            IMailFolder? rootFolder = client.GetFolder(ns.Path);
            if (rootFolder is not null)
            {
                IMailFolder? found = FindTrashRecursive(rootFolder);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static IMailFolder? FindTrashRecursive(IMailFolder folder)
    {
        if (folder.Attributes.HasFlag(FolderAttributes.Trash) ||
            string.Equals(folder.Name, "Trash", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(folder.Name, "Deleted Items", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(folder.Name, "Deleted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(folder.Name, "Bin", StringComparison.OrdinalIgnoreCase))
        {
            return folder;
        }

        foreach (IMailFolder sub in folder.GetSubfolders(false))
        {
            IMailFolder? match = FindTrashRecursive(sub);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static IMailFolder? ResolveArchiveFolder(IImapClient client)
    {
        try
        {
            IMailFolder? specialArchive = client.GetFolder(SpecialFolder.Archive);
            if (specialArchive is not null)
            {
                return specialArchive;
            }
        }
        catch
        {
            // Fall back to searching personal folders
        }

        foreach (FolderNamespace ns in client.PersonalNamespaces)
        {
            IMailFolder? rootFolder = client.GetFolder(ns.Path);
            if (rootFolder is not null)
            {
                IMailFolder? found = FindArchiveRecursive(rootFolder);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static IMailFolder? FindArchiveRecursive(IMailFolder folder)
    {
        if (folder.Attributes.HasFlag(FolderAttributes.Archive) ||
            string.Equals(folder.Name, "Archive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(folder.Name, "Archives", StringComparison.OrdinalIgnoreCase))
        {
            return folder;
        }

        foreach (IMailFolder sub in folder.GetSubfolders(false))
        {
            IMailFolder? match = FindArchiveRecursive(sub);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static async Task<MailboxFolder> MapFolder(IMailFolder folder, CancellationToken cancellationToken)
    {
        string? specialRole = null;
        if (folder.Attributes.HasFlag(FolderAttributes.Inbox))
        {
            specialRole = "Inbox";
        }
        else if (folder.Attributes.HasFlag(FolderAttributes.Trash))
        {
            specialRole = "Trash";
        }
        else if (folder.Attributes.HasFlag(FolderAttributes.Sent))
        {
            specialRole = "Sent";
        }
        else if (folder.Attributes.HasFlag(FolderAttributes.Drafts))
        {
            specialRole = "Drafts";
        }
        else if (folder.Attributes.HasFlag(FolderAttributes.Junk))
        {
            specialRole = "Junk";
        }
        else if (folder.Attributes.HasFlag(FolderAttributes.Archive))
        {
            specialRole = "Archive";
        }

        int totalCount = folder.Count;
        int unreadCount = folder.Unread;

        List<MailboxFolder> childFolders = [];
        IEnumerable<IMailFolder> children = await folder.GetSubfoldersAsync(false, cancellationToken);
        foreach (IMailFolder child in children)
        {
            childFolders.Add(await MapFolder(child, cancellationToken));
        }

        return new MailboxFolder(
            folder.FullName,
            folder.Name,
            folder.FullName,
            specialRole,
            totalCount,
            unreadCount,
            childFolders);
    }
}
