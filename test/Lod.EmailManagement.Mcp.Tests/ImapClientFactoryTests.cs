using FluentAssertions;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lod.EmailManagement.Mcp.Tests;

public class ImapClientFactoryTests
{
    [Fact]
    public async Task CreateConnectedClientThrowsKeyNotFoundExceptionWhenMailboxNotFound()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        using ImapClientFactory factory = new(config, NullLogger<ImapClientFactory>.Instance);

        Func<Task> act = async () => await factory.CreateConnectedClient("unknown-mailbox");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*unknown-mailbox*");
    }

    [Fact]
    public async Task CreateConnectedClientThrowsInvalidOperationExceptionWhenMailboxIsInactive()
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Mailboxes:0:Id"] = "inactive-box",
            ["Mailboxes:0:DisplayName"] = "Inactive Mailbox",
            ["Mailboxes:0:EmailAddress"] = "inactive@example.com",
            ["Mailboxes:0:ImapHost"] = "imap.example.com",
            ["Mailboxes:0:IsActive"] = "false"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        using ImapClientFactory factory = new(config, NullLogger<ImapClientFactory>.Instance);

        Func<Task> act = async () => await factory.CreateConnectedClient("inactive-box");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive*");
    }
}
