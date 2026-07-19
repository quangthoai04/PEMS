using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
/// (no sensitive columns). Read-only Ã¢â‚¬â€ always <c>AsNoTracking()</c>.
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
        IRoleAccessPolicy accessPolicy,
        IAccountListCriteria request,
        CancellationToken ct)
    {
        // Ã¢â€â‚¬Ã¢â€â‚¬ Caller identity (defense in depth; the endpoint is also RBAC-gated) Ã¢â€â‚¬Ã¢â€â‚¬
        if (!currentUser.IsAuthenticated ||
            currentUser.UserId is null ||
            string.IsNullOrEmpty(currentUser.RoleCode))
        {
            throw new AuthBusinessException(
                AccountErrorCodes.AccountListForbidden,
                "BÃ¡ÂºÂ¡n khÃƒÂ´ng cÃƒÂ³ quyÃ¡Â»Ân xem danh sÃƒÂ¡ch tÃƒÂ i khoÃ¡ÂºÂ£n.", 403);
        }

        var roleCode = currentUser.RoleCode!;
        var myCampusId = currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(roleCode);   // ADMIN / HO
        var isHoCaller = roleCode == RoleCodes.Ho;
        var isStaffLeader = roleCode == RoleCodes.Staff && currentUser.SubRole == UserSubRoles.Leader;
        var subRoleForCheck = roleCode is "STAFF" or "DEPARTMENT" ? currentUser.SubRole ?? "NONE" : "NONE";

        // Ã¢â€â‚¬Ã¢â€â‚¬ Normalize paging / sort Ã¢â€â‚¬Ã¢â€â‚¬
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : (request.PageSize > 100 ? 100 : request.PageSize);

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy)
            ? "createdat"
            : request.SortBy!.Trim().ToLowerInvariant();
        if (!AllowedSortColumns.Contains(sortBy))
        {
            throw new AuthBusinessException(
                AccountErrorCodes.UnsupportedSortColumn, "CÃ¡Â»â„¢t sÃ¡ÂºÂ¯p xÃ¡ÂºÂ¿p khÃƒÂ´ng hÃ¡Â»Â£p lÃ¡Â»â€¡.", 400);
        }
        var ascending = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword!.Trim();
        var hasKeyword = keyword is { Length: > 0 };

        var query = db.Users.AsNoTracking();

        // Ã¢â€â‚¬Ã¢â€â‚¬ Row-level scope by role/campus (never trust client campusId) Ã¢â€â‚¬Ã¢â€â‚¬
        if (privileged)
        {
            if (request.CampusId.HasValue)
                query = query.Where(u => u.PrimaryCampusId == request.CampusId);
        }
        else
        {
            if (request.CampusId.HasValue && request.CampusId != myCampusId)
            {
                throw new AuthBusinessException(
                    AccountErrorCodes.CampusScopeForbidden,
                    "BÃ¡ÂºÂ¡n khÃƒÂ´ng cÃƒÂ³ quyÃ¡Â»Ân xem tÃƒÂ i khoÃ¡ÂºÂ£n Ã¡Â»Å¸ cÃ†Â¡ sÃ¡Â»Å¸ nÃƒÂ y.", 403);
            }

            if (isStaffLeader)
            {
                // Staff Leader: own campus, plus Visitor accounts ONLY when searching
                // (never dump all campus-less visitors). Prefer exact email keyword.
                if (!myCampusId.HasValue)
                    query = hasKeyword ? query.Where(u => u.Role.RoleCode == RoleCodes.Visitor) : query.Where(u => false);
                else if (hasKeyword)
                    query = query.Where(u => u.PrimaryCampusId == myCampusId || u.Role.RoleCode == RoleCodes.Visitor);
                else
                    query = query.Where(u => u.PrimaryCampusId == myCampusId);
            }
            else
            {
                // Other campus-scoped roles: strictly own campus, no visitor dump.
                query = !myCampusId.HasValue
                    ? query.Where(u => false)
                    : query.Where(u => u.PrimaryCampusId == myCampusId);
            }
        }

        // Ã¢â€â‚¬Ã¢â€â‚¬ Keyword search (safe fields only) Ã¢â€â‚¬Ã¢â€â‚¬
        // HO Account Management scope (UC-95/UC-99): the HO screen manages ONLY
        // HO accounts and Staff Leaders. ADMIN keeps the unrestricted privileged view.
        if (isHoCaller)
        {
            query = query.Where(u =>
                u.Role.RoleCode == RoleCodes.Ho ||
                (u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader));
        }

        // Staff Leader Account Management scope (UC-95-SL/UC-99-SL): combined with the
        // own-campus row filter above, the Staff Leader screen manages ONLY STAFF (incl.
        // themselves as STAFF/LEADER and IC staff as STAFF/STAFF), Department Leaders and
        // Students. ADMIN/HO/VISITOR and DEPARTMENT/STAFF are never listed.
        if (isStaffLeader)
        {
            query = query.Where(u =>
                u.Role.RoleCode == RoleCodes.Staff ||
                (u.Role.RoleCode == RoleCodes.Department && u.SubRole == UserSubRoles.Leader) ||
                u.Role.RoleCode == RoleCodes.Student);
        }

        if (hasKeyword)
        {
            // Whitespace-insensitive keyword match: collapse every run of whitespace in the keyword
            // away and compare against the space-stripped columns, so "cơ      sở" still matches
            // "Cơ sở ...". REPLACE(x,' ','') is EF-translatable; text columns only ever hold
            // regular spaces so stripping ' ' on the column side is enough.
            var kw = Regex.Replace(keyword!, @"\s+", string.Empty).ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Replace(" ", string.Empty).Contains(kw) ||
                u.FullName.ToLower().Replace(" ", string.Empty).Contains(kw) ||
                u.Role.RoleCode.ToLower().Replace(" ", string.Empty).Contains(kw) ||
                u.Role.Name.ToLower().Replace(" ", string.Empty).Contains(kw) ||
                (u.PrimaryCampus != null &&
                    (u.PrimaryCampus.Name.ToLower().Replace(" ", string.Empty).Contains(kw) ||
                     u.PrimaryCampus.CampusCode.ToLower().Replace(" ", string.Empty).Contains(kw))) ||
                (u.Department != null && u.Department.Name.ToLower().Replace(" ", string.Empty).Contains(kw)) ||
                (u.Phone != null && u.Phone.Replace(" ", string.Empty).Contains(kw)) ||
                (u.StudentCode != null && u.StudentCode.ToLower().Replace(" ", string.Empty).Contains(kw)));
        }

        // Ã¢â€â‚¬Ã¢â€â‚¬ Whitelisted filters Ã¢â€â‚¬Ã¢â€â‚¬
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

        if (request.DepartmentId.HasValue)
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
            // ALL Ã¢â€ â€™ no filter
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

        // Ã¢â€â‚¬Ã¢â€â‚¬ Sort (whitelist switch, never raw SQL) + stable tie-breaker Ã¢â€â‚¬Ã¢â€â‚¬
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

        // Ã¢â€â‚¬Ã¢â€â‚¬ Per-caller action permissions (evaluated once) Ã¢â€â‚¬Ã¢â€â‚¬
        var roleId = currentUser.RoleId;
        var canViewDetailsPerm = accessPolicy.CanAccessAccountManagement(currentUser);
        var canUpdateRolePerm = accessPolicy.CanAccessAccountManagement(currentUser);
        var canManageStatusPerm = accessPolicy.CanAccessAccountManagement(currentUser);

        var items = rows.Select(r =>
        {
            var rowIsHigh = r.RoleCode == RoleCodes.Admin || r.RoleCode == RoleCodes.Ho;
            var rowIsVisitor = r.RoleCode == RoleCodes.Visitor;
            var sameCampus = myCampusId.HasValue && r.CampusId == myCampusId;
            var isCurrentUser = currentUser.UserId.HasValue && r.UserId == currentUser.UserId.Value;

            // Privileged (ADMIN/HO) may act on any row; campus-scoped callers may act on
            // their own-campus rows or on a Visitor (e.g. to convert via UC-100), but
            // never on an ADMIN/HO row.
            var inActionScope = privileged || (!rowIsHigh && (sameCampus || rowIsVisitor));

            // ── Status-toggle availability + reason (UC-97 / HO_BASIC_INFO spec §13). ──
            // A user can never toggle their own status; LOCKED rows are excluded because the toggle
            // only moves ACTIVE↔INACTIVE. An HO caller MAY now enable/disable another HO (any campus)
            // as well as Staff Leaders — the dedicated "special flow" block is gone. The reason lets
            // the UI explain the hide.
            string? hideStatusToggleReason = null;
            if (!canManageStatusPerm)
                hideStatusToggleReason = "NO_PERMISSION";
            else if (isCurrentUser)
                hideStatusToggleReason = "SELF_ACCOUNT";
            else if (!inActionScope)
                hideStatusToggleReason = "TARGET_ROLE_NOT_MANAGEABLE";
            else if (r.Status == UserStatuses.Locked)
                hideStatusToggleReason = "ACCOUNT_LOCKED";

            // ── HO basic-info edit permission + reason (HO_BASIC_INFO spec §11). Only meaningful
            //    for an HO caller: target not self, not LOCKED, and HO or STAFF/LEADER. ──
            var targetIsHoScope = r.RoleCode == RoleCodes.Ho
                || (r.RoleCode == RoleCodes.Staff && r.SubRole == UserSubRoles.Leader);
            string? editBasicInfoDisabledReason = null;
            var canEditBasicInfo = false;
            if (isHoCaller)
            {
                if (!canManageStatusPerm)
                    editBasicInfoDisabledReason = "NO_PERMISSION";
                else if (isCurrentUser)
                    editBasicInfoDisabledReason = "SELF_ACCOUNT";
                else if (!targetIsHoScope)
                    editBasicInfoDisabledReason = "TARGET_ROLE_NOT_MANAGEABLE";
                else if (r.Status == UserStatuses.Locked)
                    editBasicInfoDisabledReason = "ACCOUNT_LOCKED";
                canEditBasicInfo = editBasicInfoDisabledReason is null;
            }

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
                CanUpdateRole = canUpdateRolePerm && inActionScope && !isCurrentUser,
                CanManageStatus = hideStatusToggleReason is null,
                HideStatusToggleReason = hideStatusToggleReason,
                IsCurrentUser = isCurrentUser,
                CanEditBasicInfo = canEditBasicInfo,
                EditBasicInfoDisabledReason = editBasicInfoDisabledReason
            };
        }).ToList();

        return PaginatedResult<AccountListItemDto>.Create(items, page, pageSize, totalItems);
    }

    /// <summary>Flat projection target so action flags can be computed in memory.</summary>
    private sealed class AccountRow
    {
        public ulong UserId { get; init; }
        public string Email { get; init; } = default!;
        public string FullName { get; init; } = default!;
        public string? Phone { get; init; }
        public PEMS.Domain.Enums.Gender? Gender { get; init; }
        public string? AvatarUrl { get; init; }
        public string? Nationality { get; init; }
        public string? StudentCode { get; init; }
        public string RoleCode { get; init; } = default!;
        public string RoleName { get; init; } = default!;
        public string? SubRole { get; init; }
        public ulong? CampusId { get; init; }
        public string? CampusCode { get; init; }
        public string? CampusName { get; init; }
        public ulong? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public string Status { get; init; } = default!;
        public string? CreatedVia { get; init; }
        public List<string> Providers { get; init; } = new();
        public DateTime? LastLoginAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
