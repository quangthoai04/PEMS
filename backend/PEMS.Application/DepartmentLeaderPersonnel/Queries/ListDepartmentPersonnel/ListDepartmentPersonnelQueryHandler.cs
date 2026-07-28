using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.ListDepartmentPersonnel;

/// <summary>
/// Spec §9. Scope first, then keyword, then status — in that order, so the search can only ever reach
/// rows the caller is already entitled to see. Everything (filtering, ordering, paging, counting) runs
/// in the database; the handler never materializes the department to filter in memory.
/// </summary>
public sealed class ListDepartmentPersonnelQueryHandler
    : IRequestHandler<ListDepartmentPersonnelQuery, ListDepartmentPersonnelResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;

    public ListDepartmentPersonnelQueryHandler(
        IApplicationDbContext db, IDepartmentLeaderPersonnelScopeService scopeService)
    {
        _db = db;
        _scopeService = scopeService;
    }

    public async Task<ListDepartmentPersonnelResponse> Handle(
        ListDepartmentPersonnelQuery request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        var page = DepartmentPersonnelListRules.NormalizePage(request.Page);
        var pageSize = DepartmentPersonnelListRules.NormalizePageSize(request.PageSize);
        var keyword = DepartmentPersonnelListRules.NormalizeKeyword(request.Keyword);
        var statusFilter = DepartmentPersonnelListRules.NormalizeStatusFilter(request.Status);

        var headUserId = await _db.Departments.AsNoTracking()
            .Where(d => d.DepartmentId == scope.DepartmentId)
            .Select(d => d.HeadUserId)
            .FirstOrDefaultAsync(cancellationToken);

        // ── 1. Scope. Applied before anything the client sent. ──
        var query = _db.Users.AsNoTracking()
            .Where(u => u.DepartmentId == scope.DepartmentId && u.Role.RoleCode == RoleCodes.Department);

        // ── 2. Keyword, case-insensitively across name / email / phone. ──
        if (keyword is not null)
        {
            query = query.Where(u =>
                u.FullName.ToLower().Contains(keyword)
                || u.Email.ToLower().Contains(keyword)
                || (u.Phone != null && u.Phone.ToLower().Contains(keyword)));
        }

        // ── 3. Status. AND-ed with the keyword, never OR-ed. ──
        if (statusFilter is not null)
        {
            query = query.Where(u => u.Status == statusFilter);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        query = ApplyOrdering(query, request.SortBy, request.SortDirection, headUserId);

        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.Gender,
                u.Status,
                u.SubRole,
                u.AvatarUrl,
                u.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(u => new DepartmentPersonnelListItem
        {
            UserId = u.UserId,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            Gender = DepartmentPersonnelGenders.ToWire(u.Gender),
            Status = u.Status,
            SubRole = u.SubRole,
            Position = DepartmentPersonnelActionFlags.ResolvePosition(u.SubRole),
            AvatarUrl = u.AvatarUrl,
            DepartmentName = scope.DepartmentName,
            CampusName = scope.CampusName,
            CreatedAt = u.CreatedAt,
            CanView = DepartmentPersonnelActionFlags.CanView(),
            CanEdit = DepartmentPersonnelActionFlags.CanEdit(),
            CanDisable = DepartmentPersonnelActionFlags.CanDisable(u.UserId, u.Status, scope.ActorUserId, headUserId),
            CanEnable = DepartmentPersonnelActionFlags.CanEnable(u.UserId, u.Status, scope.ActorUserId),
            CanTransferLeadershipTo = DepartmentPersonnelActionFlags.CanTransferLeadershipTo(
                u.UserId, u.Status, u.SubRole, scope.ActorUserId, headUserId),
            CanResendEmailConfirmation = DepartmentPersonnelActionFlags.CanResendEmailConfirmation(u.Status),
        }).ToList();

        return new ListDepartmentPersonnelResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
        };
    }

    /// <summary>
    /// Default order: the seated head first, then name, then <c>user_id</c>. The trailing id is what
    /// makes paging stable — without a unique tiebreaker two members sharing a name can swap places
    /// between page 1 and page 2 and one of them is never shown.
    ///
    /// An explicit <c>sortBy</c> replaces the name component only; the id tiebreaker always remains.
    /// Column names come from the whitelist, never straight from the request.
    /// </summary>
    private static IQueryable<User> ApplyOrdering(
        IQueryable<User> query, string? sortBy, string? sortDirection, ulong? headUserId)
    {
        var descending = string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        var column = sortBy?.Trim().ToLowerInvariant();

        // The head is pinned to the top regardless of the chosen column — it is the department's
        // primary contact, not a sort result. Expressed over a COLUMN even when the department has no
        // head (the scope gate makes that unreachable, but a bare `OrderBy(u => 0)` would emit
        // `ORDER BY 0`, which MySQL reads as an ordinal column reference and rejects).
        var head = headUserId ?? 0;
        var ordered = query.OrderBy(u => u.UserId == head ? 0 : 1);

        return column switch
        {
            "fullname" => descending
                ? ordered.ThenByDescending(u => u.FullName).ThenBy(u => u.UserId)
                : ordered.ThenBy(u => u.FullName).ThenBy(u => u.UserId),

            "email" => descending
                ? ordered.ThenByDescending(u => u.Email).ThenBy(u => u.UserId)
                : ordered.ThenBy(u => u.Email).ThenBy(u => u.UserId),

            "status" => descending
                ? ordered.ThenByDescending(u => u.Status).ThenBy(u => u.UserId)
                : ordered.ThenBy(u => u.Status).ThenBy(u => u.UserId),

            "createdat" => descending
                ? ordered.ThenByDescending(u => u.CreatedAt).ThenBy(u => u.UserId)
                : ordered.ThenBy(u => u.CreatedAt).ThenBy(u => u.UserId),

            _ => ordered.ThenBy(u => u.FullName).ThenBy(u => u.UserId),
        };
    }
}
