using FluentAssertions;
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
    public async Task ArchiveItemCallsService()
    {
        OperationResult expected = new(true, "Item archived");
        _mailboxServiceMock.Setup(s => s.ArchiveItem("work", "INBOX", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        OperationResult result = await _tools.ArchiveItem("work", "INBOX", "1");

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

    [Fact]
    public async Task ListFolderItemsUsesDefaultParameters()
    {
        List<EmailSummary> expected = [];
        _mailboxServiceMock.Setup(s => s.ListFolderItems("work", "INBOX", 50, 0, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        IReadOnlyList<EmailSummary> result = await _tools.ListFolderItems("work", "INBOX");

        result.Should().BeEquivalentTo(expected);
        _mailboxServiceMock.Verify(s => s.ListFolderItems("work", "INBOX", 50, 0, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetItemReturnsNullWhenNotFound()
    {
        _mailboxServiceMock.Setup(s => s.GetItem("work", "INBOX", "missing-id", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailDetail?)null);

        EmailDetail? result = await _tools.GetItem("work", "INBOX", "missing-id");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SendEmailPassesAllOptionalParameters()
    {
        SendEmailRequest? capturedRequest = null;
        _mailboxServiceMock.Setup(s => s.SendEmail("work", It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, SendEmailRequest, CancellationToken>((_, req, _) => capturedRequest = req)
            .ReturnsAsync(new OperationResult(true, "Sent"));

        List<string> to = ["to@example.com"];
        List<string> cc = ["cc@example.com"];
        List<string> bcc = ["bcc@example.com"];

        OperationResult result = await _tools.SendEmail("work", to, "Subject", "BodyText", "<b>BodyHtml</b>", cc, bcc);

        result.Success.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.To.Should().BeEquivalentTo(to);
        capturedRequest.Subject.Should().Be("Subject");
        capturedRequest.BodyText.Should().Be("BodyText");
        capturedRequest.BodyHtml.Should().Be("<b>BodyHtml</b>");
        capturedRequest.Cc.Should().BeEquivalentTo(cc);
        capturedRequest.Bcc.Should().BeEquivalentTo(bcc);
    }
}
