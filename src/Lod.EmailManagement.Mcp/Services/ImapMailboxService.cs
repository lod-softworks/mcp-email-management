using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Models;

namespace Lod.EmailManagement.Mcp.Services;

public class ImapMailboxService(
    IImapClientFactory clientFactory,
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

            attachments.Add(new EmailAttachmentMetadata(
                attachment.ContentId ?? Guid.NewGuid().ToString(),
                fileName,
                contentType,
                attachmentSize));
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
        IMailFolder? trashFolder = ResolveTrashFolder(client, mailboxId);

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

    private IMailFolder? ResolveTrashFolder(IImapClient client, string mailboxId)
    {
        List<MailboxAccountOptions> accounts = configuration.GetSection(MailboxAccountOptions.SectionName).Get<List<MailboxAccountOptions>>() ?? [];
        MailboxAccountOptions? account = accounts.FirstOrDefault(a => string.Equals(a.Id, mailboxId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(account?.TrashFolderName))
        {
            try
            {
                IMailFolder customTrash = client.GetFolder(account.TrashFolderName);
                if (customTrash is not null)
                {
                    return customTrash;
                }
            }
            catch
            {
                // Fall back to attribute resolution
            }
        }

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
