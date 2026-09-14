using FluentAssertions;
using Lod.EmailManagement.Mcp.Mcp;
using Lod.EmailManagement.Mcp.Models;
using Lod.EmailManagement.Mcp.Services;
using Moq;

namespace Lod.EmailManagement.Mcp.Tests;

public class EmailMcpToolsTests
{
    private readonly Mock<IMailboxService> _mailboxServiceMock;
    private readonly EmailMcpTools _tools;

    public EmailMcpToolsTests()
    {
        _mailboxServiceMock = new Mock<IMailboxService>();
        _tools = new EmailMcpTools(_mailboxServiceMock.Object);
    }

    [Fact]
    public async Task ListMailboxesCallsService()
    {
        List<MailboxSummary> expected = [new("work", "Work", "work@example.com", true)];
        _mailboxServiceMock.Setup(s => s.ListMailboxes(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        IReadOnlyList<MailboxSummary> result = await _tools.ListMailboxes();

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ListFoldersCallsService()
    {
        List<MailboxFolder> expected = [new("INBOX", "INBOX", "INBOX", "Inbox", 10, 2, [])];
        _mailboxServiceMock.Setup(s => s.ListFolders("work", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        IReadOnlyList<MailboxFolder> result = await _tools.ListFolders("work");

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ListFolderItemsCallsServiceWithParameters()
    {
        List<EmailSummary> expected = [new("1", "Subject", new("a@b.com", null), [], DateTimeOffset.UtcNow, false, false, 100)];
        _mailboxServiceMock.Setup(s => s.ListFolderItems("work", "INBOX", 25, 5, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        IReadOnlyList<EmailSummary> result = await _tools.ListFolderItems("work", "INBOX", 25, 5, true);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetItemCallsService()
    {
        EmailDetail expected = new("1", "Subject", new("a@b.com", null), [], [], [], DateTimeOffset.UtcNow, "body", null, true, false, []);
        _mailboxServiceMock.Setup(s => s.GetItem("work", "INBOX", "1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        EmailDetail? result = await _tools.GetItem("work", "INBOX", "1", true);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task MoveItemCallsService()
    {
        OperationResult expected = new(true, "Item moved");
        _mailboxServiceMock.Setup(s => s.MoveItem("work", "INBOX", "Archive", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        OperationResult result = await _tools.MoveItem("work", "INBOX", "Archive", "1");

        result.Should().Be(expected);
    }

    [Fact]
    public async Task TrashItemCallsService()
    {
        OperationResult expected = new(true, "Item trashed");
        _mailboxServiceMock.Setup(s => s.TrashItem("work", "INBOX", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        OperationResult result = await _tools.TrashItem("work", "INBOX", "1");

        result.Should().Be(expected);
    }

    [Fact]
    public async Task SendEmailCallsServiceAndThrowsNotImplementedException()
    {
        _mailboxServiceMock.Setup(s => s.SendEmail("work", It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotImplementedException("Sending email is not implemented."));

        Func<Task> act = async () => await _tools.SendEmail("work", ["user@example.com"], "Subject", "Body");

        await act.Should().ThrowAsync<NotImplementedException>();
    }
}
