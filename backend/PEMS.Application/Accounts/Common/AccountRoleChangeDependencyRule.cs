using System.Collections.Generic;
using System.Linq;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// The blocker matrix for "may this account's role change right now?" (spec §7/§8). Pure
/// classification over already-loaded candidate rows, so the rule has exactly one implementation
/// shared by the handler and its unit tests — <see cref="AccountRoleChangeDependencyChecker"/> only
/// does the I/O.
///
/// The system never resolves a responsibility on the user's behalf: when something blocks, the
/// request is refused and the responsibility must be transferred, completed or cancelled through
/// its own flow (spec §5).
/// </summary>
public static class AccountRoleChangeDependencyRule
{
    public const string ActiveHostAssignmentsBlockerType = "ACTIVE_HOST_ASSIGNMENTS";
    public const string ActiveCoordinatorAssignmentsBlockerType = "ACTIVE_COORDINATOR_ASSIGNMENTS";
    public const string PendingParticipantInvitationsBlockerType = "PENDING_PARTICIPANT_INVITATIONS";
    public const string ActiveVisitParticipationsBlockerType = "ACTIVE_VISIT_PARTICIPATIONS";
    public const string ActiveLogisticsResponsibilitiesBlockerType = "ACTIVE_LOGISTICS_RESPONSIBILITIES";
    public const string DepartmentHeadAssignmentBlockerType = "DEPARTMENT_HEAD_ASSIGNMENT";

    /// <summary>How many visit ids a blocker exposes to the client (spec §8.1).</summary>
    public const int SampleVisitInstanceLimit = 5;

    /// <summary>
    /// Campus-instance statuses that keep a responsibility alive for role-change purposes (spec §7).
    /// Deliberately WIDER than the UC-106 department allowlist: UC-106 excludes
    /// WAITING_REQUEST_APPROVAL because no official host exists yet, whereas a role change must be
    /// fail-closed even against an anomalous responsibility on a not-yet-approved instance.
    /// </summary>
    public static readonly IReadOnlyList<string> ActiveVisitStatuses = new[]
    {
        VisitInstanceStatuses.WaitingRequestApproval,
        VisitInstanceStatuses.Assigned,
        VisitInstanceStatuses.BeforeVisit,
        VisitInstanceStatuses.DuringVisit,
        VisitInstanceStatuses.AfterVisit,
    };

    /// <summary>Logistics statuses that still require the assignee to act (spec §8.5).</summary>
    public static readonly IReadOnlyList<string> ActiveLogisticsStatuses = new[]
    {
        LogisticsItemStatus.Requested,
        LogisticsItemStatus.ChangeProposed,
        LogisticsItemStatus.Assigned,
        LogisticsItemStatus.Accepted,
        LogisticsItemStatus.InProgress,
    };

    /// <summary>Logistics statuses in which an unassigned item still binds whoever received it.</summary>
    public static readonly IReadOnlyList<string> ReceivedOnlyBlockingStatuses = new[]
    {
        LogisticsItemStatus.Requested,
        LogisticsItemStatus.ChangeProposed,
    };

    public static bool IsActiveVisit(string visitStatus) => ActiveVisitStatuses.Contains(visitStatus);

