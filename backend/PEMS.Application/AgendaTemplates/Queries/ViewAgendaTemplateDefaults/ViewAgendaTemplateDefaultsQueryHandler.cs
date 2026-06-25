using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDefaults;

public sealed class ViewAgendaTemplateDefaultsQueryHandler
    : IRequestHandler<ViewAgendaTemplateDefaultsQuery, ViewAgendaTemplateDefaultsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewAgendaTemplateDefaultsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ViewAgendaTemplateDefaultsDto> Handle(
        ViewAgendaTemplateDefaultsQuery request, CancellationToken cancellationToken)
    {
        AgendaTemplateAuthorization.EnsureCanViewManagement(_currentUser);

        var query =
            from d in _db.AgendaTemplateDefaults.AsNoTracking()
            join t in _db.AgendaTemplates.AsNoTracking() on d.AgendaTemplateId equals t.AgendaTemplateId
            select new { d, t };

        if (!AgendaTemplateAuthorization.IsHo(_currentUser))
        {
            var myCampus = _currentUser.PrimaryCampusId;
            query = query.Where(x => x.d.CampusId == null || x.d.CampusId == myCampus);
        }

        if (request.CampusId is not null)
            query = query.Where(x => x.d.CampusId == request.CampusId);

        var rows = await query
            .OrderBy(x => x.d.CampusScopeKey).ThenBy(x => x.d.VisitType)
            .Select(x => new AgendaTemplateDefaultRow(
                x.d.AgendaTemplateDefaultId,
                x.d.CampusId,
                x.d.CampusScopeKey,
                x.d.VisitType,
                x.d.AgendaTemplateId,
                x.t.Name,
                x.t.Status,
                x.t.DeletedAt != null))
            .ToListAsync(cancellationToken);

        return new ViewAgendaTemplateDefaultsDto(rows, rows.Count);
    }
}
