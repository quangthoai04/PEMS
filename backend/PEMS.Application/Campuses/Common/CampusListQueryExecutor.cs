using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Campuses;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Single source of truth for the campus list/search/filter read model used by
/// UC-82 (View Campus List) and UC-83 (Search and Filter Campus).
///
/// Responsibilities: enforce HO/ADMIN access (defense in depth on top of the
/// controller's [Authorize]), apply the keyword search + whitelisted filters
/// (AND logic), page the result, default-sort by name ASC, and project to
/// <see cref="CampusListItemDto"/>. Read-only — always <c>AsNoTracking()</c>.
/// IC Head name is resolved with a LEFT JOIN (null → UI "Chưa phân công").
/// </summary>
internal static class CampusListQueryExecutor
{
    private static readonly string[] AllowedSortColumns = { "name", "campuscode", "city", "status" };

    public static async Task<PaginatedResult<CampusListItemDto>> ExecuteAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy,
        ICampusListCriteria request,
        CancellationToken ct)
    {
        // ── Authorization (BR-82-01 / BR-83): only HO (and ADMIN superuser) ──
        if (!accessPolicy.CanAccessCampusManagement(currentUser))
        {
            throw new AuthBusinessException(
                CampusErrorCodes.CampusManagementForbidden,
                "Bạn không có quyền xem danh sách campus.", 403);
        }

        // ── Normalize paging / sort ──
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy)
            ? "name"
            : request.SortBy!.Trim().ToLowerInvariant();
        if (!AllowedSortColumns.Contains(sortBy))
            sortBy = "name";
        // Default direction is ascending (BR-82-03 default sort name ASC).
        var ascending = !string.Equals(request.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        var query = db.Campuses.AsNoTracking();

        // ── Keyword search: campus code OR name OR IC Head full name (BR-83-01) ──
        // Whitespace-insensitive: collapse every run of whitespace in the keyword away and compare
        // against the space-stripped columns, so "FPT   Hà   Nội" still matches "FPT Hà Nội".
        // REPLACE(x,' ','') is EF-translatable; these columns only ever hold regular spaces.
        var keyword = string.IsNullOrWhiteSpace(request.Keyword)
            ? null
            : Regex.Replace(request.Keyword!, @"\s+", string.Empty).ToLower();
        if (keyword is { Length: > 0 })
        {
            query = query.Where(c =>
                c.CampusCode.ToLower().Replace(" ", string.Empty).Contains(keyword) ||
                c.Name.ToLower().Replace(" ", string.Empty).Contains(keyword) ||
                (c.IcHeadUser != null &&
                 c.IcHeadUser.FullName.ToLower().Replace(" ", string.Empty).Contains(keyword)));
        }

        // ── Whitelisted filters (AND logic, BR-83-03) ──
        if (!string.IsNullOrWhiteSpace(request.City))
        {
            var city = request.City!.Trim().ToLower();
            query = query.Where(c => c.City != null && c.City.ToLower() == city);
        }

        if (request.CampusId.HasValue)
            query = query.Where(c => c.CampusId == request.CampusId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var st = request.Status!.Trim().ToUpperInvariant();
            query = query.Where(c => c.Status == st);
        }

        // ── Sort (whitelist switch) + stable tie-breaker ──
        IOrderedQueryable<Campus> ordered = (sortBy, ascending) switch
        {
            ("campuscode", true) => query.OrderBy(c => c.CampusCode),
            ("campuscode", false) => query.OrderByDescending(c => c.CampusCode),
            ("city", true) => query.OrderBy(c => c.City),
            ("city", false) => query.OrderByDescending(c => c.City),
            ("status", true) => query.OrderBy(c => c.Status),
            ("status", false) => query.OrderByDescending(c => c.Status),
            ("name", false) => query.OrderByDescending(c => c.Name),
            _ => query.OrderBy(c => c.Name),
        };
        var sortedQuery = ordered.ThenBy(c => c.CampusId);

        var totalItems = await query.CountAsync(ct);

        var canManage = accessPolicy.CanManageCampus(currentUser, null);

        // The status toggle is hidden for the caller's OWN campus (an HO must not disable the
        // campus they belong to). 0 is not a valid campus id, so an ADMIN/HO without a primary
        // campus keeps the toggle on every row.
        var ownCampusId = currentUser.PrimaryCampusId ?? 0UL;

        var items = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CampusListItemDto
            {
                CampusId = c.CampusId,
                CampusCode = c.CampusCode,
                Name = c.Name,
                City = c.City,
                IcHeadUserId = c.IcHeadUserId,
                IcHeadName = c.IcHeadUser != null ? c.IcHeadUser.FullName : null,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                CanManageStatus = canManage && c.CampusId != ownCampusId,
            })
            .ToListAsync(ct);

        // Operational readiness (UC-86 §21) — batch-computed for the current page only, via the
        // shared evaluator, so the badge always matches the registration dropdown and the guard.
        var readinessByCampus = await CampusAvailabilityEvaluator.EvaluateAsync(
            db, items.Select(i => i.CampusId).ToList(), ct);
        foreach (var item in items)
        {
            if (readinessByCampus.TryGetValue(item.CampusId, out var snapshot))
                item.Readiness = snapshot.Readiness;
        }

        return PaginatedResult<CampusListItemDto>.Create(items, page, pageSize, totalItems);
    }
}