    /// <summary>
    /// A participant row is a live responsibility when its status is INVITED / ACCEPTED / ASSIGNED on
    /// an active visit — EXCEPT the canonical Host row, which the host blocker already represents,
    /// and EXCEPT a DEPT_SUPPORT row the target has already delegated away.
    /// </summary>
    public static bool IsParticipantResponsibility(ParticipantResponsibilityCandidate candidate, ulong targetUserId)
    {
        if (!IsActiveVisit(candidate.VisitStatus)) return false;

        var liveStatus = candidate.ParticipantStatus is ParticipantStatuses.Invited
            or ParticipantStatuses.Accepted
            or ParticipantStatuses.Assigned;
        if (!liveStatus) return false;

        // Delegating to a staff member leaves the Department Leader's own row at ASSIGNED — there is
        // no DELEGATED status to move it to — so the row alone cannot tell "handed over" from "doing
        // it personally". The live substitute row does: while it stands, the duty is the staff
        // member's and the leader is free to be re-roled (spec §8.4b). Two deliberate limits: the
        // exemption dies with the substitute (a DECLINED/REMOVED staff bounces the duty back), and
        // it applies only to ASSIGNED — a leader who afterwards accepts the visit personally is
        // ACCEPTED and blocks again, delegation or not.
        if (candidate.ParticipantRole == ParticipantRoles.DeptSupport
            && candidate.ParticipantStatus == ParticipantStatuses.Assigned
            && candidate.DelegatedToSubstitute)
            return false;

        // Host is assigned as BOTH visit_request_campuses.current_host_user_id and an IC_HOST
        // participant row; counting the participant row too would report one responsibility twice
        // (spec §8.4). An IC_HOST row whose instance no longer points at the target is a data
        // anomaly — it is NOT silently dropped, it keeps blocking.
        if (candidate.ParticipantRole == ParticipantRoles.IcHost
            && candidate.InstanceHostUserId == targetUserId)
            return false;

        return true;
    }

    /// <summary>
    /// A logistics item binds the target when they are its assignee on an active status, or when
    /// they received it and it has not been handed to anybody yet. Being merely the historical
    /// <c>received_by</c> of an item now assigned to someone else does NOT block (spec §8.5).
    /// </summary>
    public static bool IsLogisticsResponsibility(LogisticsResponsibilityCandidate candidate, ulong targetUserId)
    {
        if (!IsActiveVisit(candidate.VisitStatus)) return false;

        if (candidate.AssignedToUserId == targetUserId)
            return ActiveLogisticsStatuses.Contains(candidate.LogisticsStatus);

        return candidate.ReceivedBy == targetUserId
               && candidate.AssignedToUserId is null
               && ReceivedOnlyBlockingStatuses.Contains(candidate.LogisticsStatus);
    }

