using System.Collections.Generic;
using System.Linq;
using PEMS.Domain.Constants;

namespace PEMS.Application.Departments.Common;

/// <summary>
/// One visit-participant row joined with its user (role/sub-role/department) and its campus
/// instance status, as loaded for the UC-106 participant dependency check. Classification of
/// which rows actually block happens in <see cref="DepartmentParticipantDependencyRule"/> so the
/// business matrix has exactly one implementation.
/// </summary>
public sealed class ParticipantDependencyCandidate
{
    public ulong UserId { get; init; }
    public ulong VisitInstanceId { get; init; }
    public ulong? UserDepartmentId { get; init; }
    public string RoleCode { get; init; } = default!;
    public string? SubRole { get; init; }
    public string ParticipantRole { get; init; } = default!;
    public string ParticipantStatus { get; init; } = default!;
    public string VisitStatus { get; init; } = default!;
}

/// <summary>
/// UC-106 participant blocker matrix (BR-UC106-24..39). A participant row blocks disabling a
/// GENERAL department only when ALL of these hold:
/// user belongs to the target department with role_code DEPARTMENT + sub_role LEADER/STAFF,
/// participant_role is DEPT_SUPPORT, participant status is INVITED/ACCEPTED/ASSIGNED, and the
/// visit instance is in an operational status (explicit allowlist per BR-UC106-33 — never
/// "NOT IN terminal").
///
/// ASSIGNED is canonical only for DEPARTMENT+STAFF; a DEPARTMENT+LEADER with ASSIGNED is a data
/// anomaly but still blocks defensively (BR-UC106-29). Rows on WAITING_REQUEST_APPROVAL visits
/// are anomalies too (no official host yet) and create no canonical blocker (BR-UC106-32).
/// UC-106 never mutates participants — when blocked, the Host/Department Leader must resolve
/// them first (BR-UC106-34).
/// </summary>
public static class DepartmentParticipantDependencyRule
{
    /// <summary>INVITED rows still waiting for a response (blocker type per doc §9.1).</summary>
    public const string PendingInvitationsBlockerType = "PENDING_PARTICIPANT_INVITATIONS";

    /// <summary>ACCEPTED/ASSIGNED rows the host relies on (blocker type per doc §9.2).</summary>
    public const string ActiveParticipationsBlockerType = "ACTIVE_VISIT_PARTICIPATIONS";

    /// <summary>Visit statuses that keep participant obligations alive (BR-UC106-33 allowlist).</summary>
    public static readonly IReadOnlyList<string> OperationalVisitStatuses = new[]
    {
        VisitInstanceStatuses.Assigned,
        VisitInstanceStatuses.BeforeVisit,
        VisitInstanceStatuses.DuringVisit,
        VisitInstanceStatuses.AfterVisit,
    };

    /// <summary>Full blocking predicate for a single candidate row against the target department.</summary>
    public static bool IsDependency(ParticipantDependencyCandidate candidate, ulong targetDepartmentId)
        => candidate.UserDepartmentId == targetDepartmentId
           && candidate.RoleCode == RoleCodes.Department
           && (candidate.SubRole == UserSubRoles.Leader || candidate.SubRole == UserSubRoles.Staff)
           && candidate.ParticipantRole == ParticipantRoles.DeptSupport
           && (candidate.ParticipantStatus == ParticipantStatuses.Invited
               || candidate.ParticipantStatus == ParticipantStatuses.Accepted
               || candidate.ParticipantStatus == ParticipantStatuses.Assigned)
           && OperationalVisitStatuses.Contains(candidate.VisitStatus);

    /// <summary>
    /// Classifies the candidate rows and returns 0..2 blockers (pending invitations, active
    /// participations). Counts distinguish participant rows vs distinct users vs distinct visits
    /// (BR-UC106-38) so the UI never claims "3 nhân sự" for one user in three visits.
    /// </summary>
    public static IReadOnlyList<DepartmentStatusBlocker> BuildBlockers(
        IEnumerable<ParticipantDependencyCandidate> candidates, ulong targetDepartmentId)
    {
        var dependencies = candidates.Where(c => IsDependency(c, targetDepartmentId)).ToList();
        var blockers = new List<DepartmentStatusBlocker>();

        var pending = dependencies
            .Where(d => d.ParticipantStatus == ParticipantStatuses.Invited)
            .ToList();
        if (pending.Count > 0)
        {
            blockers.Add(new DepartmentStatusBlocker
            {
                Type = PendingInvitationsBlockerType,
                Count = pending.Count,
                AffectedUserCount = pending.Select(d => d.UserId).Distinct().Count(),
                AffectedVisitCount = pending.Select(d => d.VisitInstanceId).Distinct().Count(),
                Message = $"Còn {pending.Count} lời mời tham gia đang chờ phản hồi từ nhân sự phòng ban.",
            });
        }

        var active = dependencies
            .Where(d => d.ParticipantStatus == ParticipantStatuses.Accepted
                        || d.ParticipantStatus == ParticipantStatuses.Assigned)
            .ToList();
        if (active.Count > 0)
        {
            blockers.Add(new DepartmentStatusBlocker
            {
                Type = ActiveParticipationsBlockerType,
                Count = active.Count,
                AffectedUserCount = active.Select(d => d.UserId).Distinct().Count(),
                AffectedVisitCount = active.Select(d => d.VisitInstanceId).Distinct().Count(),
                Message = $"Còn {active.Count} lượt tham gia đã được chấp nhận hoặc đang được phân công.",
            });
        }

        return blockers;
    }
}
