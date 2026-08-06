using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// Lists the Visitor accounts related to the caller's campus (Staff Leader only). The campus
/// relation lives in <see cref="RelatedVisitorScope"/> so list, nationalities and detail share one
/// definition. Read-only — every row is non-actionable.
///
/// A Visitor appears exactly once no matter how many requests or campus instances tie them to the
/// campus; <c>RelatedRequestCount</c> counts DISTINCT requests, not instances.
///
/// Implementation note: the related-request aggregates are computed by loading the (small,
/// campus-scoped) set of visible instances and grouping in memory, then enriching the matching
/// Visitor rows via a batched <c>Contains</c> lookup. This deliberately avoids correlated /
/// projection subqueries and order-by-subquery, which do not translate reliably on Pomelo/MySQL
/// (same approach as ViewGuestDelegationList / ViewMyVisitInvitations).
/// </summary>
public sealed class GetRelatedVisitorsQueryHandler
    : IRequestHandler<GetRelatedVisitorsQuery, PaginatedResult<RelatedVisitorAccountListItemDto>>
{
    private static readonly string[] AllowedSortColumns =
    {
        "lastrelatedrequestat", "createdat", "fullname", "latestplannedstartat", "lastloginat"
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRelatedVisitorsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<RelatedVisitorAccountListItemDto>> Handle(
        GetRelatedVisitorsQuery request, CancellationToken cancellationToken)
    {
        var campusId = RelatedVisitorScope.EnsureStaffLeaderCampus(_currentUser);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : (request.PageSize > 100 ? 100 : request.PageSize);

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy)
            ? "lastrelatedrequestat"
            : request.SortBy!.Trim().ToLowerInvariant();
        if (!AllowedSortColumns.Contains(sortBy))
            sortBy = "lastrelatedrequestat";
        var ascending = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword!.Trim().ToLower();

        // ── Step A: the campus's visible visit instances (simple join+where+select). ──
        var instances = await RelatedVisitorScope.VisibleInstances(_db, campusId)
            .Select(vrc => new InstanceRow
            {
                VisitorUserId = vrc.OperationalContactUserId!.Value,
                VisitRequestId = vrc.VisitRequestId,
                SubmittedAt = vrc.VisitRequest.SubmittedAt,
                PlannedStartAt = vrc.PlannedStartAt,
            })
            .ToListAsync(cancellationToken);

        // ── Step B: aggregate per Visitor in memory (count distinct requests, latest timestamps). ──
        var aggregates = instances
            .GroupBy(x => x.VisitorUserId)
            .ToDictionary(g => g.Key, g => new Aggregate
            {
                RelatedRequestCount = g.Select(x => x.VisitRequestId).Distinct().Count(),
                LastRelatedRequestAt = g.Max(x => x.SubmittedAt),
                LatestPlannedStartAt = g.Max(x => x.PlannedStartAt),
            });

        var visitorIds = aggregates.Keys.ToList();
        if (visitorIds.Count == 0)
            return PaginatedResult<RelatedVisitorAccountListItemDto>.Create(
                new List<RelatedVisitorAccountListItemDto>(), page, pageSize, 0);

        // ── Step C: load the related Visitor accounts (BR-02 shape) + apply status/keyword in DB. ──
        var userQuery = _db.Users.AsNoTracking()
            .Where(u => visitorIds.Contains(u.UserId)
                        && u.Role.RoleCode == RoleCodes.Visitor
                        && u.PrimaryCampusId == null
                        && u.DepartmentId == null
                        && u.SubRole == null);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var st = request.Status.Trim().ToUpperInvariant();
            userQuery = userQuery.Where(u => u.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(request.Nationality))
        {
            // Normalized on BOTH sides: the dropdown value and the stored value are compared
            // trimmed and lower-cased, so " Nhật Bản " / "nhật bản" / "NHẬT BẢN" all select the
            // same option. Deliberately not left to the column collation — that would make the
            // filter's behaviour depend on a DB setting no test here pins down.
            var nat = request.Nationality.Trim().ToLower();
            userQuery = userQuery.Where(u => u.Nationality != null && u.Nationality.Trim().ToLower() == nat);
        }

        if (keyword is { Length: > 0 })
        {
            userQuery = userQuery.Where(u =>
                u.Email.ToLower().Contains(keyword)
                || u.FullName.ToLower().Contains(keyword)
                || (u.Phone != null && u.Phone.Contains(keyword))
                || (u.Nationality != null && u.Nationality.ToLower().Contains(keyword)));
        }

        var users = await userQuery
            .Select(u => new UserRow
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                Nationality = u.Nationality,
                Status = u.Status,
                CreatedVia = u.CreatedVia,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
            })
            .ToListAsync(cancellationToken);

        // ── Step D: combine + sort + page in memory (the set is bounded to one campus). ──
        var combined = users.Select(u =>
        {
            var agg = aggregates.TryGetValue(u.UserId, out var a) ? a : new Aggregate();
            return new RelatedVisitorAccountListItemDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                Nationality = u.Nationality,
                RoleCode = RoleCodes.Visitor,
                Status = u.Status,
                CreatedVia = u.CreatedVia,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                RelatedRequestCount = agg.RelatedRequestCount,
                LastRelatedRequestAt = agg.LastRelatedRequestAt,
                LatestPlannedStartAt = agg.LatestPlannedStartAt,
                // Read-only tab (BR-04): no management capability is ever granted here.
                CanViewDetails = true,
                CanManageStatus = false,
                CanUpdateRole = false,
                CanResetPassword = false,
            };
        });

        IEnumerable<RelatedVisitorAccountListItemDto> sorted = sortBy switch
        {
            "fullname" => ascending
                ? combined.OrderBy(r => r.FullName).ThenBy(r => r.UserId)
                : combined.OrderByDescending(r => r.FullName).ThenBy(r => r.UserId),
            "createdat" => ascending
                ? combined.OrderBy(r => r.CreatedAt).ThenBy(r => r.UserId)
                : combined.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.UserId),
            "lastloginat" => ascending
                ? combined.OrderBy(r => r.LastLoginAt).ThenBy(r => r.UserId)
                : combined.OrderByDescending(r => r.LastLoginAt).ThenBy(r => r.UserId),
            "latestplannedstartat" => ascending
                ? combined.OrderBy(r => r.LatestPlannedStartAt).ThenBy(r => r.UserId)
                : combined.OrderByDescending(r => r.LatestPlannedStartAt).ThenBy(r => r.UserId),
            _ => ascending
                ? combined.OrderBy(r => r.LastRelatedRequestAt).ThenBy(r => r.CreatedAt)
                : combined.OrderByDescending(r => r.LastRelatedRequestAt).ThenByDescending(r => r.CreatedAt),
        };

        var ordered = sorted.ToList();
        var totalItems = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return PaginatedResult<RelatedVisitorAccountListItemDto>.Create(items, page, pageSize, totalItems);
    }

    private sealed class InstanceRow
    {
        public ulong VisitorUserId { get; init; }
        public ulong VisitRequestId { get; init; }
        public DateTime SubmittedAt { get; init; }
        public DateTime PlannedStartAt { get; init; }
    }

    private sealed class Aggregate
    {
        public int RelatedRequestCount { get; init; }
        public DateTime? LastRelatedRequestAt { get; init; }
        public DateTime? LatestPlannedStartAt { get; init; }
    }

    private sealed class UserRow
    {
        public ulong UserId { get; init; }
        public string FullName { get; init; } = default!;
        public string Email { get; init; } = default!;
        public string? Phone { get; init; }
        public string? Nationality { get; init; }
        public string Status { get; init; } = default!;
        public string? CreatedVia { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastLoginAt { get; init; }
    }
}