    /// <summary>
    /// Builds every blocker at once — the handler must report the full picture in one 409 rather
    /// than making the user discover the responsibilities one failed attempt at a time (spec §18.6).
    /// </summary>
    public static AccountRoleChangeImpact BuildImpact(
        ulong targetUserId,
        IEnumerable<HostAssignmentCandidate> hostCandidates,
        IEnumerable<CoordinatorAssignmentCandidate> coordinatorCandidates,
        IEnumerable<ParticipantResponsibilityCandidate> participantCandidates,
        IEnumerable<LogisticsResponsibilityCandidate> logisticsCandidates,
        DepartmentHeadCandidate? departmentHead)
    {
        var blockers = new List<AccountRoleChangeBlocker>();
        var affectedVisitIds = new HashSet<ulong>();

        var hosted = hostCandidates.Where(c => IsActiveVisit(c.VisitStatus)).ToList();
        if (hosted.Count > 0)
        {
            var visitIds = Distinct(hosted.Select(c => c.VisitInstanceId));
            affectedVisitIds.UnionWith(visitIds);
            blockers.Add(new AccountRoleChangeBlocker
            {
                Type = ActiveHostAssignmentsBlockerType,
                Count = hosted.Count,
                AffectedVisitCount = visitIds.Count,
                SampleVisitInstanceIds = Sample(visitIds),
                Message = $"Đang là Host chính của {visitIds.Count} đoàn khách đang hoạt động.",
            });
        }

        var coordinated = coordinatorCandidates.Where(c => IsActiveVisit(c.VisitStatus)).ToList();
        if (coordinated.Count > 0)
        {
            var visitIds = Distinct(coordinated.Select(c => c.VisitInstanceId));
            affectedVisitIds.UnionWith(visitIds);
            blockers.Add(new AccountRoleChangeBlocker
            {
                Type = ActiveCoordinatorAssignmentsBlockerType,
                Count = coordinated.Count,
                AffectedVisitCount = visitIds.Count,
                SampleVisitInstanceIds = Sample(visitIds),
                Message = $"Đang là Điều phối viên của {visitIds.Count} đoàn khách đang hoạt động.",
            });
        }

        var participants = participantCandidates
            .Where(c => IsParticipantResponsibility(c, targetUserId))
            .ToList();

        var pendingInvitations = participants
            .Where(c => c.ParticipantStatus == ParticipantStatuses.Invited)
            .ToList();
        if (pendingInvitations.Count > 0)
        {
            var visitIds = Distinct(pendingInvitations.Select(c => c.VisitInstanceId));
            affectedVisitIds.UnionWith(visitIds);
            blockers.Add(new AccountRoleChangeBlocker
            {
                Type = PendingParticipantInvitationsBlockerType,
                Count = pendingInvitations.Count,
                AffectedVisitCount = visitIds.Count,
                SampleVisitInstanceIds = Sample(visitIds),
                Message = $"Còn {pendingInvitations.Count} lời mời tham gia đang chờ phản hồi.",
            });
        }

        var activeParticipations = participants
            .Where(c => c.ParticipantStatus is ParticipantStatuses.Accepted or ParticipantStatuses.Assigned)
            .ToList();
        if (activeParticipations.Count > 0)
        {
            var visitIds = Distinct(activeParticipations.Select(c => c.VisitInstanceId));
            affectedVisitIds.UnionWith(visitIds);
            blockers.Add(new AccountRoleChangeBlocker
            {
                Type = ActiveVisitParticipationsBlockerType,
                Count = activeParticipations.Count,
                AffectedVisitCount = visitIds.Count,
                SampleVisitInstanceIds = Sample(visitIds),
                Message = $"Còn {activeParticipations.Count} lượt tham gia hỗ trợ đang hoạt động.",
            });
        }

        // One logistics item counts once even when the target is both received_by and assignee.
        var logistics = logisticsCandidates
            .Where(c => IsLogisticsResponsibility(c, targetUserId))
            .GroupBy(c => c.LogisticsItemId)
            .Select(g => g.First())
            .ToList();
        if (logistics.Count > 0)
        {
            var visitIds = Distinct(logistics.Select(c => c.VisitInstanceId));
            affectedVisitIds.UnionWith(visitIds);
            blockers.Add(new AccountRoleChangeBlocker
            {
                Type = ActiveLogisticsResponsibilitiesBlockerType,
                Count = logistics.Count,
                AffectedVisitCount = visitIds.Count,
                SampleVisitInstanceIds = Sample(visitIds),
                Message = $"Còn {logistics.Count} nhiệm vụ hậu cần cá nhân chưa hoàn tất.",
            });
        }

        if (departmentHead is not null)
        {
            blockers.Add(new AccountRoleChangeBlocker
            {
                Type = DepartmentHeadAssignmentBlockerType,
                Count = 1,
                AffectedVisitCount = 0,
                Message =
                    $"Tài khoản hiện đang là Trưởng phòng của {departmentHead.DepartmentName}. " +
                    "Vui lòng chỉ định Trưởng phòng thay thế trước khi thay đổi vai trò hoặc phòng ban.",
            });
        }

        return new AccountRoleChangeImpact
        {
            Blockers = blockers,
            AffectedVisitCount = affectedVisitIds.Count,
        };
    }

    /// <summary>
    /// The single Vietnamese message shown when a role change is refused: one line per blocker so
    /// the user sees everything left to resolve, then the one instruction that unblocks them.
    /// Carries no names or emails (spec §11.5) — the department name is business context, not PII.
    /// </summary>
    public static string BuildSummaryMessage(AccountRoleChangeImpact impact)
    {
        var lines = impact.Blockers.Select(b => $"- {b.Message}");
        return
            "Không thể thay đổi vai trò vì tài khoản còn trách nhiệm đang hoạt động:\n"
            + string.Join("\n", lines)
            + "\n\nVui lòng chuyển giao, hoàn tất hoặc hủy hợp lệ toàn bộ trách nhiệm trước khi thử lại.";
    }

    private static List<ulong> Distinct(IEnumerable<ulong> ids) => ids.Distinct().OrderBy(id => id).ToList();

    private static IReadOnlyList<ulong> Sample(List<ulong> visitIds)
        => visitIds.Take(SampleVisitInstanceLimit).ToList();
}
