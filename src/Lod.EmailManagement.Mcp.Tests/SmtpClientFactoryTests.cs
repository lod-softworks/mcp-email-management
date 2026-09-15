using FluentAssertions;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lod.EmailManagement.Mcp.Tests;

public class SmtpClientFactoryTests
{
    [Fact]
    public async Task CreateConnectedClientThrowsKeyNotFoundExceptionWhenMailboxNotFound()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        SmtpClientFactory factory = new(config, NullLogger<SmtpClientFactory>.Instance);

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
            ["Mailboxes:0:SmtpHost"] = "smtp.example.com",
            ["Mailboxes:0:IsActive"] = "false"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        SmtpClientFactory factory = new(config, NullLogger<SmtpClientFactory>.Instance);

        Func<Task> act = async () => await factory.CreateConnectedClient("inactive-box");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task CreateConnectedClientThrowsInvalidOperationExceptionWhenSmtpHostMissing()
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Mailboxes:0:Id"] = "no-smtp-host",
            ["Mailboxes:0:DisplayName"] = "No SMTP Host Mailbox",
            ["Mailboxes:0:EmailAddress"] = "test@example.com",
            ["Mailboxes:0:IsActive"] = "true"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        SmtpClientFactory factory = new(config, NullLogger<SmtpClientFactory>.Instance);

        Func<Task> act = async () => await factory.CreateConnectedClient("no-smtp-host");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SMTP host is not configured*");
    }
}
