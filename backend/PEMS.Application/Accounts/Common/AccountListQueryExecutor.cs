using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Single source of truth for the account list/search/filter read model used by
/// UC-95 (View Account List) and UC-99 (Search and Filter Accounts).
///
/// Responsibilities: enforce the caller's role/campus scope, apply whitelisted
/// filters + sort, page the result, and project to <see cref="AccountListItemDto"/>
/// (no sensitive columns). Read-only — always <c>AsNoTracking()</c>.
/// </summary>
internal static class AccountListQueryExecutor
{
    private static readonly string[] AllowedSortColumns =
    {
        "createdat", "updatedat", "lastloginat", "email", "fullname", "role", "status", "campus"
    };

    public static async Task<PaginatedResult<AccountListItemDto>> ExecuteAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermissionChecker permissionChecker,
        IAccountListCriteria request,
        CancellationToken ct)
    {
        // ── Caller identity (defense in depth; the endpoint is also RBAC-gated) ──
        if (!currentUser.IsAuthenticated ||
            string.IsNullOrEmpty(currentUser.UserId) ||
            string.IsNullOrEmpty(currentUser.RoleCode))
        {
            throw new AuthBusinessException(
                AccountErrorCodes.AccountListForbidden,
                "Bạn không có quyền xem danh sách tài khoản.", 403);
        }

        var roleCode = currentUser.RoleCode!;
        var myCampusId = currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(roleCode);   // ADMIN / HO
        var isStaffLeader = roleCode == RoleCodes.Staff && currentUser.SubRole == SubRoles.Leader;
        var subRoleForCheck = roleCode is "STAFF" or "DEPT" ? currentUser.SubRole ?? "NONE" : "NONE";

        // ── Normalize paging / sort ──
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : (request.PageSize > 100 ? 100 : request.PageSize);

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy)
            ? "createdat"
            : request.SortBy!.Trim().ToLowerInvariant();
        if (!AllowedSortColumns.Contains(sortBy))
        {
            throw new AuthBusinessException(
                AccountErrorCodes.UnsupportedSortColumn, "Cột sắp xếp không hợp lệ.", 400);
        }
        var ascending = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword!.Trim();
        var hasKeyword = keyword is { Length: > 0 };

        var query = db.Users.AsNoTracking();

        // ── Row-level scope by role/campus (never trust client campusId) ──
        if (privileged)
        {
            if (!string.IsNullOrWhiteSpace(request.CampusId))
                query = query.Where(u => u.PrimaryCampusId == request.CampusId);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.CampusId) &&
                !string.Equals(request.CampusId, myCampusId, StringComparison.OrdinalIgnoreCase))
            {
                throw new AuthBusinessException(
                    AccountErrorCodes.CampusScopeForbidden,
                    "Bạn không có quyền xem tài khoản ở cơ sở này.", 403);
            }

            if (isStaffLeader)
            {
                // Staff Leader: own campus, plus Visitor accounts ONLY when searching
                // (never dump all campus-less visitors). Prefer exact email keyword.
                if (string.IsNullOrEmpty(myCampusId))
                    query = hasKeyword ? query.Where(u => u.Role.RoleCode == RoleCodes.Visitor) : query.Where(u => false);
                else if (hasKeyword)
                    query = query.Where(u => u.PrimaryCampusId == myCampusId || u.Role.RoleCode == RoleCodes.Visitor);
                else
                    query = query.Where(u => u.PrimaryCampusId == myCampusId);
            }
            else
            {
                // Other campus-scoped roles: strictly own campus, no visitor dump.
                query = string.IsNullOrEmpty(myCampusId)
                    ? query.Where(u => false)
                    : query.Where(u => u.PrimaryCampusId == myCampusId);
            }
        }

        // ── Keyword search (safe fields only) ──
        if (hasKeyword)
        {
            var kw = keyword!.ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(kw) ||
                u.FullName.ToLower().Contains(kw) ||
                u.Role.RoleCode.ToLower().Contains(kw) ||
                u.Role.Name.ToLower().Contains(kw) ||
                (u.PrimaryCampus != null &&
                    (u.PrimaryCampus.Name.ToLower().Contains(kw) || u.PrimaryCampus.CampusCode.ToLower().Contains(kw))) ||
                (u.Department != null && u.Department.Name.ToLower().Contains(kw)) ||
                (u.Phone != null && u.Phone.Contains(kw)) ||
                (u.StudentCode != null && u.StudentCode.ToLower().Contains(kw)));
        }

        // ── Whitelisted filters ──
        if (!string.IsNullOrWhiteSpace(request.RoleCode))
        {
            var rc = request.RoleCode.Trim().ToUpperInvariant();
            query = query.Where(u => u.Role.RoleCode == rc);
        }

        if (!string.IsNullOrWhiteSpace(request.SubRole))
        {
            var sr = request.SubRole.Trim();
            query = query.Where(u => u.SubRole == sr);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var st = request.Status.Trim().ToUpperInvariant();
            query = query.Where(u => u.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(request.DepartmentId))
            query = query.Where(u => u.DepartmentId == request.DepartmentId);

        if (!string.IsNullOrWhiteSpace(request.ProviderType))
        {
            var pt = request.ProviderType.Trim().ToUpperInvariant();
            query = query.Where(u => u.AuthProviders.Any(p => p.ProviderType == pt));
        }

        if (!string.IsNullOrWhiteSpace(request.CreatedVia))
        {
            var cv = request.CreatedVia.Trim().ToUpperInvariant();
            query = query.Where(u => u.CreatedVia == cv);
        }

        if (!string.IsNullOrWhiteSpace(request.AccountType))
        {
            var at = request.AccountType.Trim().ToUpperInvariant();
            if (at == "VISITOR")
                query = query.Where(u => u.Role.RoleCode == RoleCodes.Visitor);
            else if (at == "INTERNAL")
                query = query.Where(u => u.Role.RoleCode != RoleCodes.Visitor);
            // ALL → no filter
        }

        if (request.HasCampus.HasValue)
        {
            query = request.HasCampus.Value
                ? query.Where(u => u.PrimaryCampusId != null)
                : query.Where(u => u.PrimaryCampusId == null);
        }

        if (request.FromDate.HasValue)
            query = query.Where(u => u.CreatedAt >= request.FromDate.Value);
        if (request.ToDate.HasValue)
        {
            var toExclusive = request.ToDate.Value.Date.AddDays(1);
            query = query.Where(u => u.CreatedAt < toExclusive);
        }
        if (request.LastLoginFrom.HasValue)
            query = query.Where(u => u.LastLoginAt >= request.LastLoginFrom.Value);
        if (request.LastLoginTo.HasValue)
        {
            var lastToExclusive = request.LastLoginTo.Value.Date.AddDays(1);
            query = query.Where(u => u.LastLoginAt < lastToExclusive);
        }

        // ── Sort (whitelist switch, never raw SQL) + stable tie-breaker ──
        IOrderedQueryable<User> ordered = (sortBy, ascending) switch
        {
            ("email", true) => query.OrderBy(u => u.Email),
            ("email", false) => query.OrderByDescending(u => u.Email),
            ("fullname", true) => query.OrderBy(u => u.FullName),
            ("fullname", false) => query.OrderByDescending(u => u.FullName),
            ("role", true) => query.OrderBy(u => u.Role.RoleCode),
            ("role", false) => query.OrderByDescending(u => u.Role.RoleCode),
            ("status", true) => query.OrderBy(u => u.Status),
            ("status", false) => query.OrderByDescending(u => u.Status),
            ("lastloginat", true) => query.OrderBy(u => u.LastLoginAt),
            ("lastloginat", false) => query.OrderByDescending(u => u.LastLoginAt),
            ("updatedat", true) => query.OrderBy(u => u.UpdatedAt),
            ("updatedat", false) => query.OrderByDescending(u => u.UpdatedAt),
            ("campus", true) => query.OrderBy(u => u.PrimaryCampus!.Name),
            ("campus", false) => query.OrderByDescending(u => u.PrimaryCampus!.Name),
            ("createdat", true) => query.OrderBy(u => u.CreatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };
        var sortedQuery = ordered.ThenBy(u => u.UserId);

        var totalItems = await query.CountAsync(ct);

        var rows = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AccountRow
            {
                UserId = u.UserId,
                Email = u.Email,
                FullName = u.FullName,
                Phone = u.Phone,
                Gender = u.Gender,
                AvatarUrl = u.AvatarUrl,
                Nationality = u.Nationality,
                StudentCode = u.StudentCode,
                RoleCode = u.Role.RoleCode,
                RoleName = u.Role.Name,
                SubRole = u.SubRole,
                CampusId = u.PrimaryCampusId,
                CampusCode = u.PrimaryCampus != null ? u.PrimaryCampus.CampusCode : null,
                CampusName = u.PrimaryCampus != null ? u.PrimaryCampus.Name : null,
                DepartmentId = u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                Status = u.Status,
                CreatedVia = u.CreatedVia,
                Providers = u.AuthProviders.Select(p => p.ProviderType).ToList(),
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync(ct);

        // ── Per-caller action permissions (evaluated once) ──
        var roleId = currentUser.RoleId ?? string.Empty;
        var canViewDetailsPerm = !string.IsNullOrEmpty(roleId) &&
            await permissionChecker.HasPermissionAsync(roleId, subRoleForCheck, PermissionCodes.ViewAccountDetails, PermissionLevels.Read, ct);
        var canUpdateRolePerm = !string.IsNullOrEmpty(roleId) &&
            await permissionChecker.HasPermissionAsync(roleId, subRoleForCheck, PermissionCodes.UpdateAccountRole, PermissionLevels.Execute, ct);
        var canManageStatusPerm = !string.IsNullOrEmpty(roleId) &&
            await permissionChecker.HasPermissionAsync(roleId, subRoleForCheck, PermissionCodes.ManageAccountStatus, PermissionLevels.Execute, ct);

        var items = rows.Select(r =>
        {
            var rowIsHigh = r.RoleCode == RoleCodes.Admin || r.RoleCode == RoleCodes.Ho;
            var rowIsVisitor = r.RoleCode == RoleCodes.Visitor;
            var sameCampus = !string.IsNullOrEmpty(myCampusId) &&
                string.Equals(r.CampusId, myCampusId, StringComparison.OrdinalIgnoreCase);

            // Privileged (ADMIN/HO) may act on any row; campus-scoped callers may act on
            // their own-campus rows or on a Visitor (e.g. to convert via UC-100), but
            // never on an ADMIN/HO row.
            var inActionScope = privileged || (!rowIsHigh && (sameCampus || rowIsVisitor));

            return new AccountListItemDto
            {
                UserId = r.UserId,
                Email = r.Email,
                FullName = r.FullName,
                Phone = r.Phone,
                Gender = r.Gender,
                AvatarUrl = r.AvatarUrl,
                Nationality = r.Nationality,
                StudentCode = r.StudentCode,
                RoleCode = r.RoleCode,
                RoleName = r.RoleName,
                SubRole = r.SubRole,
                CampusId = r.CampusId,
                CampusCode = r.CampusCode,
                CampusName = r.CampusName,
                DepartmentId = r.DepartmentId,
                DepartmentName = r.DepartmentName,
                Status = r.Status,
                CreatedVia = r.CreatedVia,
                Providers = r.Providers,
                LastLoginAt = r.LastLoginAt,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                CanViewDetails = canViewDetailsPerm && inActionScope,
                CanUpdateRole = canUpdateRolePerm && inActionScope,
                CanManageStatus = canManageStatusPerm && inActionScope
            };
        }).ToList();

        return PaginatedResult<AccountListItemDto>.Create(items, page, pageSize, totalItems);
    }

    /// <summary>Flat projection target so action flags can be computed in memory.</summary>
    private sealed class AccountRow
    {
        public string UserId { get; init; } = default!;
        public string Email { get; init; } = default!;
        public string FullName { get; init; } = default!;
        public string? Phone { get; init; }
        public string? Gender { get; init; }
        public string? AvatarUrl { get; init; }
        public string? Nationality { get; init; }
        public string? StudentCode { get; init; }
        public string RoleCode { get; init; } = default!;
        public string RoleName { get; init; } = default!;
        public string? SubRole { get; init; }
        public string? CampusId { get; init; }
        public string? CampusCode { get; init; }
        public string? CampusName { get; init; }
        public string? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public string Status { get; init; } = default!;
        public string? CreatedVia { get; init; }
        public List<string> Providers { get; init; } = new();
        public DateTime? LastLoginAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
