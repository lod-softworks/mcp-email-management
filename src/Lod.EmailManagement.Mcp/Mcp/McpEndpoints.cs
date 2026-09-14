using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Lod.EmailManagement.Mcp.Mcp;

public static class McpEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/mcp/sse", async (
            HttpContext context,
            [FromServices] McpSessionManager sessionManager) =>
        {
            await sessionManager.HandleSseConnection(context, context.RequestAborted);
        })
        .WithName("McpSseEndpoint")
        .WithDescription("Server-Sent Events endpoint for MCP protocol connection.")
        .WithTags("MCP");

        endpoints.MapPost("/mcp/messages", async (
            HttpContext context,
            [FromQuery] string? sessionId,
            [FromBody] McpRequest request,
            [FromServices] IMcpToolHandler toolHandler,
            [FromServices] McpSessionManager sessionManager) =>
        {
            McpResponse? response = await toolHandler.ProcessRequest(request, context.RequestAborted);

            // If it was a notification (no response), return 202 or 204
            if (response is null)
            {
                return Results.Accepted();
            }

            string responseJson = JsonSerializer.Serialize(response, JsonOptions);

            // If an SSE session is active, emit via SSE per MCP specification
            if (!string.IsNullOrWhiteSpace(sessionId) && sessionManager.HasSession(sessionId))
            {
                await sessionManager.SendMessage(sessionId, responseJson);
                return Results.Accepted();
            }

            // Otherwise, return JSON response directly over HTTP (dual JSON-RPC support)
            return Results.Content(responseJson, "application/json");
        })
        .WithName("McpMessagesEndpoint")
        .WithDescription("Message endpoint for sending MCP JSON-RPC requests.")
        .WithTags("MCP");

        return endpoints;
    }
}
