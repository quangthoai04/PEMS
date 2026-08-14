using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.SearchInternalPartnerOptions;

public sealed class SearchInternalPartnerOptionsQueryHandler
    : IRequestHandler<SearchInternalPartnerOptionsQuery, IReadOnlyList<InternalPartnerOptionDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SearchInternalPartnerOptionsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<InternalPartnerOptionDto>> Handle(
        SearchInternalPartnerOptionsQuery request, CancellationToken cancellationToken)
    {
        // The endpoint is [Authorize]d, but role is a separate question from authentication: a Visitor
        // account holds a token too, and must stay on the public option set.
        if (!GuestOrganizationPartnerPolicy.IsInternalAudience(_currentUser))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền tra cứu danh sách đối tác nội bộ.", 403);

        var keyword = request.Keyword?.Trim();
        var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, 50);

        // Same free-solo contract as the public combobox: no keyword, no dropdown — what the user
        // typed stays free text instead of the whole partner table falling open.
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2)
            return Array.Empty<InternalPartnerOptionDto>();

        var pattern = $"%{keyword}%";
        var scoped = GuestOrganizationPartnerPolicy.InternalSelectable(
            _db.Partners.AsNoTracking(), _currentUser.PrimaryCampusId);

        var rows = await scoped
            .Where(p =>
                EF.Functions.Like(p.Name, pattern)
                || (p.ShortName != null && EF.Functions.Like(p.ShortName, pattern))
                || (p.PartnerCode != null && EF.Functions.Like(p.PartnerCode, pattern))
                || (p.Country != null && EF.Functions.Like(p.Country, pattern))
                || (p.City != null && EF.Functions.Like(p.City, pattern)))
            // Approved first: an approved profile is the safer pick when both are plausible, and a
            // pending one is offered as the exception rather than the default.
            .OrderByDescending(p => p.ProfileStatus == PartnerProfileStatuses.Approved)
            .ThenBy(p => p.Name)
            .Take(limit)
            .Select(p => new
            {
                p.PartnerId, p.Name, p.ShortName, p.Country, p.City,
                p.PartnerType, p.ProfileStatus, p.OwnerCampusId,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return Array.Empty<InternalPartnerOptionDto>();

        var campusIds = rows.Select(r => r.OwnerCampusId).Distinct().ToList();
        var campusNames = await _db.Campuses.AsNoTracking()
            .Where(c => campusIds.Contains(c.CampusId))
            .Select(c => new { c.CampusId, c.Name })
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        return rows.Select(p => new InternalPartnerOptionDto
        {
            PartnerId = p.PartnerId,
            Name = p.Name,
            ShortName = p.ShortName,
            Country = p.Country,
            City = p.City,
            PartnerType = p.PartnerType,
            DisplayName = string.IsNullOrWhiteSpace(p.ShortName) ? p.Name : $"{p.Name} ({p.ShortName})",
            ProfileStatus = p.ProfileStatus,
            OwnerCampusId = p.OwnerCampusId,
            OwnerCampusName = campusNames.TryGetValue(p.OwnerCampusId, out var cn) ? cn : null,
        }).ToList();
    }
}
