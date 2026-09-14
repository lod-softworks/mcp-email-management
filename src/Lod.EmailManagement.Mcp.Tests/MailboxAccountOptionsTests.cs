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
        mailbox.SmtpUsername.Should().Be("agent-smtp");
    }

    [Fact]
    public void MailboxAccountOptionsDefaultsSmtpPortAndSsl()
    {
        MailboxAccountOptions options = new();

        options.SmtpPort.Should().Be(587);
        options.SmtpUseSsl.Should().BeTrue();
        options.SmtpHost.Should().BeEmpty();
        options.SmtpUsername.Should().BeNull();
    }
}
