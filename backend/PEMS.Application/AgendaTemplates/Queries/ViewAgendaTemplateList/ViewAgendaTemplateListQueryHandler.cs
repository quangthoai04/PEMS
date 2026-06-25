using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateList;

public sealed class ViewAgendaTemplateListQueryHandler
    : IRequestHandler<ViewAgendaTemplateListQuery, ViewAgendaTemplateListDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewAgendaTemplateListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ViewAgendaTemplateListDto> Handle(
        ViewAgendaTemplateListQuery request, CancellationToken cancellationToken)
    {
        AgendaTemplateAuthorization.EnsureCanViewManagement(_currentUser);

        var query = _db.AgendaTemplates.AsNoTracking().AsQueryable();

        if (!request.IncludeDeleted)
            query = query.Where(t => t.DeletedAt == null);

        // Staff Leader is limited to GLOBAL + own campus; HO sees everything.
        if (!AgendaTemplateAuthorization.IsHo(_currentUser))
        {
            var myCampus = _currentUser.PrimaryCampusId;
            query = query.Where(t => t.CampusId == null || t.CampusId == myCampus);
        }

        if (request.CampusId is not null)
            query = query.Where(t => t.CampusId == request.CampusId);
        if (!string.IsNullOrWhiteSpace(request.VisitType))
            query = query.Where(t => t.VisitType == request.VisitType);
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(t => t.Status == request.Status);

        var templates = await query
            .OrderBy(t => t.CampusScopeKey).ThenBy(t => t.VisitType).ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var ids = templates.Select(t => t.AgendaTemplateId).ToList();

        // Item counts + default flags resolved with set-based queries (no per-row correlated subquery).
        var itemCounts = ids.Count == 0
            ? new Dictionary<ulong, int>()
            : await _db.AgendaTemplateItems.AsNoTracking()
                .Where(i => ids.Contains(i.AgendaTemplateId))
                .GroupBy(i => i.AgendaTemplateId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var defaultTemplateIds = ids.Count == 0
            ? new HashSet<ulong>()
            : (await _db.AgendaTemplateDefaults.AsNoTracking()
                .Where(d => ids.Contains(d.AgendaTemplateId))
                .Select(d => d.AgendaTemplateId)
                .ToListAsync(cancellationToken)).ToHashSet();

        var summaries = templates.Select(t => new AgendaTemplateSummary(
            t.AgendaTemplateId,
            t.CampusId,
            t.CampusScopeKey,
            t.VisitType,
            t.Name,
            t.Description,
            t.Status,
            itemCounts.TryGetValue(t.AgendaTemplateId, out var c) ? c : 0,
            defaultTemplateIds.Contains(t.AgendaTemplateId))).ToList();

        return new ViewAgendaTemplateListDto(summaries, summaries.Count);
    }
}
