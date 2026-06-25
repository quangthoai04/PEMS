using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.AgendaTemplates.Queries.GetDefaultAgendaTemplate;

public sealed class GetDefaultAgendaTemplateQueryHandler
    : IRequestHandler<GetDefaultAgendaTemplateQuery, GetDefaultAgendaTemplateDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetDefaultAgendaTemplateQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetDefaultAgendaTemplateDto> Handle(
        GetDefaultAgendaTemplateQuery request, CancellationToken cancellationToken)
    {
        AgendaTemplateAuthorization.EnsureCanViewManagement(_currentUser);
        if (!VisitTypes.IsValid(request.VisitType))
            throw new BusinessRuleException("Loại hình visit không hợp lệ.", "INVALID_VISIT_TYPE");

        var (templateId, scope) = await AgendaDefaultResolver.ResolveAsync(
            _db, request.CampusId, request.VisitType, cancellationToken);

        if (templateId is null)
            return new GetDefaultAgendaTemplateDto(false, null, null);

        var view = await AgendaDefaultResolver.LoadViewAsync(_db, templateId.Value, cancellationToken);
        return new GetDefaultAgendaTemplateDto(view != null, view != null ? scope : null, view);
    }
}
