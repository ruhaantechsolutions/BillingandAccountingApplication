using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RBiz.Application.Sync.Dtos;
using RBiz.Application.Sync.Interfaces;

namespace RBiz.WebAPI.Controllers;

/// <summary>
/// Offline synchronisation for the PWA client.
///
/// The usual client loop is: GET status to confirm it is really online, GET changes
/// repeatedly until hasMore is false, then POST push to drain its outbox.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sync")]
public sealed class SyncController : ApiControllerBase
{
    private readonly ISyncService _syncService;

    public SyncController(ISyncService syncService) => _syncService = syncService;

    /// <summary>
    /// Returns everything in this tenant that changed after the given cursor.
    /// </summary>
    /// <remarks>
    /// Omit the cursor for a first full download. Store the cursor returned for each
    /// entity and send it back next time; keep calling while hasMore is true.
    ///
    /// Deleted rows arrive as changeType "Delete" with no payload - apply them, or the
    /// local cache will keep rows that no longer exist.
    /// </remarks>
    /// <param name="cursor">Opaque position from the previous pull. Omit to start fresh.</param>
    /// <param name="entities">Optional entity names to limit the pull to, e.g. users.</param>
    /// <param name="limit">Rows per entity. Capped by the server's configured maximum.</param>
    /// <param name="cancellationToken">Cancelled automatically if the client disconnects.</param>
    /// <response code="200">The delta, with a cursor per entity.</response>
    /// <response code="400">The cursor or an entity name was not valid.</response>
    [HttpGet("changes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SyncPullResponse))]
    public async Task<ActionResult<SyncPullResponse>> GetChanges(
        [FromQuery] string? cursor,
        [FromQuery] string[]? entities,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var response = await _syncService.PullAsync(cursor, entities, limit, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Uploads a batch of changes made while the device was offline.
    /// </summary>
    /// <remarks>
    /// Every item needs an operationId that stays the same across retries - that is what
    /// stops a retried batch from being applied twice.
    ///
    /// Send the baseRowVersion the device last saw for an existing row. If it no longer
    /// matches the server, that item comes back as a Conflict with the server's current
    /// row attached, and nothing is overwritten. A per-item failure does not fail the
    /// batch, so the rest of the outbox still drains.
    /// </remarks>
    /// <response code="200">Every item was processed; inspect each result's status.</response>
    /// <response code="400">The batch itself was malformed or too large.</response>
    [HttpPost("push")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SyncPushResponse))]
    public async Task<ActionResult<SyncPushResponse>> Push(
        [FromBody] SyncPushRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _syncService.PushAsync(request, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Server time, the synchronisable entity names and the current sync limits.
    /// </summary>
    /// <remarks>
    /// Cheap by design, so a PWA can call it to tell a real connection from
    /// navigator.onLine merely reporting that a network interface exists. Also lets a
    /// device measure its clock offset against the server before queueing work.
    /// </remarks>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SyncStatusResponse))]
    public ActionResult<SyncStatusResponse> GetStatus() => Ok(_syncService.GetStatus());
}
