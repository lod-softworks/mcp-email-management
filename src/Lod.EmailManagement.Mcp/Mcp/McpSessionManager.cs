using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Lod.EmailManagement.Mcp.Mcp;

public class McpSessionManager(ILogger<McpSessionManager> logger)
{
    private readonly ConcurrentDictionary<string, Channel<string>> _sessions = new();

    public async Task HandleSseConnection(HttpContext context, CancellationToken cancellationToken)
    {
        string sessionId = Guid.NewGuid().ToString("N");
        Channel<string> channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _sessions.TryAdd(sessionId, channel);
        logger.LogInformation("New MCP SSE session established: {SessionId}", sessionId);

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        try
        {
            // MCP standard: first event emitted is 'endpoint' containing the message POST URI
            string endpointUri = $"/mcp/messages?sessionId={sessionId}";
            await context.Response.WriteAsync($"event: endpoint\r\ndata: {endpointUri}\r\n\r\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                string message = await channel.Reader.ReadAsync(cancellationToken);
                await context.Response.WriteAsync($"event: message\r\ndata: {message}\r\n\r\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("MCP SSE session {SessionId} connection cancelled by client.", sessionId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error in MCP SSE stream for session {SessionId}", sessionId);
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            logger.LogInformation("MCP SSE session {SessionId} terminated.", sessionId);
        }
    }

    public async Task<bool> SendMessage(string sessionId, string message)
    {
        if (_sessions.TryGetValue(sessionId, out Channel<string>? channel))
        {
            await channel.Writer.WriteAsync(message);
            return true;
        }

        return false;
    }

    public bool HasSession(string sessionId) => _sessions.ContainsKey(sessionId);
}
