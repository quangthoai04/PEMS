using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.AgendaTemplates.Queries.ViewAgendaTemplateDetail;

public sealed class ViewAgendaTemplateDetailQueryHandler
    : IRequestHandler<ViewAgendaTemplateDetailQuery, ViewAgendaTemplateDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewAgendaTemplateDetailQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ViewAgendaTemplateDetailDto> Handle(
        ViewAgendaTemplateDetailQuery request, CancellationToken cancellationToken)
    {
        var template = await _db.AgendaTemplates.AsNoTracking()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.AgendaTemplateId == request.AgendaTemplateId, cancellationToken)
            ?? throw new NotFoundException("AgendaTemplate", request.AgendaTemplateId);

        AgendaTemplateAuthorization.EnsureCanViewScope(_currentUser, template.CampusId);

        var isDefault = await _db.AgendaTemplateDefaults.AsNoTracking()
            .AnyAsync(d => d.AgendaTemplateId == template.AgendaTemplateId, cancellationToken);

        var items = template.Items
            .OrderBy(i => i.StartOffsetMinutes).ThenBy(i => i.DisplayOrder)
            .Select(i => new AgendaTemplateItemView(
                i.AgendaTemplateItemId,
                (int)i.DisplayOrder,
                (int)i.StartOffsetMinutes,
                (int)i.DurationMinutes,
                i.Title,
                i.Description,
                i.Location,
                i.ResponsibleRoleLabel))
            .ToList();

        return new ViewAgendaTemplateDetailDto(
            template.AgendaTemplateId,
            template.CampusId,
            template.CampusScopeKey,
            template.VisitType,
            template.Name,
            template.Description,
            template.Status,
            template.DeletedAt != null,
            isDefault,
            items);
    }
}
