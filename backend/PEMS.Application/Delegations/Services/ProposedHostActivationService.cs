using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Services;

/// <summary>What happened to ONE campus's host proposal when the confirmation gate opened.</summary>
/// <param name="Activated">
/// The proposal survived revalidation and the campus is now ASSIGNED with that person as its host.
/// False means the campus fell back to WAITING_REQUEST_APPROVAL for its Staff Leader to decide —
/// it never means the contact confirmation failed.
/// </param>
/// <param name="RejectionReason">
/// Why the proposal was refused, for the Staff Leader's "choose again" notification. Never shown to
/// the guest-side contact: a host problem is not their business and not their fault.
/// </param>
public readonly record struct ProposedHostActivation(
    ulong VisitInstanceId,
    ulong CampusId,
    ulong? ProposedHostUserId,
    ulong? ProposerUserId,
    string SelectionMode,
    bool Activated,
    string? RejectionReason,
    /// <summary>
    /// The campus's status immediately BEFORE this activation (WAITING_CONTACT_CONFIRMATION or
    /// WAITING_REQUEST_APPROVAL). Null when <see cref="Activated"/> is false — a refused/needs-
    /// reselection outcome writes no decision and so has no "old → new" status change to report.
    /// </summary>
    string? OldStatus = null);

public interface IProposedHostActivationService
{
    /// <summary>
    /// Turns every PENDING host proposal on this request into either an assignment or a
    /// NEEDS_RESELECTION fallback. Call it ONLY once the gate has actually opened, and only after the
    /// caller has flushed the confirmation that opened it — see the remarks on the implementation for
    /// why the two cannot share a SaveChanges. Does not call SaveChanges itself.
    /// </summary>
    Task<IReadOnlyList<ProposedHostActivation>> ActivateAsync(
        VisitRequest visit, DateTime now, CancellationToken cancellationToken);
}

/// <summary>
/// Activates preauthorized reception-host proposals at the exact moment the request's confirmation
/// gate opens (plan §6).
///
/// <para>
/// The rule this exists to keep is narrow and easy to lose: confirming an operational contact must
/// never CHOOSE a host. It may only switch on a host somebody with the authority to pick one already
/// named, and only if that person still qualifies. So there is no fallback, no substitution, and no
/// "closest match" — a proposal that no longer holds simply leaves the campus for its Staff Leader,
/// and the contact's confirmation succeeds regardless. Rolling back a confirmation because an FPTU
/// staffing detail changed would punish the one person in this flow who did what was asked of them.
/// </para>
///
/// <para>
/// <b>Flush ordering matters.</b> The DB refuses a campus reaching ASSIGNED while its request row
/// still reads PENDING_CONTACT_CONFIRMATION, and EF writes visit_request_campuses BEFORE
/// visit_requests within one SaveChanges. Activating in the same SaveChanges as the confirmation
/// would therefore send the ASSIGNED update while the request row still said the gate was shut, and
/// the guard would (correctly) refuse it. Callers flush the confirmation first, then call this, then
/// flush again — all inside one transaction.
/// </para>
/// </summary>
public sealed class ProposedHostActivationService : IProposedHostActivationService
{
    private readonly IApplicationDbContext _db;
    private readonly IUserMutationLockService _lockService;

    public ProposedHostActivationService(IApplicationDbContext db, IUserMutationLockService lockService)
    {
        _db = db;
        _lockService = lockService;
    }

