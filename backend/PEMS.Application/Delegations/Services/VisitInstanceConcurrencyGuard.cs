using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// "Decide the version you actually read" — the optimistic-concurrency check a campus DECISION makes
/// before it writes.
///
/// <para>
/// Approving or rejecting a campus is not like other mutations. Most writes only need to land on a
/// consistent row; a decision is a statement ABOUT CONTENT — a Staff Leader saying "I have read this
/// visit and I accept it". Loading the latest row and approving that turns their answer into an answer
/// about something else: the guest edits the campus while the review screen is open, the campus is
/// bumped to a new revision, and the click that follows approves a delegation, a date or a purpose the
/// approver never saw. Nothing in the audit trail shows it, because from the row's point of view an
/// approval simply happened.
/// </para>
/// <para>
/// So the caller echoes back the <c>rowVersion</c> the screen rendered, and this refuses when the
/// committed row has moved on. The refusal is a 409 with
/// <see cref="VisitRequestErrorCodes.InstanceVersionConflict"/> — the SAME code pending-edit, safe-edit
/// and the amendment paths already raise, so the client has one conflict contract to handle rather
/// than one per endpoint.
/// </para>
/// <para>
/// The row is locked (<c>SELECT … FOR UPDATE</c>) rather than merely compared, because
/// <c>row_version</c> is a plain int with no EF concurrency token behind it: two leaders who both read
/// version 4 would both pass a bare comparison, and the second would silently overwrite the first.
/// Must be called INSIDE the decision's own transaction, or the lock is released before the write it
/// is meant to protect.
/// </para>
/// </summary>
public static class VisitInstanceConcurrencyGuard
{
    /// <summary>
    /// Locks this campus row and refuses unless it is still at <paramref name="expectedRowVersion"/>.
    ///
    /// <para>
    /// A null <paramref name="expectedRowVersion"/> means the caller did not state one. It is accepted
    /// (the field is additive — older clients and internal callers that are not deciding on rendered
    /// content still work) but it buys no protection, which is why every decision UI sends it. The row
    /// is still locked so concurrent decisions serialize either way.
    /// </para>
    /// </summary>
    public static async Task EnsureUnchangedAsync(
        IApplicationDbContext db,
        VisitRequestCampus instance,
        int? expectedRowVersion,
        CancellationToken cancellationToken)
    {
        // Uncomposed FromSqlRaw: composing (Select/First) would wrap the statement in a derived table
        // and MySQL would not lock through it.
        var rows = await db.VisitRequestCampuses
            .FromSqlRaw("SELECT * FROM visit_request_campuses WHERE visit_instance_id = {0} FOR UPDATE",
                instance.VisitInstanceId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var current = rows.Count == 1 ? rows[0].RowVersion : (int?)null;
        if (current is null)
            throw new NotFoundException("VisitRequestCampus", instance.VisitInstanceId);

        // The tracked entity is compared too: it was loaded before the lock was taken, so a version
        // that has moved since means the in-memory campus this decision is about is already stale —
        // even when the caller's expectation happens to match.
        var stale = instance.RowVersion != current.Value
            || (expectedRowVersion.HasValue && expectedRowVersion.Value != current.Value);

        if (stale)
            throw new ConflictException(
                "Thông tin đơn đã được cập nhật sau khi bạn mở màn hình. " +
                "Vui lòng tải phiên bản mới nhất và xem lại trước khi duyệt.",
                VisitRequestErrorCodes.InstanceVersionConflict);
    }
}
