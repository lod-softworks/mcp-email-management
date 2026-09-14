using MailKit.Net.Imap;
using MailKit.Security;
using Lod.EmailManagement.Mcp.Configuration;

namespace Lod.EmailManagement.Mcp.Services;

public class ImapClientFactory(
    IConfiguration configuration,
    ISecretService secretService,
    ILogger<ImapClientFactory> logger) : IImapClientFactory
{
    public async Task<IImapClient> CreateConnectedClient(string mailboxId, CancellationToken cancellationToken = default)
    {
        List<MailboxAccountOptions> accounts = configuration.GetSection(MailboxAccountOptions.SectionName).Get<List<MailboxAccountOptions>>() ?? [];
        MailboxAccountOptions? account = accounts.FirstOrDefault(a => string.Equals(a.Id, mailboxId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Mailbox with ID '{mailboxId}' was not found in configuration.");

        string imapHost = account.ImapHost;
        string? secretHost = await secretService.GetSecret($"Mailboxes--{mailboxId}--ImapHost", cancellationToken);
        if (!string.IsNullOrWhiteSpace(secretHost))
        {
            imapHost = secretHost;
        }

        string username = account.Username;
        string? secretUsername = await secretService.GetSecret($"Mailboxes--{mailboxId}--Username", cancellationToken);
        if (!string.IsNullOrWhiteSpace(secretUsername))
        {
            username = secretUsername;
        }

        string password = account.Password;
        string? secretPassword = await secretService.GetSecret($"Mailboxes--{mailboxId}--Password", cancellationToken);
        if (!string.IsNullOrWhiteSpace(secretPassword))
        {
            password = secretPassword;
        }

        SecureSocketOptions sslOptions = account.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

        logger.LogInformation("Connecting to IMAP server {Host}:{Port} for mailbox {MailboxId}...", imapHost, account.ImapPort, mailboxId);

        ImapClient client = new();
        try
        {
            await client.ConnectAsync(imapHost, account.ImapPort, sslOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            return client;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect or authenticate to IMAP server for mailbox {MailboxId}", mailboxId);
            client.Dispose();
            throw;
        }
    }
}
