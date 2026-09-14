using FluentAssertions;
using Lod.EmailManagement.Mcp.Controllers;
using Lod.EmailManagement.Mcp.Models;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Lod.EmailManagement.Mcp.Tests;

public class MailboxesControllerTests
{
    private readonly Mock<IMailboxService> _mailboxServiceMock;
    private readonly MailboxesController _controller;

    public MailboxesControllerTests()
    {
        _mailboxServiceMock = new Mock<IMailboxService>();
        _controller = new MailboxesController(_mailboxServiceMock.Object);
    }

    [Fact]
    public async Task GetMailboxesReturnsOkWithData()
    {
        List<MailboxSummary> sampleList = [new("main", "Main Mailbox", "main@example.com", true)];
        _mailboxServiceMock.Setup(s => s.ListMailboxes(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleList);

        ActionResult<IReadOnlyList<MailboxSummary>> result = await _controller.GetMailboxes(CancellationToken.None);

        OkObjectResult? okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeEquivalentTo(sampleList);
    }

    [Fact]
    public async Task GetFoldersReturnsOkWhenFound()
    {
        List<MailboxFolder> sampleFolders = [new("INBOX", "INBOX", "INBOX", "Inbox", 15, 3, [])];
        _mailboxServiceMock.Setup(s => s.ListFolders("main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleFolders);

        ActionResult<IReadOnlyList<MailboxFolder>> result = await _controller.GetFolders("main", CancellationToken.None);

        OkObjectResult? okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeEquivalentTo(sampleFolders);
    }

    [Fact]
    public async Task GetFoldersReturnsNotFoundOnKeyNotFoundException()
    {
        _mailboxServiceMock.Setup(s => s.ListFolders("invalid", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Not found"));

        ActionResult<IReadOnlyList<MailboxFolder>> result = await _controller.GetFolders("invalid", CancellationToken.None);

        NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task GetItemReturnsNotFoundWhenNull()
    {
        _mailboxServiceMock.Setup(s => s.GetItem("main", "INBOX", "999", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailDetail?)null);

        ActionResult<EmailDetail> result = await _controller.GetItem("main", "INBOX", "999", true, CancellationToken.None);

        NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
    }

    [Fact]
    public async Task MoveItemReturnsOkWhenSuccessful()
    {
        _mailboxServiceMock.Setup(s => s.MoveItem("main", "INBOX", "Archive", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult(true, "Moved"));

        ActionResult<OperationResult> result = await _controller.MoveItem("main", "INBOX", "1", new MoveItemRequest("Archive"), CancellationToken.None);

        OkObjectResult? okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        OperationResult? op = okResult!.Value as OperationResult;
        op!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TrashItemReturnsBadRequestWhenUnsuccessful()
    {
        _mailboxServiceMock.Setup(s => s.TrashItem("main", "Trash", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult(false, "Already in trash"));

        ActionResult<OperationResult> result = await _controller.TrashItem("main", "Trash", "1", CancellationToken.None);

        BadRequestObjectResult? badResult = result.Result as BadRequestObjectResult;
        badResult.Should().NotBeNull();
        OperationResult? op = badResult!.Value as OperationResult;
        op!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteItemDelegatesToTrashItem()
    {
        _mailboxServiceMock.Setup(s => s.TrashItem("main", "INBOX", "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult(true, "Moved to trash"));

        ActionResult<OperationResult> result = await _controller.DeleteItem("main", "INBOX", "1", CancellationToken.None);

        OkObjectResult? okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        OperationResult? op = okResult!.Value as OperationResult;
        op!.Success.Should().BeTrue();
    }
}
