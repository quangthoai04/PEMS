using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Admin.Commands.RevokeSession;
using PEMS.Application.Admin.Commands.RevokeUserSessions;
using PEMS.Application.Delegations.Commands.BackfillVisitHistory;
using PEMS.Application.Delegations.Commands.RepairLegacyOperationalContact;
using PEMS.Application.Admin.Queries.GetAdminAuditLogDetail;
using PEMS.Application.Admin.Queries.GetAdminAuditLogs;
using PEMS.Application.Admin.Queries.GetAdminDashboardSummary;
using PEMS.Application.Admin.Queries.GetAdminIntegrationsOverview;
using PEMS.Application.Admin.Queries.GetAdminLoginActivity;
using PEMS.Application.Admin.Queries.GetAdminLoginLogs;
using PEMS.Application.Admin.Queries.GetAdminRecentAudits;
using PEMS.Application.Admin.Queries.GetAdminSecurityEvents;
using PEMS.Application.Admin.Queries.GetAdminSecurityOverview;
using PEMS.Application.Admin.Queries.GetAdminSessions;
using PEMS.Application.Common.Security;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// System Administration Console (ADMIN-only): dashboard counters, session
    /// administration, login/security logs and the audit trail. Every handler
    /// re-checks the ADMIN role — the attribute here is the first gate only.
    /// </summary>
    [ApiController]
    [Authorize]
    [RoleAuthorize(EffectiveRole.Admin)]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AdminController(IMediator mediator) => _mediator = mediator;

        // ── Dashboard ────────────────────────────────────────────────────────

        [HttpGet("dashboard/summary")]
        public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetAdminDashboardSummaryQuery(), cancellationToken));

        [HttpGet("dashboard/login-activity")]
        public async Task<IActionResult> GetLoginActivity([FromQuery] GetAdminLoginActivityQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpGet("dashboard/security")]
        public async Task<IActionResult> GetSecurityOverview(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetAdminSecurityOverviewQuery(), cancellationToken));

        [HttpGet("dashboard/integrations")]
        public async Task<IActionResult> GetIntegrationsOverview([FromQuery] GetAdminIntegrationsOverviewQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpGet("dashboard/recent-audits")]
        public async Task<IActionResult> GetRecentAudits([FromQuery] GetAdminRecentAuditsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        // ── Sessions ─────────────────────────────────────────────────────────

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] GetAdminSessionsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpPost("sessions/{sessionId}/revoke")]
        public async Task<IActionResult> RevokeSession(
            ulong sessionId,
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RevokeSessionCommand? command,
            CancellationToken cancellationToken)
        {
            command ??= new RevokeSessionCommand();
            command.SessionId = sessionId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpPost("users/{userId}/revoke-sessions")]
        public async Task<IActionResult> RevokeUserSessions(
            ulong userId,
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RevokeUserSessionsCommand? command,
            CancellationToken cancellationToken)
        {
            command ??= new RevokeUserSessionsCommand();
            command.UserId = userId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        // ── Security logs ────────────────────────────────────────────────────

        [HttpGet("login-logs")]
        public async Task<IActionResult> GetLoginLogs([FromQuery] GetAdminLoginLogsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpGet("security-events")]
        public async Task<IActionResult> GetSecurityEvents([FromQuery] GetAdminSecurityEventsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        // ── Audit trail ──────────────────────────────────────────────────────

        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs([FromQuery] GetAdminAuditLogsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        [HttpGet("audit-logs/{auditLogId}")]
        public async Task<IActionResult> GetAuditLogDetail(ulong auditLogId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetAdminAuditLogDetailQuery(auditLogId), cancellationToken));

        // ── Visit history backfill (VISIT_HISTORY_INTEGRITY final phase, Phase C) ──────────────
        // Deliberate, one-off recovery of pre-Commit-2/3/4 history gaps. Never runs automatically.

        [HttpPost("visit-history-backfill")]
        public async Task<IActionResult> BackfillVisitHistory(
            [FromQuery] bool dryRun, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new BackfillVisitHistoryCommand(dryRun), cancellationToken));

        // ── Legacy confirmed-contact repair (forensic, one-off) ─────────────────────────────────
        // Read-only by default. `mode` MUST be the exact literal "APPLY" to write anything — an
        // omitted, empty, or misspelled value is always a dry run. Never bound as a bool: a bool query
        // parameter defaults to false when omitted, which would make "forgot the flag" silently mean
        // "apply", exactly the mistake this endpoint refuses to make possible.

        [HttpPost("legacy-contact-repair")]
        public async Task<IActionResult> RepairLegacyOperationalContact(
            [FromQuery] string? mode, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new RepairLegacyOperationalContactCommand(mode), cancellationToken));
    }
}
