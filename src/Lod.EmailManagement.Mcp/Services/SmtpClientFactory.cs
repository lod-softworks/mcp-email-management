using MailKit.Net.Smtp;
using MailKit.Security;
using Lod.EmailManagement.Mcp.Configuration;

namespace Lod.EmailManagement.Mcp.Services;

public class SmtpClientFactory(
    IConfiguration configuration,
    ILogger<SmtpClientFactory> logger) : ISmtpClientFactory
{
    public async Task<ISmtpClient> CreateConnectedClient(string mailboxId, CancellationToken cancellationToken = default)
    {
        List<MailboxAccountOptions> accounts = configuration.GetSection(MailboxAccountOptions.SectionName).Get<List<MailboxAccountOptions>>() ?? [];
        MailboxAccountOptions? account = accounts.FirstOrDefault(a => string.Equals(a.Id, mailboxId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Mailbox with ID '{mailboxId}' was not found in configuration.");
        if (!account.IsActive) throw new InvalidOperationException($"Mailbox with ID '{mailboxId}' is inactive in the configuration.");

        if (string.IsNullOrWhiteSpace(account.SmtpHost))
        {
            throw new InvalidOperationException($"SMTP host is not configured for mailbox '{mailboxId}'.");
        }

        string username = !string.IsNullOrWhiteSpace(account.SmtpUserName)
            ? account.SmtpUserName
            : (!string.IsNullOrWhiteSpace(account.ImapUserName) ? account.ImapUserName : account.EmailAddress);

        string? password = configuration.GetSection("Passwords")[account.Id];

        SecureSocketOptions sslOptions = account.SmtpUseSsl
            ? (account.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto)
            : SecureSocketOptions.None;

        logger.LogInformation("Connecting to SMTP server {Host}:{Port} for mailbox {MailboxId}...", account.SmtpHost, account.SmtpPort, mailboxId);

        SmtpClient client = new();
        try
        {
            await client.ConnectAsync(account.SmtpHost, account.SmtpPort, sslOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            return client;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect or authenticate to SMTP server for mailbox {MailboxId}", mailboxId);
            client.Dispose();
            throw;
        }
    }
}
