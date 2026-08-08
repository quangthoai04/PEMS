using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.VisitNotifications;

/// <summary>
/// Tells the registrant that ONE campus declined their request, and why.
///
/// <para>
/// Everything it says is scoped to that campus — the code, the delegation, the campus, its window and
/// the leader's reason. It never reads the request's aggregate status: a request with one campus still
/// waiting and one already accepted is not a rejected request, and a message that said so would be
/// wrong about the campuses it did not name.
/// </para>
/// <para>
/// TO is the REGISTRANT, because they are the one who can edit and resubmit. The campus's own contact
/// gets the in-app notification the command writes; this email exists because a refusal that appears
/// only inside a dashboard is a refusal the guest side may not see for days.
/// </para>
/// <para>
/// It reads its own context from the campus instead of taking it from the rejecting command, which is
/// what lets the recovery sweep send exactly the same message hours later without replaying the
/// rejection.
/// </para>
/// </summary>
public sealed class CampusRejectionEmail : IRecoverableVisitEmail
{
    /// <summary>
    /// The audit action the rejection writes. One row per rejection, never updated and never deleted —
    /// which is exactly what a notification needs as its identity.
    /// </summary>
    public const string RejectionAuditAction = "REJECT_CAMPUS_INSTANCE";

    private readonly IApplicationDbContext _db;

    public CampusRejectionEmail(IApplicationDbContext db) => _db = db;

    public string TemplateCode => SystemEmailTemplates.VisitCampusRejected;

    /// <summary>
    /// Keyed to the rejection EVENT, not to the campus (plan §37).
    ///
    /// <para>
    /// A campus can be rejected, resubmitted and rejected again, and each of those is a separate thing
    /// the registrant has to be told about. Keyed on the campus, the first successful message would
    /// have answered "has this been notified?" with yes forever, and the second rejection's failed
    /// email would never have been retried — the exact case plan §38 tests.
    /// </para>
    /// </summary>
    public string RelatedType => "VisitCampusRejectionEvent";

    /// <param name="rejectionEventId">
    /// <c>audit_logs.audit_log_id</c> of the <see cref="RejectionAuditAction"/> row this message is for.
    /// </param>
    public async Task<SystemEmailRequest?> BuildAsync(ulong rejectionEventId, CancellationToken ct)
    {
        var rejection = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.AuditLogId == rejectionEventId && a.Action == RejectionAuditAction)
            .Select(a => new { a.VisitInstanceId })
            .FirstOrDefaultAsync(ct);

        if (rejection?.VisitInstanceId is not ulong visitInstanceId) return null;

        // A later rejection of the same campus replaces what this one had to say — the campus has been
        // resubmitted and refused again since, and THAT event carries its own identity and its own
        // recovery. Sending this one now would describe a decision that has been superseded, with the
        // reason of the newer one. Nothing is owed here.
        var superseded = await _db.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.Action == RejectionAuditAction
                           && a.VisitInstanceId == visitInstanceId
                           && a.AuditLogId > rejectionEventId, ct);
        if (superseded) return null;

        var context = await (
            from c in _db.VisitRequestCampuses.AsNoTracking()
            join site in _db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            join v in _db.VisitRequests.AsNoTracking() on c.VisitRequestId equals v.VisitRequestId
            where c.VisitInstanceId == visitInstanceId
            select new
            {
                v.RequestCode,
                v.RegistrantEmail,
                v.RegistrantFullName,
                CampusName = site.Name,
                DelegationName = c.FormDetail!.DelegationName,
                c.PlannedStartAt,
                c.PlannedEndAt,
                c.DecisionNote,
                c.Status,
            }).FirstOrDefaultAsync(ct);

        if (context is null || string.IsNullOrWhiteSpace(context.RegistrantEmail))
            return null;

        // The campus has moved on — a whole-request resubmit put it back in review and cleared the
        // decision the DB requires it to shed on the way out. There is no rejection left to describe.
        if (context.Status != VisitInstanceStatuses.Rejected) return null;

        return new SystemEmailRequest(
            TemplateCode,
            new EmailRecipient(context.RegistrantEmail, context.RegistrantFullName),
            new Dictionary<string, string>
            {
                ["recipientName"] = context.RegistrantFullName ?? string.Empty,
                ["requestCode"] = context.RequestCode,
                ["delegationName"] = context.DelegationName,
                ["campusName"] = context.CampusName,
                // Same name and shape as every other visit email's window.
                ["plannedTime"] =
                    $"{context.PlannedStartAt:HH:mm dd/MM/yyyy} - {context.PlannedEndAt:HH:mm dd/MM/yyyy}",
                // The stored decision note, not a value passed in: on a retry the command's local copy
                // is long gone, and the column is where the reason actually lives. It is THIS event's
                // reason because a newer rejection would have superseded this message above.
                ["reason"] = context.DecisionNote ?? string.Empty,
            },
            RelatedType: RelatedType,
            RelatedId: rejectionEventId);
    }
}
