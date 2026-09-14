using Lod.EmailManagement.Mcp.Models;
using Lod.EmailManagement.Mcp.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lod.EmailManagement.Mcp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MailboxesController(IMailboxService mailboxService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MailboxSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MailboxSummary>>> GetMailboxes(CancellationToken cancellationToken)
    {
        IReadOnlyList<MailboxSummary> mailboxes = await mailboxService.ListMailboxes(cancellationToken);
        return Ok(mailboxes);
    }

    [HttpGet("{mailboxId}/folders")]
    [ProducesResponseType(typeof(IReadOnlyList<MailboxFolder>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MailboxFolder>>> GetFolders(
        string mailboxId,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<MailboxFolder> folders = await mailboxService.ListFolders(mailboxId, cancellationToken);
            return Ok(folders);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{mailboxId}/folders/{folderId}/items")]
    [ProducesResponseType(typeof(IReadOnlyList<EmailSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmailSummary>>> GetFolderItems(
        string mailboxId,
        string folderId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<EmailSummary> items = await mailboxService.ListFolderItems(
                mailboxId,
                folderId,
                limit,
                offset,
                unreadOnly,
                cancellationToken);

            return Ok(items);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{mailboxId}/folders/{folderId}/items/{itemId}")]
    [ProducesResponseType(typeof(EmailDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailDetail>> GetItem(
        string mailboxId,
        string folderId,
        string itemId,
        [FromQuery] bool includeBodyHtml = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EmailDetail? item = await mailboxService.GetItem(
                mailboxId,
                folderId,
                itemId,
                includeBodyHtml,
                cancellationToken);

            if (item is null)
            {
                return NotFound(new { error = $"Item '{itemId}' was not found in folder '{folderId}'." });
            }

            return Ok(item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{mailboxId}/folders/{sourceFolderId}/items/{itemId}/move")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationResult>> MoveItem(
        string mailboxId,
        string sourceFolderId,
        string itemId,
        [FromBody] MoveItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            OperationResult result = await mailboxService.MoveItem(
                mailboxId,
                sourceFolderId,
                request.TargetFolderId,
                itemId,
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{mailboxId}/folders/{folderId}/items/{itemId}/trash")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationResult>> TrashItem(
        string mailboxId,
        string folderId,
        string itemId,
        CancellationToken cancellationToken)
    {
        try
        {
            OperationResult result = await mailboxService.TrashItem(
                mailboxId,
                folderId,
                itemId,
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{mailboxId}/folders/{folderId}/items/{itemId}")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<OperationResult>> DeleteItem(
        string mailboxId,
        string folderId,
        string itemId,
        CancellationToken cancellationToken)
    {
        // Internal policy: Deletes always move to trash
        return TrashItem(mailboxId, folderId, itemId, cancellationToken);
    }
}
