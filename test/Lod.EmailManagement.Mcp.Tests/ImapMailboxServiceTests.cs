using FluentAssertions;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Models;
using Lod.EmailManagement.Mcp.Services;

namespace Lod.EmailManagement.Mcp.Tests;

public class ImapMailboxServiceTests
{
    private readonly Mock<IImapClientFactory> _clientFactoryMock = new();
    private readonly Mock<ISmtpClientFactory> _smtpClientFactoryMock = new();
    private readonly Mock<ILogger<ImapMailboxService>> _loggerMock = new();

    private IConfiguration CreateConfiguration(Dictionary<string, string?>? additionalSettings = null)
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Mailboxes:0:Id"] = "work",
            ["Mailboxes:0:DisplayName"] = "Work Email",
            ["Mailboxes:0:EmailAddress"] = "work@example.com",
            ["Mailboxes:0:SmtpHost"] = "smtp.example.com",
            ["Mailboxes:0:IsActive"] = "true",
            ["Passwords:work"] = "secret-pass"
        };

        if (additionalSettings is not null)
        {
            foreach (KeyValuePair<string, string?> kvp in additionalSettings)
            {
                inMemorySettings[kvp.Key] = kvp.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task SendEmailLogsWarningAndThrowsWhenDisabled()
    {
        IConfiguration config = CreateConfiguration();
        ImapMailboxService service = new(
            _clientFactoryMock.Object,
            _smtpClientFactoryMock.Object,
            Options.Create(new EmailSendingOptions { Enabled = false }),
            config,
            _loggerMock.Object);

        SendEmailRequest request = new(
            ["recipient@example.com"],
            "Important Update",
            "This is the body");

        Func<Task> act = async () => await service.SendEmail("work", request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*disabled by configuration*");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled by configuration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailReturnsFailureWhenRecipientListIsEmpty()
    {
        IConfiguration config = CreateConfiguration();
        ImapMailboxService service = new(
            _clientFactoryMock.Object,
            _smtpClientFactoryMock.Object,
            Options.Create(new EmailSendingOptions { Enabled = true }),
            config,
            _loggerMock.Object);

        SendEmailRequest request = new([], "Subject", "Body");

        OperationResult result = await service.SendEmail("work", request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("At least one recipient must be specified");
    }

    [Fact]
    public async Task SendEmailReturnsFailureWhenBodyIsEmpty()
    {
        IConfiguration config = CreateConfiguration();
        ImapMailboxService service = new(
            _clientFactoryMock.Object,
            _smtpClientFactoryMock.Object,
            Options.Create(new EmailSendingOptions { Enabled = true }),
            config,
            _loggerMock.Object);

        SendEmailRequest request = new(["recipient@example.com"], "Subject", "", null);

        OperationResult result = await service.SendEmail("work", request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Email body cannot be empty");
    }

    [Fact]
    public async Task SendEmailReturnsFailureWhenRecipientAddressIsInvalid()
    {
        IConfiguration config = CreateConfiguration();
        ImapMailboxService service = new(
            _clientFactoryMock.Object,
            _smtpClientFactoryMock.Object,
            Options.Create(new EmailSendingOptions { Enabled = true }),
            config,
            _loggerMock.Object);

        SendEmailRequest request = new(["not a valid email @@"], "Subject", "Body");

        OperationResult result = await service.SendEmail("work", request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid recipient email address");
    }

    [Fact]
    public async Task SendEmailSendsSuccessfullyWhenEnabled()
    {
        IConfiguration config = CreateConfiguration();
        Mock<ISmtpClient> smtpClientMock = new();
        MimeMessage? sentMessage = null;

        smtpClientMock.Setup(c => c.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null))
            .Callback<MimeMessage, CancellationToken, MailKit.ITransferProgress?>((msg, _, _) => sentMessage = msg)
            .ReturnsAsync("OK");

        _smtpClientFactoryMock.Setup(f => f.CreateConnectedClient("work", It.IsAny<CancellationToken>()))
            .ReturnsAsync(smtpClientMock.Object);

        ImapMailboxService service = new(
            _clientFactoryMock.Object,
            _smtpClientFactoryMock.Object,
            Options.Create(new EmailSendingOptions { Enabled = true }),
            config,
            _loggerMock.Object);

        SendEmailRequest request = new(
            ["recipient@example.com"],
            "Hello World",
            "This is plain text.",
            "<p>This is HTML.</p>",
            ["cc@example.com"],
            ["bcc@example.com"]);

        OperationResult result = await service.SendEmail("work", request);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Email successfully sent to recipient@example.com");

        sentMessage.Should().NotBeNull();
        sentMessage!.Subject.Should().Be("Hello World");
        sentMessage.TextBody.Should().Be("This is plain text.");
        sentMessage.HtmlBody.Should().Be("<p>This is HTML.</p>");
        sentMessage.From.Mailboxes.First().Address.Should().Be("work@example.com");
        sentMessage.To.Mailboxes.First().Address.Should().Be("recipient@example.com");
        sentMessage.Cc.Mailboxes.First().Address.Should().Be("cc@example.com");
        sentMessage.Bcc.Mailboxes.First().Address.Should().Be("bcc@example.com");

        smtpClientMock.Verify(c => c.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null), Times.Once);
        smtpClientMock.Verify(c => c.DisconnectAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendEmailReturnsFailureWhenSmtpSendFails()
    {
        IConfiguration config = CreateConfiguration();
        Mock<ISmtpClient> smtpClientMock = new();

        smtpClientMock.Setup(c => c.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null))
            .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "Mailbox unavailable"));

        _smtpClientFactoryMock.Setup(f => f.CreateConnectedClient("work", It.IsAny<CancellationToken>()))
            .ReturnsAsync(smtpClientMock.Object);

        ImapMailboxService service = new(
            _clientFactoryMock.Object,
            _smtpClientFactoryMock.Object,
            Options.Create(new EmailSendingOptions { Enabled = true }),
            config,
            _loggerMock.Object);

        SendEmailRequest request = new(
            ["recipient@example.com"],
            "Hello World",
            "This is plain text.");

        OperationResult result = await service.SendEmail("work", request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to send email");
    }

    private ImapMailboxService CreateService(IConfiguration? config = null, bool emailSendingEnabled = false)
    {
        return new ImapMailboxService(
            _clientFactoryMock.Object,
            _smtpClientFactoryMock.Object,
            Options.Create(new EmailSendingOptions { Enabled = emailSendingEnabled }),
            config ?? CreateConfiguration(),
            _loggerMock.Object);
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

        ImapMailboxService service = CreateService(config);

        IReadOnlyList<MailboxSummary> mailboxes = await service.ListMailboxes();

        mailboxes.Should().HaveCount(2);
        mailboxes[0].Should().BeEquivalentTo(new MailboxSummary("work", "Work Email", "work@example.com", true));
        mailboxes[1].Should().BeEquivalentTo(new MailboxSummary("personal", "Personal Email", "personal@example.com", false));
    }

    [Fact]
    public async Task ListMailboxesReturnsEmptyListWhenNoMailboxesConfigured()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ImapMailboxService service = CreateService(config);

        IReadOnlyList<MailboxSummary> mailboxes = await service.ListMailboxes();

        mailboxes.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFolderReturnsFailureWhenFolderNameIsEmpty()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ImapMailboxService service = CreateService(config);

        OperationResult result = await service.CreateFolder("work", "   ");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Folder name cannot be empty");
    }

    [Fact]
    public async Task SetItemReadStatusReturnsFailureWhenItemIdIsInvalid()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ImapMailboxService service = CreateService(config);

        OperationResult result = await service.SetItemReadStatus("work", "INBOX", "not-a-valid-uid", true);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid item UniqueId");
    }

    [Fact]
    public async Task SetItemFlaggedStatusReturnsFailureWhenItemIdIsInvalid()
    {
        IConfiguration config = new ConfigurationBuilder().Build();
        ImapMailboxService service = CreateService(config);

        OperationResult result = await service.SetItemFlaggedStatus("work", "INBOX", "not-a-valid-uid", true);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid item UniqueId");
    }
}
