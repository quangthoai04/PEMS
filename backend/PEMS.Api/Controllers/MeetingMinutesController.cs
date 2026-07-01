using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.Minutes;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class MeetingMinutesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public MeetingMinutesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewminuteslist")]
        public async Task<IActionResult> ViewMinutesList([FromQuery] PEMS.Application.MeetingMinutes.Queries.ViewMinutesList.ViewMinutesListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchandfilterminutes")]
        public async Task<IActionResult> SearchAndFilterMinutes([FromQuery] PEMS.Application.MeetingMinutes.Queries.SearchAndFilterMinutes.SearchAndFilterMinutesQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{minutesId}/export-pdf")]
        public async Task<IActionResult> ExportPdf(ulong minutesId, CancellationToken cancellationToken)
        {
            var fileBytes = await _mediator.Send(new PEMS.Application.MeetingMinutes.Queries.ExportMinutes.ExportMinutesPdfQuery { MinutesId = minutesId }, cancellationToken);
            return File(fileBytes, "application/pdf", $"minutes-{minutesId}.pdf");
        }

        [HttpGet("{minutesId}/export-excel")]
        public async Task<IActionResult> ExportExcel(ulong minutesId, CancellationToken cancellationToken)
        {
            var fileBytes = await _mediator.Send(new PEMS.Application.MeetingMinutes.Queries.ExportMinutes.ExportMinutesExcelQuery { MinutesId = minutesId }, cancellationToken);
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"minutes-{minutesId}.xlsx");
        }

        // ── Biên bản chuyến thăm (1 bản / visit_instance + cơ chế lock chỉnh sửa) ──
        // Read the single minutes record for a campus instance (with lock state + action flags).
        [HttpGet("visit-instances/{visitInstanceId}")]
        public async Task<IActionResult> GetVisitInstanceMinutes(ulong visitInstanceId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetVisitInstanceMinutesQuery(visitInstanceId), cancellationToken));

        // Open the editor: create-if-missing + acquire the edit lock (returns a lock token).
        [HttpPost("visit-instances/{visitInstanceId}/create-or-lock")]
        public async Task<IActionResult> CreateOrLock(ulong visitInstanceId, [FromBody] CreateOrLockMinutesBody? body, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new CreateOrLockMinutesCommand(visitInstanceId, body?.Title), cancellationToken));

        // Re-acquire the lock on an existing record (re-open for editing).
        [HttpPost("{minutesId}/acquire-lock")]
        public async Task<IActionResult> AcquireLock(ulong minutesId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new AcquireMinutesLockCommand(minutesId), cancellationToken));

        // Save content + attendance snapshot + action items (requires the caller's lock token +
        // matching row_version); releases the lock.
        [HttpPut("{minutesId}")]
        public async Task<IActionResult> Save(ulong minutesId, [FromBody] SaveMinutesBody body, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new SaveMinutesCommand(
                    minutesId, body.Title, body.Content, body.EditLockToken, body.RowVersion,
                    body.Participants, body.ActionItems), cancellationToken));

        // Release the lock without saving (Hủy chỉnh sửa).
        [HttpPost("{minutesId}/release-lock")]
        public async Task<IActionResult> ReleaseLock(ulong minutesId, [FromBody] ReleaseMinutesLockBody body, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ReleaseMinutesLockCommand(minutesId, body.EditLockToken), cancellationToken));

        // "Đồng bộ người mới": attendance rows that should exist but are missing (append-only candidates).
        [HttpGet("{minutesId}/new-participant-candidates")]
        public async Task<IActionResult> NewParticipantCandidates(ulong minutesId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetNewMinuteParticipantsQuery(minutesId), cancellationToken));

        // Search system users to add a participant manually (scoped to editors of the instance).
        [HttpGet("visit-instances/{visitInstanceId}/user-search")]
        public async Task<IActionResult> UserSearch(ulong visitInstanceId, [FromQuery] string? query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new SearchMinuteUsersQuery(visitInstanceId, query), cancellationToken));
    }

    public sealed record CreateOrLockMinutesBody(string? Title);
    public sealed record SaveMinutesBody(
        string Title,
        string? Content,
        string EditLockToken,
        uint RowVersion,
        List<SaveMinuteParticipantInput>? Participants,
        List<SaveMinuteActionItemInput>? ActionItems);
    public sealed record ReleaseMinutesLockBody(string EditLockToken);
}
