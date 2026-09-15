using MailKit.Net.Smtp;

namespace Lod.EmailManagement.Mcp.Services;

public interface ISmtpClientFactory
{
    Task<ISmtpClient> CreateConnectedClient(string mailboxId, CancellationToken cancellationToken = default);
}
