using FluentAssertions;
using Lod.EmailManagement.Mcp.Configuration;
using Microsoft.Extensions.Configuration;

namespace Lod.EmailManagement.Mcp.Tests;

public class MailboxAccountOptionsTests
{
    [Fact]
    public void MailboxAccountOptionsBindsSmtpConfigurationCorrectly()
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Mailboxes:0:Id"] = "primary",
            ["Mailboxes:0:DisplayName"] = "Work Email",
            ["Mailboxes:0:EmailAddress"] = "agent@example.com",
            ["Mailboxes:0:ImapHost"] = "imap.example.com",
            ["Mailboxes:0:ImapPort"] = "993",
            ["Mailboxes:0:ImapUseSsl"] = "true",
            ["Mailboxes:0:SmtpHost"] = "smtp.example.com",
            ["Mailboxes:0:SmtpPort"] = "587",
            ["Mailboxes:0:SmtpUseSsl"] = "true",
            ["Mailboxes:0:SmtpUsername"] = "agent-smtp",
            ["Mailboxes:0:Username"] = "agent@example.com",
            ["Mailboxes:0:IsActive"] = "true"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        List<MailboxAccountOptions> mailboxes = configuration.GetSection(MailboxAccountOptions.SectionName).Get<List<MailboxAccountOptions>>() ?? [];

        mailboxes.Should().HaveCount(1);
        MailboxAccountOptions mailbox = mailboxes[0];
        mailbox.Id.Should().Be("primary");
        mailbox.SmtpHost.Should().Be("smtp.example.com");
        mailbox.SmtpPort.Should().Be(587);
        mailbox.SmtpUseSsl.Should().BeTrue();
        mailbox.SmtpUserName.Should().Be("agent-smtp");
    }

    [Fact]
    public void MailboxAccountOptionsDefaultsSmtpPortAndSsl()
    {
        MailboxAccountOptions options = new();

        options.SmtpPort.Should().Be(587);
        options.SmtpUseSsl.Should().BeTrue();
        options.SmtpHost.Should().BeEmpty();
        options.SmtpUserName.Should().BeNull();
    }

    [Fact]
    public void MailboxAccountOptionsBindsImapConfigurationCorrectly()
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            ["Mailboxes:0:Id"] = "imap-test",
            ["Mailboxes:0:DisplayName"] = "IMAP Account",
            ["Mailboxes:0:EmailAddress"] = "imap@example.com",
            ["Mailboxes:0:ImapHost"] = "mail.example.org",
            ["Mailboxes:0:ImapPort"] = "143",
            ["Mailboxes:0:ImapUseSsl"] = "false",
            ["Mailboxes:0:ImapUserName"] = "imap-user",
            ["Mailboxes:0:IsActive"] = "true"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        List<MailboxAccountOptions> mailboxes = configuration.GetSection(MailboxAccountOptions.SectionName).Get<List<MailboxAccountOptions>>() ?? [];

        mailboxes.Should().HaveCount(1);
        MailboxAccountOptions mailbox = mailboxes[0];
        mailbox.Id.Should().Be("imap-test");
        mailbox.DisplayName.Should().Be("IMAP Account");
        mailbox.EmailAddress.Should().Be("imap@example.com");
        mailbox.ImapHost.Should().Be("mail.example.org");
        mailbox.ImapPort.Should().Be(143);
        mailbox.ImapUseSsl.Should().BeFalse();
        mailbox.ImapUserName.Should().Be("imap-user");
        mailbox.IsActive.Should().BeTrue();
    }

    [Fact]
    public void MailboxAccountOptionsDefaultsImapPortAndSsl()
    {
        MailboxAccountOptions options = new();

        options.ImapPort.Should().Be(993);
        options.ImapUseSsl.Should().BeTrue();
        options.ImapHost.Should().BeEmpty();
        options.ImapUserName.Should().BeEmpty();
        options.IsActive.Should().BeTrue();
    }
}
