using System.Text.Json;
using FluentAssertions;
using Lod.EmailManagement.Mcp.Mcp;
using Lod.EmailManagement.Mcp.Models;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lod.EmailManagement.Mcp.Tests;

public class McpProtocolTests
{
    private readonly Mock<IMailboxService> _mailboxServiceMock;
    private readonly EmailMcpToolHandler _handler;

    public McpProtocolTests()
    {
        _mailboxServiceMock = new Mock<IMailboxService>();
        _handler = new EmailMcpToolHandler(_mailboxServiceMock.Object, NullLogger<EmailMcpToolHandler>.Instance);
    }

    [Fact]
    public async Task InitializeReturnsExpectedProtocolVersion()
    {
        McpRequest request = new("2.0", JsonDocument.Parse("1").RootElement, "initialize", null);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        response!.Error.Should().BeNull();
        McpInitializeResult? result = response.Result as McpInitializeResult;
        result.Should().NotBeNull();
        result!.ProtocolVersion.Should().Be("2024-11-05");
        result.ServerInfo.Name.Should().Be("email-management-mcp");
    }

    [Fact]
    public async Task InitializedNotificationReturnsNullResponse()
    {
        McpRequest request = new("2.0", null, "notifications/initialized", null);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().BeNull();
    }

    [Fact]
    public async Task PingReturnsSuccessResponse()
    {
        McpRequest request = new("2.0", JsonDocument.Parse("2").RootElement, "ping", null);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        response!.Error.Should().BeNull();
    }

    [Fact]
    public async Task ToolsListReturnsAllExpectedTools()
    {
        McpRequest request = new("2.0", JsonDocument.Parse("3").RootElement, "tools/list", null);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        McpToolsListResult? result = response!.Result as McpToolsListResult;
        result.Should().NotBeNull();
        result!.Tools.Should().HaveCount(8);

        List<string> toolNames = result.Tools.Select(t => t.Name).ToList();
        toolNames.Should().Contain([
            "list_mailboxes",
            "list_folders",
            "list_folder_items",
            "get_item",
            "move_item",
            "trash_item",
            "archive_item",
            "send_email"
        ]);
    }

    [Fact]
    public async Task CallListMailboxesExecutesService()
    {
        List<MailboxSummary> sampleMailboxes =
        [
            new("work", "Work Mailbox", "work@example.com", true)
        ];
        _mailboxServiceMock.Setup(s => s.ListMailboxes(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleMailboxes);

        string jsonParams = """{"name": "list_mailboxes", "arguments": {}}""";
        McpRequest request = new("2.0", JsonDocument.Parse("4").RootElement, "tools/call", JsonDocument.Parse(jsonParams).RootElement);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        McpToolCallResult? result = response!.Result as McpToolCallResult;
        result.Should().NotBeNull();
        result!.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("work@example.com");
    }

    [Fact]
    public async Task CallListFoldersExecutesService()
    {
        List<MailboxFolder> sampleFolders =
        [
            new("INBOX", "INBOX", "INBOX", "Inbox", 10, 2, [])
        ];
        _mailboxServiceMock.Setup(s => s.ListFolders("work", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleFolders);

        string jsonParams = """{"name": "list_folders", "arguments": {"mailbox_id": "work"}}""";
        McpRequest request = new("2.0", JsonDocument.Parse("5").RootElement, "tools/call", JsonDocument.Parse(jsonParams).RootElement);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        McpToolCallResult? result = response!.Result as McpToolCallResult;
        result.Should().NotBeNull();
        result!.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("INBOX");
    }

    [Fact]
    public async Task CallGetItemExecutesService()
    {
        EmailDetail detail = new(
            "100",
            "Test Subject",
            new("sender@test.com", "Sender"),
            [new("recipient@test.com", "Recipient")],
            [],
            [],
            DateTimeOffset.UtcNow,
            "Plain body content",
            "<p>HTML body</p>",
            true,
            false,
            []);

        _mailboxServiceMock.Setup(s => s.GetItem("work", "INBOX", "100", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        string jsonParams = """{"name": "get_item", "arguments": {"mailbox_id": "work", "folder_id": "INBOX", "item_id": "100", "include_body_html": true}}""";
        McpRequest request = new("2.0", JsonDocument.Parse("6").RootElement, "tools/call", JsonDocument.Parse(jsonParams).RootElement);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        McpToolCallResult? result = response!.Result as McpToolCallResult;
        result.Should().NotBeNull();
        result!.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("Plain body content");
    }

    [Fact]
    public async Task CallTrashItemExecutesService()
    {
        _mailboxServiceMock.Setup(s => s.TrashItem("work", "INBOX", "100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult(true, "Moved to Trash."));

        string jsonParams = """{"name": "trash_item", "arguments": {"mailbox_id": "work", "folder_id": "INBOX", "item_id": "100"}}""";
        McpRequest request = new("2.0", JsonDocument.Parse("7").RootElement, "tools/call", JsonDocument.Parse(jsonParams).RootElement);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        McpToolCallResult? result = response!.Result as McpToolCallResult;
        result.Should().NotBeNull();
        result!.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("Moved to Trash.");
    }

    [Fact]
    public async Task CallArchiveItemExecutesService()
    {
        _mailboxServiceMock.Setup(s => s.ArchiveItem("work", "INBOX", "100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationResult(true, "Moved to Archive."));

        string jsonParams = """{"name": "archive_item", "arguments": {"mailbox_id": "work", "folder_id": "INBOX", "item_id": "100"}}""";
        McpRequest request = new("2.0", JsonDocument.Parse("71").RootElement, "tools/call", JsonDocument.Parse(jsonParams).RootElement);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        McpToolCallResult? result = response!.Result as McpToolCallResult;
        result.Should().NotBeNull();
        result!.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("Moved to Archive.");
    }

    [Fact]
    public async Task UnknownToolReturnsErrorResult()
    {
        string jsonParams = """{"name": "unsupported_tool", "arguments": {}}""";
        McpRequest request = new("2.0", JsonDocument.Parse("8").RootElement, "tools/call", JsonDocument.Parse(jsonParams).RootElement);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        McpToolCallResult? result = response!.Result as McpToolCallResult;
        result.Should().NotBeNull();
        result!.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task CallSendEmailThrowsNotImplementedException()
    {
        _mailboxServiceMock.Setup(s => s.SendEmail("work", It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotImplementedException("Sending email is not implemented."));

        string jsonParams = """{"name": "send_email", "arguments": {"mailbox_id": "work", "to": ["target@example.com"], "subject": "Hello", "body_text": "World"}}""";
        McpRequest request = new("2.0", JsonDocument.Parse("10").RootElement, "tools/call", JsonDocument.Parse(jsonParams).RootElement);

        Func<Task> act = async () => await _handler.ProcessRequest(request);

        await act.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    public async Task UnknownMethodReturnsRpcError()
    {
        McpRequest request = new("2.0", JsonDocument.Parse("9").RootElement, "non_existent_method", null);

        McpResponse? response = await _handler.ProcessRequest(request);

        response.Should().NotBeNull();
        response!.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(-32601);
    }
}
