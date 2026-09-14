using FluentAssertions;
using Lod.EmailManagement.Mcp.Mcp;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lod.EmailManagement.Mcp.Tests;

public class McpSessionManagerTests
{
    [Fact]
    public async Task SendMessageReturnsFalseWhenSessionDoesNotExist()
    {
        McpSessionManager manager = new(NullLogger<McpSessionManager>.Instance);

        bool sent = await manager.SendMessage("non-existent-session-id", "test message");

        sent.Should().BeFalse();
    }

    [Fact]
    public void HasSessionReturnsFalseForUnknownSession()
    {
        McpSessionManager manager = new(NullLogger<McpSessionManager>.Instance);

        bool exists = manager.HasSession("unknown");

        exists.Should().BeFalse();
    }
}
