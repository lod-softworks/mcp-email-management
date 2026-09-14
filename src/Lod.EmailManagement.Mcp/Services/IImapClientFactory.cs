using MailKit.Net.Imap;

namespace Lod.EmailManagement.Mcp.Services;

public interface IImapClientFactory
{
    Task<IImapClient> CreateConnectedClient(string mailboxId, CancellationToken cancellationToken = default);
}
