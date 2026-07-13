using System;
using System.Collections.Generic;
using PEMS.Domain.Constants;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Machine-readable readiness issue codes surfaced in
/// <see cref="CampusOperationalReadinessDto.ReadinessIssues"/>. Mirrored by the frontend so the
/// UI can localize the "vì sao campus ACTIVE nhưng chưa nhận đăng ký" explanation off the code.
/// </summary>
public static class CampusReadinessIssues
{
    public const string CampusInactive = "CAMPUS_INACTIVE";
    public const string ActiveIcDepartmentMissing = "ACTIVE_IC_DEPARTMENT_MISSING";
    public const string MultipleActiveIcDepartments = "MULTIPLE_ACTIVE_IC_DEPARTMENTS";
    public const string ActiveStaffLeaderMissing = "ACTIVE_STAFF_LEADER_MISSING";
    public const string MultipleActiveStaffLeaders = "MULTIPLE_ACTIVE_STAFF_LEADERS";

    /// <summary>
    /// campuses.ic_head_user_id does not point at the resolved Staff Leader. Display/consistency
    /// warning only (UC-86 §8.5) — it never changes <c>IsAvailableForVisitRegistration</c>.
    /// </summary>
    public const string IcHeadMappingInconsistent = "IC_HEAD_MAPPING_INCONSISTENT";
}

/// <summary>
/// Computed operational availability of a campus for the visit registration form (UC-86 §7.2).
/// NOT persisted — campuses.status stays the only stored state; this is derived per request.
/// </summary>
public sealed class CampusOperationalReadinessDto
{
    public bool IsAvailableForVisitRegistration { get; init; }
    public bool ActiveIcDepartmentExists { get; init; }
    public bool ActiveStaffLeaderExists { get; init; }
    public IReadOnlyList<string> ReadinessIssues { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Pure UC-86 §8 classification: a campus may appear on the visit registration form only when it
/// is ACTIVE, has EXACTLY ONE active IC department and EXACTLY ONE valid active Staff Leader.
/// More than one of either is a configuration error (BR-86-19/20) — never resolved by picking one.
/// Shared by every consumer (list/detail readiness, campus options, submit validation, enable
/// response) so the dropdown, the badge and the backend guard can never disagree.
/// </summary>
public static class CampusReadinessRule
{
    public static CampusOperationalReadinessDto Evaluate(
        string campusStatus,
        int activeIcDepartmentCount,
        int validStaffLeaderCount,
        bool icHeadMappingConsistent = true)
    {
        var issues = new List<string>();

        if (!string.Equals(campusStatus, EntityStatuses.Active, StringComparison.Ordinal))
            issues.Add(CampusReadinessIssues.CampusInactive);

        if (activeIcDepartmentCount == 0)
            issues.Add(CampusReadinessIssues.ActiveIcDepartmentMissing);
        else if (activeIcDepartmentCount > 1)
            issues.Add(CampusReadinessIssues.MultipleActiveIcDepartments);

        if (validStaffLeaderCount == 0)
            issues.Add(CampusReadinessIssues.ActiveStaffLeaderMissing);
        else if (validStaffLeaderCount > 1)
            issues.Add(CampusReadinessIssues.MultipleActiveStaffLeaders);

        var available = issues.Count == 0;

        // Consistency warning only — appended AFTER availability is decided (§8.5).
        if (available && !icHeadMappingConsistent)
            issues.Add(CampusReadinessIssues.IcHeadMappingInconsistent);

        return new CampusOperationalReadinessDto
        {
            IsAvailableForVisitRegistration = available,
            ActiveIcDepartmentExists = activeIcDepartmentCount == 1,
            ActiveStaffLeaderExists = validStaffLeaderCount == 1,
            ReadinessIssues = issues,
        };
    }
}
