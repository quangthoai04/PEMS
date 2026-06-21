using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Delegations.Commands.AssignDepartmentStaff;
using PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;
using PEMS.Application.Delegations.Queries.GetVisitInvitations;
using PEMS.Domain.Constants;

namespace PEMS.Api.Controllers;

[ApiController]
[Route("api/visit-invitations")]
public class VisitInvitationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public VisitInvitationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyInvitations(
        [FromQuery] GetVisitInvitationsQuery query, 
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{participantId}/accept")]
    public async Task<IActionResult> AcceptInvitation(ulong participantId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RespondVisitParticipantInvitationCommand(participantId, true, null), 
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{participantId}/decline")]
    public async Task<IActionResult> DeclineInvitation(
        ulong participantId, 
        [FromBody] DeclineInvitationBody body, 
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RespondVisitParticipantInvitationCommand(participantId, false, body.Reason), 
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{participantId}/assign-department-staff")]
    public async Task<IActionResult> AssignDepartmentStaff(
        ulong participantId, 
        [FromBody] AssignDepartmentStaffBody body, 
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new AssignDepartmentStaffCommand(participantId, body.DepartmentStaffUserId, body.Note), 
            cancellationToken);
        return Ok(result);
    }
}

public sealed record DeclineInvitationBody(string Reason);

public sealed record AssignDepartmentStaffBody(ulong DepartmentStaffUserId, string Note);
