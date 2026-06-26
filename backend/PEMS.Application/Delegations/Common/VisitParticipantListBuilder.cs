using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Builds the participant list of a campus instance with all display names enriched in-memory
/// (user role/sub-role/department, invited-by / assigned-by names). Shared by the participants
/// endpoint and the process-detail endpoint so both always agree.
/// </summary>
public static class VisitParticipantListBuilder
{
    public static async Task<List<VisitParticipantListItemDto>> BuildAsync(
        IApplicationDbContext db, ulong visitInstanceId, CancellationToken cancellationToken)
    {
        var rows = await db.VisitParticipants
            .Where(p => p.VisitInstanceId == visitInstanceId)
            .OrderByDescending(p => p.IsHost)
            .ThenBy(p => p.ParticipantId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return new List<VisitParticipantListItemDto>();

        // Every user referenced: the participant + whoever invited / assigned them.
        var userIds = rows.Select(r => r.UserId)
            .Concat(rows.Where(r => r.InvitedBy.HasValue).Select(r => r.InvitedBy!.Value))
            .Concat(rows.Where(r => r.AssignedBy.HasValue).Select(r => r.AssignedBy!.Value))
            .Distinct()
            .ToList();

        var users = (await (
                from u in db.Users
                join r in db.Roles on u.RoleId equals r.RoleId
                where userIds.Contains(u.UserId)
                select new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    RoleCode = r.RoleCode,
                    u.SubRole,
                    u.DepartmentId,
                })
                .ToListAsync(cancellationToken))
            .ToDictionary(u => u.UserId);

        var deptIds = users.Values
            .Where(u => u.DepartmentId.HasValue)
            .Select(u => u.DepartmentId!.Value)
            .Distinct()
            .ToList();

        var deptNames = deptIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await db.Departments
                .Where(d => deptIds.Contains(d.DepartmentId))
                .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, cancellationToken);

        string? NameOf(ulong? id) => id.HasValue && users.TryGetValue(id.Value, out var u) ? u.FullName : null;

        var list = rows.Select(p =>
        {
            users.TryGetValue(p.UserId, out var u);
            var deptId = u?.DepartmentId;
            return new VisitParticipantListItemDto
            {
                ParticipantId = p.ParticipantId,
                UserId = p.UserId,
                FullName = u?.FullName ?? "(Không xác định)",
                Email = u?.Email ?? string.Empty,
                Phone = u?.Phone,
                RoleCode = u?.RoleCode ?? string.Empty,
                SubRole = u?.SubRole,
                DepartmentId = deptId,
                DepartmentName = deptId.HasValue && deptNames.TryGetValue(deptId.Value, out var dn) ? dn : null,
                ParticipantRole = p.ParticipantRole,
                IsHost = p.IsHost,
                Status = p.Status,
                InvitedByUserId = p.InvitedBy,
                InvitedByName = NameOf(p.InvitedBy),
                InvitedAt = p.InvitedAt,
                RespondedAt = p.RespondedAt,
                AssignedByUserId = p.AssignedBy,
                AssignedByName = NameOf(p.AssignedBy),
                AssignedAt = p.AssignedAt,
                Note = p.Note,
            };
        }).ToList();

        // Department-leader → assigned-staff linkage. AssignDepartmentStaff sets BOTH the leader's
        // DEPT_SUPPORT row and the staff's DEPT_SUPPORT row to ASSIGNED; we surface, on the leader
        // row, which staff (if any) is now handling it so the host UI can show "Đã gán cho [Tên]".
        foreach (var leader in list.Where(x =>
                     x.ParticipantRole == ParticipantRoles.DeptSupport
                     && x.DepartmentId.HasValue
                     && string.Equals(x.SubRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)))
        {
            var staff = list.FirstOrDefault(x =>
                x.ParticipantRole == ParticipantRoles.DeptSupport
                && x.DepartmentId == leader.DepartmentId
                && string.Equals(x.SubRole, UserSubRoles.Staff, StringComparison.OrdinalIgnoreCase)
                && x.Status == ParticipantStatuses.Assigned);

            leader.DepartmentAssignment = new DepartmentAssignmentDto
            {
                DepartmentId = leader.DepartmentId!.Value,
                DepartmentName = leader.DepartmentName ?? string.Empty,
                LeaderUserId = leader.UserId,
                AssignedStaffUserId = staff?.UserId,
                AssignedStaffName = staff?.FullName,
            };
        }

        return list;
    }
}