    public async Task<IReadOnlyList<ProposedHostActivation>> ActivateAsync(
        VisitRequest visit, DateTime now, CancellationToken cancellationToken)
    {
        var results = new List<ProposedHostActivation>();

        // A cancelled request has no campuses worth staffing, and the gate being shut means this was
        // called out of order — either way, do nothing rather than assign somebody.
        if (visit.Status == VisitRequestStatuses.Cancelled
            || VisitRequestStatuses.IsBehindContactGate(visit.Status))
            return results;

        var candidates = visit.CampusInstances
            .Where(c => c.ProposedHostUserId is not null
                        && c.ProposedHostActivationStatus == ProposedHostActivationStatuses.Pending)
            // Stable order (plan §6.1 step 2) so concurrent transactions take the row locks the same
            // way round and deadlock instead of interleaving.
            .OrderBy(c => c.VisitInstanceId)
            .ToList();

        // Tiers 2-3 of the shared lock hierarchy (see IUserMutationLockService), taken for the whole
        // batch up front in the same ascending order `candidates` is already sorted in — the comment
        // above predates an actual lock call existing; this is that call. As with CampusApprovalExecutor,
        // the host participant row this activates is IC_HOST, categorically excluded from ever being a
        // pending email/portal response target, so no re-read/re-check after the lock is needed to close
        // a response-vs-write race here — only ordinary commit-ordering against a concurrent cancellation.
        if (candidates.Count > 0)
        {
            await _lockService.LockVisitRequestsAsync(new[] { visit.VisitRequestId }, cancellationToken);
            await _lockService.LockVisitRequestCampusesAsync(
                candidates.Select(c => c.VisitInstanceId).ToArray(), cancellationToken);
        }

        foreach (var instance in candidates)
        {
            // Only a campus that has not been decided yet can be decided by an activation. A campus
            // that was cancelled, rejected or already approved while the invitation was outstanding
            // keeps whatever happened to it.
            if (instance.Status is not (VisitInstanceStatuses.WaitingContactConfirmation
                                        or VisitInstanceStatuses.WaitingRequestApproval)
                || instance.CurrentHostUserId is not null)
            {
                results.Add(Refused(instance, "Cơ sở đã được xử lý trước khi đề xuất kịp kích hoạt."));
                MarkNeedsReselection(instance, now);
                continue;
            }

            // A campus cannot be staffed before it has somebody on the guest side to work with; that
            // is the whole content of the gate, applied per campus.
            if (instance.OperationalContactUserId is null)
            {
                results.Add(Refused(instance, "Cơ sở chưa có đầu mối đoàn khách xác nhận."));
                MarkNeedsReselection(instance, now);
                continue;
            }

            var (refusal, decisionActorRole) = await RevalidateAsync(visit, instance, cancellationToken);
            if (refusal is not null)
            {
                results.Add(Refused(instance, refusal));
                MarkNeedsReselection(instance, now);
                continue;
            }

            var oldStatus = instance.Status;
            Activate(instance, decisionActorRole!, now);
            await EnsureHostParticipantAsync(instance, now, cancellationToken);

            results.Add(new ProposedHostActivation(
                instance.VisitInstanceId, instance.CampusId, instance.ProposedHostUserId,
                instance.ProposedHostByUserId, instance.HostSelectionMode,
                Activated: true, RejectionReason: null, OldStatus: oldStatus));
        }

        return results;
    }

