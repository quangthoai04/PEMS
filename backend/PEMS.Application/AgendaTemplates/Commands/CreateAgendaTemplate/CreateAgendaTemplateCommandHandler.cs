using System.Linq;
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

namespace PEMS.Application.AgendaTemplates.Commands.CreateAgendaTemplate;

public sealed class CreateAgendaTemplateCommandHandler
    : IRequestHandler<CreateAgendaTemplateCommand, CreateAgendaTemplateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CreateAgendaTemplateCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CreateAgendaTemplateResponse> Handle(
        CreateAgendaTemplateCommand request, CancellationToken cancellationToken)
    {
        AgendaTemplateAuthorization.EnsureCanManageScope(_currentUser, request.CampusId);
        var actorId = _currentUser.UserId!.Value;

        if (request.CampusId is not null)
        {
            var campusActive = await _db.Campuses.AnyAsync(
                c => c.CampusId == request.CampusId && c.Status == EntityStatuses.Active, cancellationToken);
            if (!campusActive)
                throw new BusinessRuleException("Cơ sở không tồn tại hoặc đang ngừng hoạt động.", "CAMPUS_INACTIVE");
        }

        var scopeKey = AgendaScope.KeyFor(request.CampusId);
        var name = request.Name.Trim();

        var duplicate = await _db.AgendaTemplates.AnyAsync(
            t => t.CampusScopeKey == scopeKey
                 && t.VisitType == request.VisitType
                 && t.Name == name
                 && t.DeletedAt == null,
            cancellationToken);
        if (duplicate)
            throw new ConflictException(
                "Đã tồn tại mẫu agenda cùng tên trong phạm vi và loại hình visit này.",
                "AGENDA_TEMPLATE_DUPLICATE");

        var now = _clock.UtcNow;
        var template = new AgendaTemplate
        {
            CampusId = request.CampusId,
            CampusScopeKey = scopeKey,
            VisitType = request.VisitType,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = request.Status,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        foreach (var i in request.Items.OrderBy(i => i.DisplayOrder))
        {
            template.Items.Add(new AgendaTemplateItem
            {
                DisplayOrder = (uint)i.DisplayOrder,
                StartOffsetMinutes = (uint)i.StartOffsetMinutes,
                DurationMinutes = (uint)i.DurationMinutes,
                Title = i.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim(),
                Location = string.IsNullOrWhiteSpace(i.Location) ? null : i.Location.Trim(),
                ResponsibleRoleLabel = string.IsNullOrWhiteSpace(i.ResponsibleRoleLabel) ? null : i.ResponsibleRoleLabel.Trim(),
                CreatedAt = now,
                CreatedBy = actorId,
            });
        }

        _db.AgendaTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "CREATE_AGENDA_TEMPLATE",
            EntityType = "AgendaTemplate",
            EntityId = template.AgendaTemplateId,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateAgendaTemplateResponse(template.AgendaTemplateId, template.Items.Count, "Đã tạo mẫu agenda.");
    }
}
