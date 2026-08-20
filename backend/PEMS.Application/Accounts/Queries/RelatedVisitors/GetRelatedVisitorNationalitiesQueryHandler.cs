using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// Builds the nationality filter options of the Staff Leader's related-Visitor tab from the FULL
/// set of Visitors related to the caller's campus.
///
/// It exists because the options cannot be derived from the paged list endpoint: reading the first
/// page only would hide every nationality that happens to appear beyond it. Scope comes from the
/// same <see cref="RelatedVisitorScope.VisibleInstances"/> predicate the list and detail use, so a
/// nationality can never be offered for a campus whose Visitors the caller cannot see.
/// </summary>
public sealed class GetRelatedVisitorNationalitiesQueryHandler
    : IRequestHandler<GetRelatedVisitorNationalitiesQuery, RelatedVisitorNationalitiesDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetRelatedVisitorNationalitiesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<RelatedVisitorNationalitiesDto> Handle(
        GetRelatedVisitorNationalitiesQuery request, CancellationToken cancellationToken)
    {
        var campusId = RelatedVisitorScope.EnsureStaffLeaderCampus(_currentUser);

        // Distinct Visitor ids of the campus, materialised first: the same batched-Contains shape
        // the list handler uses, which avoids the correlated subqueries Pomelo/MySQL translates
        // poorly.
        var visitorIds = await RelatedVisitorScope.VisibleInstances(_db, campusId)
            .Select(vrc => vrc.OperationalContactUserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (visitorIds.Count == 0)
            return new RelatedVisitorNationalitiesDto { Items = new List<string>() };

        var rawNationalities = await _db.Users.AsNoTracking()
            .Where(u => visitorIds.Contains(u.UserId)
                        // Same BR-02 account shape as the list/detail: a user whose role was moved
                        // to an internal one is no longer a Visitor of this tab.
                        && u.Role.RoleCode == RoleCodes.Visitor
                        && u.PrimaryCampusId == null
                        && u.DepartmentId == null
                        && u.SubRole == null
                        && u.Nationality != null)
            .Select(u => u.Nationality!)
            .ToListAsync(cancellationToken);

        // Vietnamese-aware, case-insensitive ordering; the ordinal tie-break keeps the output
        // deterministic when two rows differ only by casing (the DB row order must not decide it).
        var displayOrder = StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: true);

        // Patch 4: merge aliases of the SAME real country into one filter option — audited example:
        // "UAE" and "Các Tiểu vương quốc Ả Rập Thống nhất" are one country under two spellings, and
        // without this they showed up as two separate, non-mergeable filter choices. users.nationality
        // itself is never rewritten here (no migration, no bulk-normalize) — only what the dropdown
        // OFFERS changes; GetRelatedVisitorsQueryHandler is what widens the WHERE clause so picking the
        // merged option still matches every raw spelling behind it. A value that does not resolve to a
        // real country (never observed in the audited data, but not assumed impossible) passes through
        // unchanged, exactly as before.
        var items = rawNationalities
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)                       // drops "" and whitespace-only rows
            .Select(x => CountryName.TryResolve(x, out var canonical) ? canonical! : x)
            .OrderBy(x => x, displayOrder)
            .ThenBy(x => x, StringComparer.Ordinal)
            // Runs AFTER the sort, so "Nhật Bản" / " nhật bản " collapse to a single, stable option.
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RelatedVisitorNationalitiesDto { Items = items };
    }
}
