using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Services;

/// <summary>Why a proposed Host is not eligible, or <see cref="Eligible"/> when they are.</summary>
public enum HostEligibility
{
    Eligible,
    /// <summary>No such user.</summary>
    NotFound,
    /// <summary>A Staff Leader other than the caller — a leader may only ever nominate themself.</summary>
    OtherStaffLeader,
    /// <summary>Not an ACTIVE IC-department Staff of this campus.</summary>
    NotIcStaffOfCampus,
}

/// <summary>
/// The one definition of "may this person host this campus". Both the approve-and-assign path and the
/// host-transfer path ask THIS, so the second one cannot quietly admit somebody the first would refuse.
///
/// The rule mirrors the DB triggers (trg_visit_campuses_assignment_validate_*): an ACTIVE IC-department
/// Staff whose primary campus is the instance's campus, or the acting Staff Leader themself. Another
/// campus's staff, an inactive account, and a different Staff Leader are all refused.
/// </summary>
public static class VisitHostEligibility
{
    public static async Task<(HostEligibility Result, User? Host)> EvaluateAsync(
        IApplicationDbContext db,
        ulong candidateUserId,
        ulong campusId,
        ulong actingLeaderUserId,
        CancellationToken ct)
    {
        var host = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == candidateUserId, ct);
        if (host is null)
            return (HostEligibility.NotFound, null);

        var departmentType = await db.Departments
            .Where(d => d.DepartmentId == host.DepartmentId)
            .Select(d => d.DepartmentType)
            .FirstOrDefaultAsync(ct);

        var isIcStaff = host.Role.RoleCode == RoleCodes.Staff
            && host.SubRole == UserSubRoles.Staff
            && host.PrimaryCampusId == campusId
            && host.Status == UserStatuses.Active
            && departmentType == "IC";

        var isSelfHost = host.UserId == actingLeaderUserId
            && host.Role.RoleCode == RoleCodes.Staff
            && host.SubRole == UserSubRoles.Leader
            && host.PrimaryCampusId == campusId
            && host.Status == UserStatuses.Active;

        if (isIcStaff || isSelfHost)
            return (HostEligibility.Eligible, host);

        return (host.Role.RoleCode == RoleCodes.Staff && host.SubRole == UserSubRoles.Leader
            ? HostEligibility.OtherStaffLeader
            : HostEligibility.NotIcStaffOfCampus, host);
    }

    /// <summary>The user-facing sentence for a refusal. Kept beside the rule so the two stay in step.</summary>
    public static string MessageFor(HostEligibility result) => result switch
    {
        HostEligibility.OtherStaffLeader =>
            "Staff Leader chỉ được chọn chính mình làm host, không được chọn Staff Leader khác.",
        _ => "Host được chọn phải là IC Staff đang hoạt động thuộc đúng cơ sở, hoặc chính bạn.",
    };
}
