using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Makes sure a revision has a BEFORE to be compared against, before anything overwrites it.
///
/// <para>
/// The history drawer diffs revision N against revision N-1. When N-1 was never written the drawer
/// has nothing to compare and reports "Phiên bản: — → 2 · Sự kiện này không có thay đổi chi tiết nào
/// được ghi nhận" — which reads as "nothing changed" to a user who has just changed something. The
/// missing row is the bug; the empty diff is only its symptom.
/// </para>
/// <para>
/// Today's create path writes revision 1 for every campus, so a request created by this code is
/// fine. Rows that predate it are not: canonical seed data, migrated requests, restored databases and
/// test fixtures can all leave <c>form_revision = 1</c> on the detail with no matching row in
/// <c>visit_instance_form_revision_history</c>. The next edit then writes revision 2 into a chain
/// whose first link does not exist.
/// </para>
/// <para>
/// So every path that is ABOUT TO write revision N+1 calls this first, and this captures revision N
/// from the state that is still unmodified. Timing is the whole point — see the CRITICAL note on each
/// method. Called after the mutation, it would record the AFTER values as the BEFORE, which is worse
/// than recording nothing: a fabricated baseline makes the drawer state confidently that nothing
/// changed.
/// </para>
/// <para>
/// It never invents data. If the baseline row already exists it is a no-op, and it only ever writes
/// what the live row currently says. A revision whose true predecessor was lost before this code ran
/// is NOT reconstructed — the read path reports that comparison is unavailable instead
/// (<c>comparisonStatus = PREVIOUS_REVISION_MISSING</c>), because "we do not know" and "nothing
/// changed" are different claims and only one of them is true.
/// </para>
/// <para>
/// The recovery row is marked <see cref="FormRevisionSourceTypes.Migration"/>, an existing enum value
/// (no schema change): it is a technical baseline, not something a person did, and the timeline
/// deliberately does not render MIGRATION rows as user-facing business events.
/// </para>
/// </summary>
internal static class VisitRevisionBaselineGuard
{
    /// <summary>
    /// Ensures <c>visit_instance_form_revision_history</c> holds a row for the campus's CURRENT
    /// <c>FormRevision</c>.
    ///
    /// <para>
    /// CRITICAL: call BEFORE <c>ApplyFormDetail</c>, before the schedule is assigned onto the
    /// instance, and before members are replaced. Those three are what the snapshot captures, and
    /// once any of them has been written the "before" is gone.
    /// </para>
    /// <para>
    /// Does NOT flush. The caller's own SaveChanges commits this in the same transaction as the edit,
    /// so a rolled-back edit cannot leave a baseline behind — and two concurrent edits cannot both
    /// insert one, because they are already serialized by the <c>FOR UPDATE</c> lock the edit paths
    /// take on the campus row before they get here.
    /// </para>
    /// </summary>
    public static async Task EnsureInstanceBaselineAsync(
        IApplicationDbContext db,
        VisitRequest request,
        VisitRequestCampus instance,
        VisitInstanceFormDetail detail,
        ulong actorId,
        System.DateTime now,
        CancellationToken ct)
    {
        var exists = await db.VisitInstanceFormRevisionHistories
            .AsNoTracking()
            .AnyAsync(r => r.VisitInstanceId == instance.VisitInstanceId
                           && r.FormRevision == detail.FormRevision, ct);
        if (exists) return;

        // The members as they are RIGHT NOW. Read from the link table rather than from whatever the
        // caller happens to have staged: the edit paths build their replacement list before writing
        // it, and using that list here would snapshot the after-state under the before-state's
        // revision number — the exact fabrication this guard exists to prevent.
        var members = await db.VisitInstanceGuestMembers.AsNoTracking()
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .Join(db.VisitGuestMembers.AsNoTracking(),
                l => l.GuestMemberId, m => m.GuestMemberId,
                (l, m) => new { l.DisplayOrder, Member = m })
            .OrderBy(x => x.DisplayOrder)
            .Select(x => x.Member)
            .ToListAsync(ct);

        db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
        {
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            FormRevision = detail.FormRevision,
            ApprovalRevision = detail.ApprovalRevision,
            SourceType = FormRevisionSourceTypes.Migration,
            // The canonical builder, never a hand-rolled anonymous object: a baseline serialized in a
            // different shape from the revision it will be compared against produces a diff full of
            // fields that only appear to have changed.
            SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, detail, members),
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = BaselineReason,
        });
    }

    /// <summary>
    /// The request-level equivalent: ensures <c>visit_request_revision_history</c> has a row to diff
    /// the next registrant-snapshot change against.
    ///
    /// <para>
    /// CRITICAL: call BEFORE the registrant columns are written. Same reason as above — after the
    /// write, the row IS the after-state and there is nothing left to record as "before".
    /// </para>
    /// <para>
    /// Request revisions are numbered from the history table itself rather than from a column on the
    /// request, so the baseline is only needed when the table is EMPTY for this request: a chain that
    /// has any row already has a predecessor for whatever comes next.
    /// </para>
    /// </summary>
    /// <param name="preEditSnapshotJson">
    /// The registrant block as it read BEFORE this edit, captured by the caller with
    /// <c>VisitFormRevisionSnapshotBuilder.Request</c> while the row was still untouched.
    ///
    /// <para>
    /// Passed in rather than built here because the caller can only know whether a baseline is
    /// WANTED after it has applied the change (an edit that turns out to touch nothing must not add a
    /// history row), and by then the row no longer holds the before values. Capturing a string costs
    /// nothing; capturing it late costs the truth.
    /// </para>
    /// </param>
    public static async Task EnsureRequestBaselineAsync(
        IApplicationDbContext db,
        VisitRequest request,
        string preEditSnapshotJson,
        ulong actorId,
        System.DateTime now,
        CancellationToken ct)
    {
        var hasAny = await db.VisitRequestRevisionHistories
            .AsNoTracking()
            .AnyAsync(r => r.VisitRequestId == request.VisitRequestId, ct);
        if (hasAny) return;

        db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
        {
            VisitRequestId = request.VisitRequestId,
            RequestRevision = 1,
            SourceType = FormRevisionSourceTypes.Migration,
            SnapshotJson = preEditSnapshotJson,
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = BaselineReason,
        });
    }

    /// <summary>The registrant block as it reads right now — capture BEFORE applying an edit.</summary>
    public static string CaptureRequestSnapshot(VisitRequest request)
        => VisitFormRevisionSnapshotBuilder.Request(request);

    /// <summary>
    /// Marks the row as a recovered technical baseline rather than an action somebody took. Read by
    /// the history reader, which does not surface these as business events.
    /// </summary>
    public const string BaselineReason = "RECOVERED_BASELINE";
}
