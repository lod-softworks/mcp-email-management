using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

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
    }
}
