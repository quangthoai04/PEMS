using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.ChangePersonnelStatus;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.CreateDepartmentPersonnel;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.ResendPersonnelEmailConfirmation;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.TransferDepartmentLeadership;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.UpdateDepartmentPersonnel;
using PEMS.Application.DepartmentLeaderPersonnel.Queries.GetDepartmentPersonnelDetail;
using PEMS.Application.DepartmentLeaderPersonnel.Queries.GetLeaderCandidates;
using PEMS.Application.DepartmentLeaderPersonnel.Queries.GetMyDepartment;
using PEMS.Application.DepartmentLeaderPersonnel.Queries.GetPersonnelStatusImpact;
using PEMS.Application.DepartmentLeaderPersonnel.Queries.ListDepartmentPersonnel;

namespace PEMS.Api.Controllers;

/// <summary>
/// Department Leader personnel management (spec §6).
///
/// Every route here is scoped to the CALLER's department. There is deliberately no
/// <c>departmentId</c> parameter anywhere in this controller — not in a route, not in a query string,
/// not in a body — because accepting one is what made the previous screen vulnerable to IDOR: a Leader
/// of department A could simply pass department B's id (spec §5.1).
///
/// Authorization runs at two levels and both are required. <see cref="RoleAuthorizeAttribute"/> keeps
/// the wrong ROLE out cheaply, and every handler then re-reads the database to confirm the caller is
/// still the department's seated <c>head_user_id</c> — a check the JWT cannot make, since a token
/// minted before a leadership transfer still carries DEPARTMENT+LEADER (spec §4/§5.2).
///
/// The controller itself holds no business logic: it binds the request, sends it, returns the result.
/// </summary>
[ApiController]
[Authorize]
[RoleAuthorize(EffectiveRole.DepartmentLead)]
[Route("api/department-leader")]
[Produces("application/json")]
public sealed class DepartmentLeaderController : ControllerBase
{
    private readonly IMediator _mediator;

    public DepartmentLeaderController(IMediator mediator) => _mediator = mediator;

    /// <summary>Spec §8 — the caller's own department header + personnel head-count breakdown.</summary>
    [HttpGet("department")]
    public async Task<IActionResult> GetMyDepartment(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetMyDepartmentQuery(), cancellationToken));

    /// <summary>
    /// Spec §9 — paged personnel list with server-side search, status filter and sorting. Scope is
    /// applied before the keyword, so search can never reach another department's rows.
    /// </summary>
    [HttpGet("personnel")]
    public async Task<IActionResult> ListPersonnel(
        [FromQuery] ListDepartmentPersonnelQuery query, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    /// <summary>
    /// Spec §11 — one personnel record. A target outside the department answers 404 exactly like a
    /// non-existent id, so this cannot be used to probe accounts elsewhere.
    /// </summary>
    [HttpGet("personnel/{userId}")]
    public async Task<IActionResult> GetPersonnelDetail(ulong userId, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetDepartmentPersonnelDetailQuery(userId), cancellationToken));

    /// <summary>
    /// Spec §10 — creates a DEPARTMENT/STAFF account in the caller's department. The body carries
    /// identity fields only; role, department, campus and status are server-assigned.
    /// </summary>
    [HttpPost("personnel")]
    public async Task<IActionResult> CreatePersonnel(
        [FromBody] CreateDepartmentPersonnelCommand command, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command, cancellationToken));

    /// <summary>
    /// Spec §12 — edits full name, email, phone and gender. The email is editable in EVERY account
    /// status and never changes that status.
    /// </summary>
    [HttpPut("personnel/{userId}")]
    public async Task<IActionResult> UpdatePersonnel(
        ulong userId,
        [FromBody] UpdateDepartmentPersonnelCommand command,
        CancellationToken cancellationToken)
    {
        // The route id wins over anything in the body, so a mismatched body id cannot retarget the edit.
        command.UserId = userId;
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// Spec §13 — re-issues the confirmation link for a PENDING account. The destination address is
    /// read from the database; the caller cannot supply one.
    /// </summary>
    [HttpPost("personnel/{userId}/resend-email-confirmation")]
    public async Task<IActionResult> ResendEmailConfirmation(ulong userId, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new ResendPersonnelEmailConfirmationCommand(userId), cancellationToken));

    /// <summary>
    /// Spec §14 — read-only preview of what a status change would do (blockers, warnings, sessions).
    /// Nothing is written; the toggle opens this before asking for confirmation.
    /// </summary>
    [HttpGet("personnel/{userId}/status-impact")]
    public async Task<IActionResult> GetStatusImpact(
        ulong userId, [FromQuery] string targetStatus, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPersonnelStatusImpactQuery(userId, targetStatus), cancellationToken));

    /// <summary>
    /// Spec §15 — enable/disable (ACTIVE ↔ INACTIVE only). Re-runs the same blocker evaluation as the
    /// preview under a row lock; nothing is deleted and nobody leaves the department.
    /// </summary>
    [HttpPatch("personnel/{userId}/status")]
    public async Task<IActionResult> ChangePersonnelStatus(
        ulong userId,
        [FromBody] ChangePersonnelStatusCommand command,
        CancellationToken cancellationToken)
    {
        command.UserId = userId;
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// Spec §16 — staff eligible to become the next head. Its own endpoint rather than the paged list,
    /// which would hide eligible members sitting on another page.
    /// </summary>
    [HttpGet("leader-candidates")]
    public async Task<IActionResult> GetLeaderCandidates(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetLeaderCandidatesQuery(), cancellationToken));

    /// <summary>
    /// Spec §16 — hands the department over in one transaction. No point exists at which the
    /// department has zero or two leaders, and concurrent transfers resolve to exactly one winner.
    /// </summary>
    [HttpPost("transfer-leadership")]
    public async Task<IActionResult> TransferLeadership(
        [FromBody] TransferDepartmentLeadershipCommand command, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command, cancellationToken));
}
