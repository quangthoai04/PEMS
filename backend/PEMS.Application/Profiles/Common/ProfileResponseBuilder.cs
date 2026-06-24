using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Profiles.Common;

/// <summary>
/// Loads the current user (joined with role / campus / department) and builds the
/// role-aware <see cref="ProfileResponse"/>. Shared by the View (UC-14) and Update
/// (UC-15) handlers so the response shape and the display rules stay in one place.
/// </summary>
internal static class ProfileResponseBuilder
{
    /// <summary>
    /// Reads the profile of <paramref name="userId"/>. Throws <see cref="NotFoundException"/>
    /// when the user does not exist, and <see cref="ForbiddenException"/> when the account is
    /// not ACTIVE (INACTIVE/LOCKED accounts must not access self-service profile — UC spec §4 / TC-09).
    /// </summary>
    public static async Task<ProfileResponse> BuildAsync(
        IApplicationDbContext db, ulong userId, CancellationToken cancellationToken)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.AvatarUrl,
                u.Gender,
                u.Email,
                u.Phone,
                u.Nationality,
                RoleCode = u.Role.RoleCode,
                u.SubRole,
                u.StudentCode,
                u.Status,
                u.PrimaryCampusId,
                CampusCode = u.PrimaryCampus != null ? u.PrimaryCampus.CampusCode : null,
                CampusName = u.PrimaryCampus != null ? u.PrimaryCampus.Name : null,
                u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                DepartmentType = u.Department != null ? u.Department.DepartmentType : null,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("User", userId);

        if (row.Status != UserStatuses.Active)
            throw new ForbiddenException("Tài khoản của bạn hiện không thể truy cập hồ sơ.");

        var isVisitor = row.RoleCode == RoleCodes.Visitor;

        ProfileCampusInfo? campus = row.PrimaryCampusId is { } campusId && row.CampusName is not null
            ? new ProfileCampusInfo
            {
                CampusId = campusId,
                CampusCode = row.CampusCode ?? string.Empty,
                Name = row.CampusName,
            }
            : null;

        // VISITOR never shows a campus. Internal users always show one (real name, or a
        // "Chưa cấu hình" placeholder when it cannot be resolved). Never hardcode per-role.
        string? displayCampusName = isVisitor
            ? null
            : (campus?.Name ?? "Chưa cấu hình");

        ProfileDepartmentInfo? department = row.DepartmentId is { } departmentId && row.DepartmentName is not null
            ? new ProfileDepartmentInfo
            {
                DepartmentId = departmentId,
                Name = row.DepartmentName,
                DepartmentType = row.DepartmentType ?? string.Empty,
            }
            : null;

        return new ProfileResponse
        {
            UserId = row.UserId,
            FullName = row.FullName,
            AvatarUrl = row.AvatarUrl,
            Gender = row.Gender?.ToString().ToUpperInvariant(),
            Email = row.Email,
            Phone = row.Phone,
            Nationality = row.Nationality,
            RoleCode = row.RoleCode,
            SubRole = row.SubRole,
            DisplayRole = row.RoleCode,
            DisplayPosition = MapPosition(row.SubRole),
            StudentCode = row.StudentCode,
            Campus = campus,
            DisplayCampusName = displayCampusName,
            Department = department,
            DisplayDepartmentName = department?.Name,
            Status = row.Status,
        };
    }

    private static string? MapPosition(string? subRole) => subRole switch
    {
        UserSubRoles.Leader => "Trưởng phòng",
        UserSubRoles.Staff => "Nhân viên",
        _ => null,
    };
}
