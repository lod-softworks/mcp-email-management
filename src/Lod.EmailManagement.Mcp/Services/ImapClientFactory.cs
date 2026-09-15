using MailKit.Net.Imap;
using MailKit.Security;
using Lod.EmailManagement.Mcp.Configuration;
using System.Collections.Concurrent;

namespace Lod.EmailManagement.Mcp.Services;

public interface IImapClientFactory
{
    Task<IImapClient> CreateConnectedClient(string mailboxId, CancellationToken cancellationToken = default);
}

public class ImapClientFactory(
    IConfiguration configuration,
    ILogger<ImapClientFactory> logger)
    : IImapClientFactory, IDisposable
{
    private bool disposedValue;

    private static Timer DisposeTimer { get; } = new(DisposeClients, null, 1000, 30_000);
    private static ConcurrentDictionary<string, (IImapClient Client, DateTime Expiration)> Clients { get; } = [];

    public async Task<IImapClient> CreateConnectedClient(string mailboxId, CancellationToken cancellationToken = default)
    {
        if (Clients.TryGetValue(mailboxId, out (IImapClient Client, DateTime Expiration) value) && value.Expiration > DateTime.Now.AddSeconds(60))
        {
            return value.Client;
        }

        List<MailboxAccountOptions> accounts = configuration.GetSection(MailboxAccountOptions.SectionName).Get<List<MailboxAccountOptions>>() ?? [];
        MailboxAccountOptions? account = accounts.FirstOrDefault(a => string.Equals(a.Id, mailboxId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Mailbox with ID '{mailboxId}' was not found in configuration.");
        if (!account.IsActive) throw new InvalidOperationException($"Mailbox with ID '{mailboxId}' is inactive in the configuration.");

        string username = !string.IsNullOrWhiteSpace(account.ImapUserName) ? account.ImapUserName : account.EmailAddress;
        string? password = configuration.GetSection("Passwords")[account.Id];

        SecureSocketOptions sslOptions = account.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

        logger.LogInformation("Connecting to IMAP server {Host}:{Port} for mailbox {MailboxId}...", account.ImapHost, account.ImapPort, mailboxId);

        ImapClient client = new();
        try
        {
            await client.ConnectAsync(account.ImapHost, account.ImapPort, sslOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            DateTime expiration = DateTime.Now.AddMinutes(5);

            return client;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect or authenticate to IMAP server for mailbox {MailboxId}", mailboxId);
            client.Dispose();
            throw;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                DisposeTimer.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private static void DisposeClients(object? state)
    {
        foreach (var kvp in Clients)
        {
            if (kvp.Value.Expiration <= DateTime.Now)
            {
                kvp.Value.Client.Dispose();
                Clients.TryRemove(kvp.Key, out _);

                break; // Exit the loop to avoid potential collection modification issues
            }
        }
    }
}
