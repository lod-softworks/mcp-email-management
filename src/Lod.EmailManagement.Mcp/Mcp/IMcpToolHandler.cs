using System.Text.Json;

namespace Lod.EmailManagement.Mcp.Mcp;

public interface IMcpToolHandler
{
    IReadOnlyList<McpTool> GetTools();

    Task<McpToolCallResult> CallTool(string toolName, JsonElement? arguments, CancellationToken cancellationToken = default);

    Task<McpResponse?> ProcessRequest(McpRequest request, CancellationToken cancellationToken = default);
}