    /// <summary>
    /// The full §6.2 re-check, run against committed state rather than against whatever was true when
    /// the proposal was made — which may be weeks earlier. Returns a null refusal when the proposal
    /// still holds, together with the decision_actor_role the activation must be filed under.
    /// </summary>
    private async Task<(string? Refusal, string? DecisionActorRole)> RevalidateAsync(
        VisitRequest visit, VisitRequestCampus instance, CancellationToken ct)
    {
        var proposedId = instance.ProposedHostUserId!.Value;
        var proposerId = instance.ProposedHostByUserId;

        if (proposerId is null)
            return ("Đề xuất không ghi nhận người đề xuất.", null);

        var proposed = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == proposedId, ct);
        if (proposed is null)
            return ("Người được đề xuất không còn tồn tại.", null);
        if (!string.Equals(proposed.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            return ("Tài khoản người được đề xuất không còn hoạt động.", null);
        if (proposed.PrimaryCampusId != instance.CampusId)
            return ("Người được đề xuất không còn thuộc cơ sở này.", null);

        // Host eligibility is the SHARED rule, asked with the proposer as the "acting leader" — that
        // is exactly who authorized this, so a Leader who proposed themself passes the self-host arm
        // and a Leader proposing another Leader is refused, identically to the approve path.
        var (eligibility, _) = await VisitHostEligibility.EvaluateAsync(
            _db, proposedId, instance.CampusId, proposerId.Value, ct);
        if (eligibility != HostEligibility.Eligible)
            return (VisitHostEligibility.MessageFor(eligibility), null);

        var proposer = proposerId.Value == proposedId
            ? proposed
            : await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == proposerId.Value, ct);
        if (proposer is null)
            return ("Người đề xuất không còn tồn tại.", null);
        if (!string.Equals(proposer.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            return ("Tài khoản người đề xuất không còn hoạt động.", null);
        if (proposer.PrimaryCampusId != instance.CampusId)
            return ("Người đề xuất không còn phụ trách cơ sở này.", null);

        var proposerIsLeader = proposer.Role.RoleCode == RoleCodes.Staff
                               && proposer.SubRole == UserSubRoles.Leader;

        if (instance.HostSelectionMode == HostSelectionModes.Selected)
        {
            // Naming somebody else was always a Leader's right alone. If the proposer has since lost
            // that, the pick has lost its authority with it.
            if (!proposerIsLeader)
                return ("Người đề xuất không còn quyền chỉ định người phụ trách.", null);

            return (null, DecisionActorRole.StaffLeader);
        }

        if (instance.HostSelectionMode == HostSelectionModes.Self)
        {
            if (proposerId.Value != proposedId)
                return ("Đề xuất tự nhận không khớp giữa người đề xuất và người được đề xuất.", null);

            if (proposerIsLeader)
                return (null, DecisionActorRole.StaffLeader);

            // The DB grants a non-Leader decider exactly one licence: they registered this request
            // and took it on themselves. Anything else has to go through the campus Staff Leader.
            if (visit.RegistrantUserId != proposerId.Value)
                return ("Chỉ người đăng ký đơn mới được tự nhận phụ trách mà không cần Staff Leader duyệt.", null);

            return (null, DecisionActorRole.Staff);
        }

        return ("Cơ sở không có phương án người phụ trách hợp lệ.", null);
    }

    private static void Activate(VisitRequestCampus instance, string decisionActorRole, DateTime now)
    {
        var proposerId = instance.ProposedHostByUserId!.Value;

        instance.Status = VisitInstanceStatuses.Assigned;
        instance.CurrentHostUserId = instance.ProposedHostUserId;
        instance.HostAssignedBy = proposerId;
        instance.HostAssignedAt = now;

        // decided_by is the person who authorized the proposal, not the contact who happened to
        // click accept: the contact chose nothing here and must not appear to have approved a campus.
        instance.DecidedBy = proposerId;
        instance.DecidedAt = now;
        instance.DecisionActorRole = decisionActorRole;
        instance.DecisionSource = DecisionSources.PreauthorizedHostActivation;

        instance.ProposedHostActivationStatus = ProposedHostActivationStatuses.Activated;
        instance.ProposedHostActivatedAt = now;

        instance.UpdatedAt = now;
        instance.UpdatedBy = proposerId;
        instance.RowVersion += 1;
    }

    /// <summary>
    /// Keeps the proposal on the row for audit and flags it for re-selection. The campus lands on
    /// WAITING_REQUEST_APPROVAL, which is where a campus with a confirmed contact and no host belongs.
    /// </summary>
    private static void MarkNeedsReselection(VisitRequestCampus instance, DateTime now)
    {
        instance.ProposedHostActivationStatus = ProposedHostActivationStatuses.NeedsReselection;
        if (instance.Status == VisitInstanceStatuses.WaitingContactConfirmation
            && instance.OperationalContactUserId is not null)
            instance.Status = VisitInstanceStatuses.WaitingRequestApproval;
        instance.UpdatedAt = now;
    }

    private static ProposedHostActivation Refused(VisitRequestCampus instance, string reason)
        => new(instance.VisitInstanceId, instance.CampusId, instance.ProposedHostUserId,
               instance.ProposedHostByUserId, instance.HostSelectionMode,
               Activated: false, RejectionReason: reason);

    /// <summary>
    /// One active participant row per user per instance — reuse an existing non-removed row rather
    /// than inserting a duplicate, exactly as the approve path does.
    /// </summary>
    private async Task EnsureHostParticipantAsync(
        VisitRequestCampus instance, DateTime now, CancellationToken ct)
    {
        var hostId = instance.CurrentHostUserId!.Value;
        var assignedBy = instance.HostAssignedBy;

        var existing = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && p.UserId == hostId
                        && p.Status != ParticipantStatuses.Removed)
            .OrderBy(p => p.ParticipantId)
            .FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            _db.VisitParticipants.Add(new VisitParticipant
            {
                VisitInstanceId = instance.VisitInstanceId,
                UserId = hostId,
                ParticipantRole = ParticipantRoles.IcHost,
                IsHost = true,
                Status = ParticipantStatuses.Assigned,
                AssignedBy = assignedBy,
                AssignedAt = now,
                CreatedAt = now,
                CreatedBy = assignedBy,
            });
            return;
        }

        existing.ParticipantRole = ParticipantRoles.IcHost;
        existing.IsHost = true;
        existing.Status = ParticipantStatuses.Assigned;
        existing.AssignedBy = assignedBy;
        existing.AssignedAt = now;
        existing.UpdatedAt = now;
        existing.UpdatedBy = assignedBy;
    }
}
