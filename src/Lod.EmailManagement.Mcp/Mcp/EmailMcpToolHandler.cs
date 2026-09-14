using System.Text.Json;
using Lod.EmailManagement.Mcp.Models;
using Lod.EmailManagement.Mcp.Services;

namespace Lod.EmailManagement.Mcp.Mcp;

public class EmailMcpToolHandler(
    IMailboxService mailboxService,
    ILogger<EmailMcpToolHandler> logger) : IMcpToolHandler
{
    private static readonly List<McpTool> Tools =
    [
        new(
            "list_mailboxes",
            "Lists all configured mailboxes available in the email management service.",
            new("object", [], [])),
        new(
            "list_folders",
            "Lists all folders and folder hierarchies for a specified mailbox.",
            new(
                "object",
                new Dictionary<string, object>
                {
                    ["mailbox_id"] = new { type = "string", description = "The unique identifier of the mailbox." }
                },
                ["mailbox_id"])),
        new(
            "list_folder_items",
            "Lists email summaries in a specified mailbox folder with pagination and optional unread filter.",
            new(
                "object",
                new Dictionary<string, object>
                {
                    ["mailbox_id"] = new { type = "string", description = "The mailbox ID." },
                    ["folder_id"] = new { type = "string", description = "The folder path or ID (e.g. 'INBOX')." },
                    ["limit"] = new { type = "integer", description = "Maximum number of items to return (default: 50)." },
                    ["offset"] = new { type = "integer", description = "Offset/index for pagination (default: 0)." },
                    ["unread_only"] = new { type = "boolean", description = "Whether to only return unread emails (default: false)." }
                },
                ["mailbox_id", "folder_id"])),
        new(
            "get_item",
            "Retrieves full email details including headers, plain text and HTML bodies, and attachment metadata.",
            new(
                "object",
                new Dictionary<string, object>
                {
                    ["mailbox_id"] = new { type = "string", description = "The mailbox ID." },
                    ["folder_id"] = new { type = "string", description = "The folder path or ID." },
                    ["item_id"] = new { type = "string", description = "The email message unique ID." },
                    ["include_body_html"] = new { type = "boolean", description = "Whether to include HTML body in response (default: true)." }
                },
                ["mailbox_id", "folder_id", "item_id"])),
        new(
            "move_item",
            "Moves an email message from a source folder to a target folder.",
            new(
                "object",
                new Dictionary<string, object>
                {
                    ["mailbox_id"] = new { type = "string", description = "The mailbox ID." },
                    ["source_folder_id"] = new { type = "string", description = "Current folder path of the item." },
                    ["target_folder_id"] = new { type = "string", description = "Destination folder path." },
                    ["item_id"] = new { type = "string", description = "The email message unique ID." }
                },
                ["mailbox_id", "source_folder_id", "target_folder_id", "item_id"])),
        new(
            "trash_item",
            "Moves an email message to the designated Trash folder for the mailbox.",
            new(
                "object",
                new Dictionary<string, object>
                {
                    ["mailbox_id"] = new { type = "string", description = "The mailbox ID." },
                    ["folder_id"] = new { type = "string", description = "Current folder path of the item." },
                    ["item_id"] = new { type = "string", description = "The email message unique ID." }
                },
                ["mailbox_id", "folder_id", "item_id"]))
    ];

    public IReadOnlyList<McpTool> GetTools() => Tools;

    public async Task<McpResponse?> ProcessRequest(McpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (request.Method)
            {
                case "initialize":
                    McpInitializeResult initResult = new(
                        "2024-11-05",
                        new McpServerCapabilities(new { }),
                        new McpServerInfo("email-management-mcp", "1.0.0"));
                    return McpResponse.Success(request.Id, initResult);

                case "notifications/initialized":
                    // Client notification; no response required
                    return null;

                case "ping":
                    return McpResponse.Success(request.Id, new { });

                case "tools/list":
                    return McpResponse.Success(request.Id, new McpToolsListResult(GetTools()));

                case "tools/call":
                    if (request.Params is not { } paramsElement)
                    {
                        return McpResponse.Fail(request.Id, -32602, "Missing params object for tools/call");
                    }

                    string? toolName = paramsElement.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(toolName))
                    {
                        return McpResponse.Fail(request.Id, -32602, "Missing tool name in tools/call");
                    }

                    JsonElement? arguments = paramsElement.TryGetProperty("arguments", out JsonElement argsEl) ? argsEl : null;
                    McpToolCallResult callResult = await CallTool(toolName, arguments, cancellationToken);
                    return McpResponse.Success(request.Id, callResult);

                default:
                    return McpResponse.Fail(request.Id, -32601, $"Method '{request.Method}' not found");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing MCP request for method '{Method}'", request.Method);
            return McpResponse.Fail(request.Id, -32603, $"Internal error: {ex.Message}");
        }
    }

    public async Task<McpToolCallResult> CallTool(string toolName, JsonElement? arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (toolName)
            {
                case "list_mailboxes":
                {
                    IReadOnlyList<MailboxSummary> mailboxes = await mailboxService.ListMailboxes(cancellationToken);
                    return McpToolCallResult.Json(mailboxes);
                }

                case "list_folders":
                {
                    string mailboxId = GetRequiredString(arguments, "mailbox_id");
                    IReadOnlyList<MailboxFolder> folders = await mailboxService.ListFolders(mailboxId, cancellationToken);
                    return McpToolCallResult.Json(folders);
                }

                case "list_folder_items":
                {
                    string mailboxId = GetRequiredString(arguments, "mailbox_id");
                    string folderId = GetRequiredString(arguments, "folder_id");
                    int limit = GetOptionalInt(arguments, "limit", 50);
                    int offset = GetOptionalInt(arguments, "offset", 0);
                    bool unreadOnly = GetOptionalBool(arguments, "unread_only", false);

                    IReadOnlyList<EmailSummary> items = await mailboxService.ListFolderItems(mailboxId, folderId, limit, offset, unreadOnly, cancellationToken);
                    return McpToolCallResult.Json(items);
                }

                case "get_item":
                {
                    string mailboxId = GetRequiredString(arguments, "mailbox_id");
                    string folderId = GetRequiredString(arguments, "folder_id");
                    string itemId = GetRequiredString(arguments, "item_id");
                    bool includeBodyHtml = GetOptionalBool(arguments, "include_body_html", true);

                    EmailDetail? item = await mailboxService.GetItem(mailboxId, folderId, itemId, includeBodyHtml, cancellationToken);
                    if (item is null)
                    {
                        return McpToolCallResult.Text($"Email item '{itemId}' not found in folder '{folderId}'.", true);
                    }

                    return McpToolCallResult.Json(item);
                }

                case "move_item":
                {
                    string mailboxId = GetRequiredString(arguments, "mailbox_id");
                    string sourceFolderId = GetRequiredString(arguments, "source_folder_id");
                    string targetFolderId = GetRequiredString(arguments, "target_folder_id");
                    string itemId = GetRequiredString(arguments, "item_id");

                    OperationResult result = await mailboxService.MoveItem(mailboxId, sourceFolderId, targetFolderId, itemId, cancellationToken);
                    return McpToolCallResult.Json(result);
                }

                case "trash_item":
                {
                    string mailboxId = GetRequiredString(arguments, "mailbox_id");
                    string folderId = GetRequiredString(arguments, "folder_id");
                    string itemId = GetRequiredString(arguments, "item_id");

                    OperationResult result = await mailboxService.TrashItem(mailboxId, folderId, itemId, cancellationToken);
                    return McpToolCallResult.Json(result);
                }

                default:
                    return McpToolCallResult.Text($"Unknown tool: '{toolName}'", true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute tool '{ToolName}'", toolName);
            return McpToolCallResult.Text($"Error executing '{toolName}': {ex.Message}", true);
        }
    }

    private static string GetRequiredString(JsonElement? element, string propertyName)
    {
        if (element.HasValue && element.Value.TryGetProperty(propertyName, out JsonElement prop) && prop.ValueKind == JsonValueKind.String)
        {
            string? val = prop.GetString();
            if (!string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }

        throw new ArgumentException($"Missing required argument '{propertyName}'.");
    }

    private static int GetOptionalInt(JsonElement? element, string propertyName, int defaultValue)
    {
        if (element.HasValue && element.Value.TryGetProperty(propertyName, out JsonElement prop) && prop.TryGetInt32(out int val))
        {
            return val;
        }

        return defaultValue;
    }

    private static bool GetOptionalBool(JsonElement? element, string propertyName, bool defaultValue)
    {
        if (element.HasValue && element.Value.TryGetProperty(propertyName, out JsonElement prop))
        {
            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }
            if (prop.ValueKind == JsonValueKind.False)
            {
                return false;
            }
        }

        return defaultValue;
    }
}
