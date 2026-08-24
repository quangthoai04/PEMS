using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.OperationalContact;

/// <summary>
/// Corrects ONE campus's operational-contact DETAILS. The address is unchanged by definition — a
/// changed address is an identity change and never reaches this handler.
///
/// <para>
/// What it does NOT do is the whole point, so it is worth stating plainly: no
/// <c>visit_request_identity_changes</c> row, no <c>email_action_tokens</c> row, no email of any kind,
/// no change to <c>operational_contact_user_id</c>, no clearing of the confirmation or its source, no
/// move to WAITING_CONTACT_CONFIRMATION, no effect on the global contact gate, no campus or request
/// status change, no amendment, and no registration lead-time check.
/// </para>
/// <para>
/// A live invitation is left exactly as it is — same token, same <c>expires_at</c>, same
/// <c>token_version</c>, same resend budget. Its stored snapshot IS refreshed, because that snapshot is
/// what the accept writes onto the campus: leaving it stale would mean a correction made today is
/// silently undone the moment the invited person clicks their link. An invitation that is no longer
/// PENDING is not touched at all — a metadata edit must not resurrect one that has expired.
/// </para>
/// </summary>
public sealed class UpdateOperationalContactProfileCommandHandler
    : IRequestHandler<UpdateOperationalContactProfileCommand, OperationalContactManageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IOperationalContactInvitationService _invitations;
    private readonly ICanonicalContentRefresher _canonical;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public UpdateOperationalContactProfileCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IOperationalContactInvitationService invitations, ICanonicalContentRefresher canonical,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _invitations = invitations;
        _canonical = canonical;
        _writeFlag = writeFlag;
    }

    public async Task<OperationalContactManageResponse> Handle(
        UpdateOperationalContactProfileCommand request, CancellationToken cancellationToken)
    {
        var actorId = OperationalContactGuards.RequireAuthenticated(_writeFlag, _currentUser);
        var now = _clock.VietnamNow;
        var correlationId = Guid.NewGuid().ToString("N");

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        // ── Request-level lock FIRST, same order VisitSafeEditService uses (plan CanhIter3FixBug,
        //    decisions V/W) — this handler now writes request-level canonical content below, so it needs
        //    the same request-then-instance lock discipline to avoid racing a concurrent Safe Edit's own
        //    canonical recompute on the same VisitRequest row. Uncomposed FromSqlRaw: composing would
        //    wrap the statement in a derived table and MySQL would not lock through it. ──
        await _db.VisitRequests
            .FromSqlRaw("SELECT * FROM visit_requests WHERE visit_request_id = {0} FOR UPDATE",
                request.VisitRequestId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var (visit, instance) = await OperationalContactGuards.LoadCampusInRequestAsync(
            _db, request.VisitRequestId, request.VisitInstanceId, cancellationToken,
            includeMembersForCanonical: true);

        // The registrant, or the person currently holding THIS campus correcting their own details.
        OperationalContactGuards.EnsureMayManageContact(visit, instance, actorId, allowCurrentContact: true);
        OperationalContactGuards.EnsureProfileUpdateAllowed(visit, instance);

        var detail = instance.FormDetail
            ?? throw new NotFoundException("Chi tiết đơn của cơ sở này", instance.VisitInstanceId);

        // ── The address is not this command's to move. Refused rather than ignored: a form that sent a
        //    new address and got "saved" back would have applied the name and dropped the address, and
        //    the user would have no way to tell. ──
        var currentEmail = VisitRequestFingerprintBuilder.NormalizeEmail(detail.OperationalContactEmail);
        var incomingEmail = VisitRequestFingerprintBuilder.NormalizeEmail(request.Email);
        if (!string.Equals(currentEmail, incomingEmail, StringComparison.Ordinal))
            throw new ConflictException(
                "Email đầu mối vận hành đã thay đổi. Đổi email là thay đổi người phụ trách và phải qua bước xác nhận.",
                OperationalContactErrorCodes.ChangeConflict);

        // ── Authoritative per-instance concurrency (plan CanhIter3FixBug, decision U): a bare in-memory
        //    comparison here would let a concurrent VisitSafeEditService contact edit on the SAME
        //    instance silently win or lose depending on load order — row_version is a plain int with no
        //    EF concurrency token behind it (confirmed: IsConcurrencyToken is used nowhere in this
        //    codebase). VisitInstanceConcurrencyGuard does a real SELECT ... FOR UPDATE re-read. A caller
        //    with no stated expectation (older/internal client, ExpectedRowVersion null) is still
        //    protected — the TRACKED value becomes the fallback baseline, so a row that moved since load
        //    is still caught, just without the extra guarantee that the caller personally read it. ──
        await VisitInstanceConcurrencyGuard.EnsureUnchangedAsync(
            _db, instance, request.ExpectedRowVersion ?? instance.RowVersion, cancellationToken);

        var profile = OperationalContactProfileMutation.Normalize(
            request.FullName, request.Organization, request.JobTitle, request.Phone);

        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = OperationalContactHistoryAudit.ProfileUpdated,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            CorrelationId = correlationId,
            SourceType = "IDENTITY",
            CreatedAt = now,
        };
        var anyChanged = OperationalContactProfileMutation.AddProfileChanges(audit, detail, profile, now);

        if (!anyChanged)
            throw new BusinessRuleException(
                "Không có thay đổi nào để lưu cho đầu mối vận hành của cơ sở này.",
                OperationalContactErrorCodes.ProfileNoChanges);

        OperationalContactProfileMutation.Apply(detail, profile);
        // operational_contact_guest_member_id is deliberately UNTOUCHED, unlike the replace and
        // transfer paths which clear it. This corrects the DETAILS of the person already holding the
        // role — a misspelt name, a new phone — and correcting somebody's spelling does not make them
        // a different human. Clearing the link here would strip the "· Đầu mối" badge off the right
        // person in the biên bản for a typo fix; the address, which is what identity actually turns
        // on, cannot be changed through this handler at all.
        detail.RowVersion += 1;
        detail.UpdatedAt = now;
        detail.UpdatedBy = actorId;

        // form_revision is deliberately NOT bumped. It counts revisions of what the campus is being
        // ASKED to host — the delegation, the schedule, the people — and a Staff Leader reading
        // "phiên bản 3" should be looking at three versions of the ask, not at a corrected phone number.

        // ── Canonical content (VisitScope/HasMixedCampusDetails/BusinessFingerprint) includes contact
        //    FullName/Organization/JobTitle/Phone (plan CanhIter3FixBug, decision V) — VisitSafeEditService
        //    already recomputes this after any touched instance, so a metadata correction made through
        //    THIS door must leave the same derived state, not a stale one. Recomputed here rather than
        //    request.RowVersion also being bumped: this handler has never touched that counter, and
        //    recomputing derived content is a read-model-freshness fix orthogonal to the optimistic-lock
        //    counter's own semantics. Not called when nothing actually changed. ──
        await _canonical.RecomputeAsync(visit, cancellationToken);

        // One lock, one pass: refresh the invitation's snapshot if it is about this address, and take
        // the view the response reports. Reading it back after the commit would mean a second
        // `SELECT … FOR UPDATE` outside any transaction.
        var pending = await RefreshPendingSnapshotAsync(instance, detail, cancellationToken);

        instance.RowVersion += 1;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;

        _db.AuditLogs.Add(audit);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        // Reports the campus and request EXACTLY as they were before the save, because that is the
        // truth: this command changes neither. The confirmation state is carried through so a screen
        // that refreshes from this response does not lose the pending-invitation banner it was showing.
        return new OperationalContactManageResponse(
            visit.VisitRequestId, instance.VisitInstanceId, instance.Status, visit.Status,
            ContactConfirmed: instance.OperationalContactUserId is not null,
            pending.Kind, pending.Status, pending.EmailMasked,
            pending.ExpiresAt, pending.ResendCount, pending.TokenVersion,
            "Đã cập nhật thông tin đầu mối vận hành của cơ sở. Người phụ trách và trạng thái xác nhận giữ nguyên.");
    }

    /// <summary>
    /// Keeps a PENDING invitation's stored snapshot in step with the campus, without touching anything
    /// that governs its lifetime.
    ///
    /// <para>
    /// The row is taken FOR UPDATE and re-checked for PENDING, so an invitation that expired or was
    /// answered between the read and here is left alone. <c>expires_at</c>, <c>token_version</c> and
    /// <c>resend_count</c> are not assigned at all — a correction is not a resend, and extending a
    /// deadline as a side effect of fixing a job title would hand the invited person extra time nobody
    /// decided to give them.
    /// </para>
    /// </summary>
    /// <returns>The in-flight invitation as the response should report it (all-null when there is none).</returns>
    private async Task<PendingChangeView> RefreshPendingSnapshotAsync(
        VisitRequestCampus instance, VisitInstanceFormDetail detail, CancellationToken ct)
    {
        var pending = await _invitations.LockPendingChangeForInstanceAsync(instance.VisitInstanceId, ct);
        if (pending is null || pending.Status != IdentityChangeStatuses.Pending)
            return new PendingChangeView(null, null, null, null, 0, 0);

        var view = new PendingChangeView(
            pending.ChangeKind, pending.Status, pending.NewEmailMasked,
            pending.ExpiresAt, pending.ResendCount, pending.TokenVersion);

        // Only when the invitation is about THIS address. A pending TRANSFER is an invitation to a
        // DIFFERENT person, whose details are the transfer's own and none of this command's business.
        var invited = pending.NewEmailNormalized;
        if (string.IsNullOrEmpty(invited)
            || !string.Equals(
                invited,
                VisitRequestFingerprintBuilder.NormalizeEmail(detail.OperationalContactEmail),
                StringComparison.Ordinal))
            return view;

        pending.PendingSnapshotJson = OperationalContactProfileMutation.BuildPendingSnapshotJson(detail, invited);

        return view;
    }

    private sealed record PendingChangeView(
        string? Kind, string? Status, string? EmailMasked, DateTime? ExpiresAt,
        uint ResendCount, uint TokenVersion);
}
