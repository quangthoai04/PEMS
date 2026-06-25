using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.AgendaTemplates;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.AgendaTemplates.Commands.SetAgendaTemplateDefault;

public sealed class SetAgendaTemplateDefaultCommandHandler
    : IRequestHandler<SetAgendaTemplateDefaultCommand, SetAgendaTemplateDefaultResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public SetAgendaTemplateDefaultCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<SetAgendaTemplateDefaultResponse> Handle(
        SetAgendaTemplateDefaultCommand request, CancellationToken cancellationToken)
    {
        AgendaTemplateAuthorization.EnsureCanManageScope(_currentUser, request.CampusId);
        var actorId = _currentUser.UserId!.Value;

        var scopeKey = AgendaScope.KeyFor(request.CampusId);

        var template = await _db.AgendaTemplates
            .FirstOrDefaultAsync(t => t.AgendaTemplateId == request.AgendaTemplateId, cancellationToken)
            ?? throw new NotFoundException("AgendaTemplate", request.AgendaTemplateId);

        if (template.DeletedAt != null)
            throw new BusinessRuleException("Không thể đặt mẫu đã bị xóa làm mặc định.", "AGENDA_TEMPLATE_DELETED");
        if (template.Status != AgendaTemplateStatuses.Active)
            throw new BusinessRuleException("Chỉ mẫu agenda đang ACTIVE mới được đặt làm mặc định.", "AGENDA_TEMPLATE_INACTIVE");
        if (template.VisitType != request.VisitType)
            throw new BusinessRuleException("Mặc định phải trỏ tới mẫu cùng loại hình visit.", "AGENDA_TEMPLATE_VISIT_TYPE_MISMATCH");
        if (template.CampusScopeKey != scopeKey)
            throw new BusinessRuleException("Mặc định phải cùng phạm vi (campus/GLOBAL) với mẫu agenda.", "AGENDA_TEMPLATE_SCOPE_MISMATCH");

        var now = _clock.UtcNow;

        var existing = await _db.AgendaTemplateDefaults
            .FirstOrDefaultAsync(d => d.CampusScopeKey == scopeKey && d.VisitType == request.VisitType, cancellationToken);

        AgendaTemplateDefault entity;
        if (existing is null)
        {
            entity = new AgendaTemplateDefault
            {
                CampusId = request.CampusId,
                CampusScopeKey = scopeKey,
                VisitType = request.VisitType,
                AgendaTemplateId = template.AgendaTemplateId,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            _db.AgendaTemplateDefaults.Add(entity);
        }
        else
        {
            entity = existing;
            entity.CampusId = request.CampusId;
            entity.AgendaTemplateId = template.AgendaTemplateId;
            entity.UpdatedAt = now;
            entity.UpdatedBy = actorId;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "SET_AGENDA_TEMPLATE_DEFAULT",
            EntityType = "AgendaTemplate",
            EntityId = template.AgendaTemplateId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new SetAgendaTemplateDefaultResponse(
            entity.AgendaTemplateDefaultId, entity.CampusId, entity.CampusScopeKey,
            entity.VisitType, entity.AgendaTemplateId, "Đã đặt mẫu agenda mặc định.");
    }
}
