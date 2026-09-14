using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lod.EmailManagement.Mcp.Mcp;

public record class McpRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] JsonElement? Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonElement? Params);

public record class McpResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] JsonElement? Id,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] McpError? Error = null)
{
    public static McpResponse Success(JsonElement? id, object? result) =>
        new("2.0", id, result, null);

    public static McpResponse Fail(JsonElement? id, int code, string message, object? data = null) =>
        new("2.0", id, null, new McpError(code, message, data));
}

public record class McpError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] object? Data = null);

public record class McpServerInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);

public record class McpServerCapabilities(
    [property: JsonPropertyName("tools")] object? Tools);

public record class McpInitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] McpServerCapabilities Capabilities,
    [property: JsonPropertyName("serverInfo")] McpServerInfo ServerInfo);

public record class McpTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] McpToolInputSchema InputSchema);

public record class McpToolInputSchema(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("properties")] Dictionary<string, object> Properties,
    [property: JsonPropertyName("required")] List<string>? Required = null);

public record class McpToolCallResult(
    [property: JsonPropertyName("content")] List<McpContentItem> Content,
    [property: JsonPropertyName("isError")] bool IsError = false)
{
    public static McpToolCallResult Text(string text, bool isError = false) =>
        new([new McpContentItem("text", text)], isError);

    public static McpToolCallResult Json(object value) =>
        new([new McpContentItem("text", JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }))], false);
}

public record class McpContentItem(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);

public record class McpToolsListResult(
    [property: JsonPropertyName("tools")] IReadOnlyList<McpTool> Tools);
