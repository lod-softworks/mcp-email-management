using System.Net;
using FluentAssertions;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Moq;
using Lod.EmailManagement.Mcp.Configuration;
using Lod.EmailManagement.Mcp.Services;

namespace Lod.EmailManagement.Mcp.Tests;

public class McpEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpEndpointIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task McpEndpointRequiresAuthentication()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpointAllowsAuthorizedRequests()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");

        HttpResponseMessage response = await client.GetAsync("/mcp");

        // The endpoint should authenticate the request (not 401)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpointHandlesInitializeRequest()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string initJson = """
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": { "name": "test-client", "version": "1.0.0" }
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(initJson, System.Text.Encoding.UTF8, "application/json"));
        string content = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Received status {response.StatusCode} with content: {content}");
        content.Should().Contain("protocolVersion");
    }

    [Fact]
    public async Task McpEndpointReturnsToolsListViaOfficialSdk()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string listJson = """
        {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "tools/list",
            "params": {}
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(listJson, System.Text.Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.Should().BeTrue();
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("list_mailboxes");
        content.Should().Contain("list_folders");
        content.Should().Contain("list_folder_items");
        content.Should().Contain("get_item");
        content.Should().Contain("move_item");
        content.Should().Contain("trash_item");
        content.Should().Contain("archive_item");
        content.Should().Contain("send_email");
        content.Should().Contain("create_folder");
        content.Should().Contain("mark_item_read");
        content.Should().Contain("mark_item_unread");
        content.Should().Contain("mark_item_flagged");
        content.Should().Contain("mark_item_unflagged");
    }

    [Fact]
    public async Task McpEndpointRejectsInvalidApiKey()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "invalid-key-9999");

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpEndpointAllowsBearerTokenAuthentication()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string initJson = """
        {
            "jsonrpc": "2.0",
            "id": 10,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": { "name": "test-client", "version": "1.0.0" }
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(initJson, System.Text.Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task McpEndpointAllowsQueryParameterAuthentication()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string initJson = """
        {
            "jsonrpc": "2.0",
            "id": 11,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": { "name": "test-client", "version": "1.0.0" }
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp?apiKey=dev-api-key-12345", new StringContent(initJson, System.Text.Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task McpEndpointAllowsSnakeCaseQueryParameterAuthentication()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string initJson = """
        {
            "jsonrpc": "2.0",
            "id": 12,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": { "name": "test-client", "version": "1.0.0" }
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp?api_key=dev-api-key-12345", new StringContent(initJson, System.Text.Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task McpEndpointHandlesToolsCallListMailboxes()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string callJson = """
        {
            "jsonrpc": "2.0",
            "id": 20,
            "method": "tools/call",
            "params": {
                "name": "list_mailboxes",
                "arguments": {}
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(callJson, System.Text.Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.Should().BeTrue();
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("emailAddress");
        content.Should().Contain("isActive");
        content.Should().Contain("20");
    }

    [Fact]
    public async Task McpEndpointHandlesPingRequest()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string pingJson = """
        {
            "jsonrpc": "2.0",
            "id": 30,
            "method": "ping",
            "params": {}
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(pingJson, System.Text.Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task McpEndpointHandlesToolsCallForUnknownTool()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string callJson = """
        {
            "jsonrpc": "2.0",
            "id": 21,
            "method": "tools/call",
            "params": {
                "name": "nonexistent_tool",
                "arguments": {}
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(callJson, System.Text.Encoding.UTF8, "application/json"));

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("21");
        content.Should().Contain("error");
    }

    [Fact]
    public async Task McpEndpointHandlesToolsCallSendEmailThrowsOrReturnsError()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string callJson = """
        {
            "jsonrpc": "2.0",
            "id": 22,
            "method": "tools/call",
            "params": {
                "name": "send_email",
                "arguments": {
                    "mailboxId": "primary",
                    "to": ["user@example.com"],
                    "subject": "Test",
                    "bodyText": "Body"
                }
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(callJson, System.Text.Encoding.UTF8, "application/json"));

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"isError\":true");
        content.Should().Contain("send_email");
    }

    [Fact]
    public async Task McpEndpointHandlesToolsCallCreateFolderValidation()
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string callJson = """
        {
            "jsonrpc": "2.0",
            "id": 23,
            "method": "tools/call",
            "params": {
                "name": "create_folder",
                "arguments": {
                    "mailboxId": "primary",
                    "folderName": "   "
                }
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(callJson, System.Text.Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.Should().BeTrue();
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Folder name cannot be empty");
    }

    [Fact]
    public async Task McpEndpointHandlesToolsCallSendEmailWhenEnabled()
    {
        Mock<ISmtpClient> smtpClientMock = new();
        smtpClientMock.Setup(c => c.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync("OK");

        Mock<ISmtpClientFactory> smtpFactoryMock = new();
        smtpFactoryMock.Setup(f => f.CreateConnectedClient("primary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(smtpClientMock.Object);

        WebApplicationFactory<Program> customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EmailSending:Enabled"] = "true",
                    ["Mailboxes:0:Id"] = "primary",
                    ["Mailboxes:0:DisplayName"] = "Primary Work Email",
                    ["Mailboxes:0:EmailAddress"] = "agent@example.com",
                    ["Mailboxes:0:SmtpHost"] = "smtp.example.com",
                    ["Mailboxes:0:IsActive"] = "true"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.Configure<EmailSendingOptions>(opt => opt.Enabled = true);
                services.AddSingleton<ISmtpClientFactory>(smtpFactoryMock.Object);
            });
        });

        HttpClient client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", "dev-api-key-12345");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        string callJson = """
        {
            "jsonrpc": "2.0",
            "id": 24,
            "method": "tools/call",
            "params": {
                "name": "send_email",
                "arguments": {
                    "mailboxId": "primary",
                    "to": ["recipient@example.com"],
                    "subject": "Integration Test",
                    "bodyText": "Hello from integration test"
                }
            }
        }
        """;

        HttpResponseMessage response = await client.PostAsync("/mcp", new StringContent(callJson, System.Text.Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.Should().BeTrue();
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Email successfully sent");
        smtpClientMock.Verify(c => c.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>(), null), Times.Once);
    }
}
