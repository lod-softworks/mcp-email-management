using FluentAssertions;
using Lod.EmailManagement.Mcp.Models;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lod.EmailManagement.Mcp.Tests;

public class ImapMailboxServiceTests
{
    private readonly Mock<IImapClientFactory> _clientFactoryMock = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();
    private readonly Mock<ILogger<ImapMailboxService>> _loggerMock = new();

    [Fact]
    public async Task SendEmailLogsWarningAndThrowsNotImplementedException()
    {
        ImapMailboxService service = new(_clientFactoryMock.Object, _configuration, _loggerMock.Object);

        SendEmailRequest request = new(
            ["recipient@example.com"],
            "Important Update",
            "This is the body");

        Func<Task> act = async () => await service.SendEmail("primary", request);

        await act.Should().ThrowAsync<NotImplementedException>()
            .WithMessage("*Sending email is not implemented*");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempted to send email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ListMailboxesReturnsConfiguredMailboxes()
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Mailboxes:0:Id"] = "work",
            ["Mailboxes:0:DisplayName"] = "Work Email",
            ["Mailboxes:0:EmailAddress"] = "work@example.com",
            ["Mailboxes:0:IsActive"] = "true",
            ["Mailboxes:1:Id"] = "personal",
            ["Mailboxes:1:DisplayName"] = "Personal Email",
            ["Mailboxes:1:EmailAddress"] = "personal@example.com",
            ["Mailboxes:1:IsActive"] = "false"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        ImapMailboxService service = new(_clientFactoryMock.Object, config, _loggerMock.Object);

        IReadOnlyList<MailboxSummary> mailboxes = await service.ListMailboxes();

        mailboxes.Should().HaveCount(2);
        mailboxes[0].Should().BeEquivalentTo(new MailboxSummary("work", "Work Email", "work@example.com", true));
        mailboxes[1].Should().BeEquivalentTo(new MailboxSummary("personal", "Personal Email", "personal@example.com", false));
    }

    [Fact]
    public async Task ListMailboxesReturnsEmptyListWhenNoMailboxesConfigured()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ImapMailboxService service = new(_clientFactoryMock.Object, config, _loggerMock.Object);

        IReadOnlyList<MailboxSummary> mailboxes = await service.ListMailboxes();

        mailboxes.Should().BeEmpty();
    }
}
