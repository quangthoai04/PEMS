using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Domain.Entities.Departments;

namespace PEMS.Application.Departments.Common;

/// <summary>
/// Single source of truth for the Staff Leader department list read model used by UC-104
/// (View Department List) and UC-103 (Search and Filter Departments).
///
/// Enforces the Staff Leader campus scope, applies keyword (name + head full name) and status
/// filters, sorts (whitelist), pages, and projects to <see cref="DepartmentListItemDto"/>. The head
/// name is resolved via LEFT JOIN (null → UI "Chưa gán trưởng phòng"). <c>canToggleStatus =
/// departmentType == GENERAL</c>. Read-only — always <c>AsNoTracking()</c>.
/// </summary>
internal static class DepartmentListQueryExecutor
{
    private static readonly string[] AllowedSortColumns = { "name", "status", "headname", "createdat" };

    public static async Task<PaginatedResult<DepartmentListItemDto>> ExecuteAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDepartmentListCriteria request,
        CancellationToken ct)
    {
        var campusId = StaffLeaderDepartmentScope.EnsureStaffLeaderCampus(currentUser);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : (request.PageSize > 100 ? 100 : request.PageSize);

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy)
            ? "name"
            : request.SortBy!.Trim().ToLowerInvariant();
        if (!AllowedSortColumns.Contains(sortBy))
            sortBy = "name";
        var ascending = !string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var query = db.Departments.AsNoTracking().Where(d => d.CampusId == campusId);

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword!.Trim().ToLower();
        if (keyword is { Length: > 0 })
        {
            query = query.Where(d =>
                d.Name.ToLower().Contains(keyword) ||
                (d.HeadUser != null && d.HeadUser.FullName.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var st = request.Status!.Trim().ToUpperInvariant();
            if (st is "ACTIVE" or "INACTIVE")
                query = query.Where(d => d.Status == st);
        }

        IOrderedQueryable<Department> ordered = (sortBy, ascending) switch
        {
            ("status", true) => query.OrderBy(d => d.Status),
            ("status", false) => query.OrderByDescending(d => d.Status),
            ("headname", true) => query.OrderBy(d => d.HeadUser != null ? d.HeadUser.FullName : null),
            ("headname", false) => query.OrderByDescending(d => d.HeadUser != null ? d.HeadUser.FullName : null),
            ("createdat", true) => query.OrderBy(d => d.CreatedAt),
            ("createdat", false) => query.OrderByDescending(d => d.CreatedAt),
            ("name", false) => query.OrderByDescending(d => d.Name),
            _ => query.OrderBy(d => d.Name),
        };
        var sortedQuery = ordered.ThenBy(d => d.DepartmentId);

        var totalItems = await query.CountAsync(ct);

        var items = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DepartmentListItemDto
            {
                DepartmentId = d.DepartmentId,
                CampusId = d.CampusId,
                CampusName = d.Campus.Name,
                Name = d.Name,
                HeadUserId = d.HeadUserId,
                HeadFullName = d.HeadUser != null ? d.HeadUser.FullName : null,
                Status = d.Status,
                DepartmentType = d.DepartmentType,
                CanToggleStatus = d.DepartmentType == "GENERAL",
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
            })
            .ToListAsync(ct);

        return PaginatedResult<DepartmentListItemDto>.Create(items, page, pageSize, totalItems);
    }
}
