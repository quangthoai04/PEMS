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
    /// The campus revision a decision claims to have been taken on, refused when the caller states none.
    ///
    /// <para>
    /// It lives here rather than in the controller because it is the same rule as
    /// <see cref="EnsureUnchangedAsync"/>, one step earlier: that method answers "is the revision you
    /// read still current?", and this one answers "did you read one at all?". A caller who cannot say
    /// which revision they judged has not made a reviewable decision, and defaulting them to the
    /// current row is precisely the stale-review this guard exists to prevent — silently, and on the
    /// content nobody looked at.
    /// </para>
    /// <para>
    /// The parameter is nullable because that is what an omitted JSON field looks like at the transport
    /// boundary; binding it as a plain int would read a missing field as 0, which is a REAL row version
    /// and would quietly decide against an unreviewed campus. Past this point nothing is nullable, so
    /// no code downstream has an "unstated" case left to get wrong.
    /// </para>
    /// </summary>
    /// <returns>The stated revision, which may legitimately be 0 — a campus starts there.</returns>
    /// <exception cref="ValidationException">
    /// 400 <see cref="VisitRequestErrorCodes.InstanceVersionRequired"/> when none was stated.
    /// </exception>
    public static int RequireExpectedRowVersion(int? expectedRowVersion)
        => expectedRowVersion
           ?? throw new ValidationException(
               "Thiếu phiên bản dữ liệu của cơ sở. Vui lòng tải lại màn hình duyệt rồi thao tác lại.",
               VisitRequestErrorCodes.InstanceVersionRequired);

    /// <summary>
    /// Locks this campus row and refuses unless it is still at <paramref name="expectedRowVersion"/>.
    ///
    /// <para>
    /// <paramref name="expectedRowVersion"/> is mandatory. It used to be nullable, and a null was
    /// accepted as "no expectation stated" — which meant the whole protection could be dropped by
    /// leaving one field out of the request, and a caller that omitted it decided against whatever the
    /// row had silently become. There is now nothing to omit: the decision commands take a plain
    /// <c>int</c> and the HTTP boundary refuses a missing field before a command is ever built.
    /// </para>
    /// </summary>
    public static async Task EnsureUnchangedAsync(
        IApplicationDbContext db,
        VisitRequestCampus instance,
        int expectedRowVersion,
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
        var stale = instance.RowVersion != current.Value || expectedRowVersion != current.Value;

        if (stale)
            throw new ConflictException(
                "Thông tin đơn đã được cập nhật sau khi bạn mở màn hình. " +
                "Vui lòng tải phiên bản mới nhất và xem lại trước khi duyệt.",
                VisitRequestErrorCodes.InstanceVersionConflict);
    }
}
