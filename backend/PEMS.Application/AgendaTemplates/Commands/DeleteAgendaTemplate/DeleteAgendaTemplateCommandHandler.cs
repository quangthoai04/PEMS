using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.AgendaTemplates.Commands.DeleteAgendaTemplate;

public sealed class DeleteAgendaTemplateCommandHandler
    : IRequestHandler<DeleteAgendaTemplateCommand, DeleteAgendaTemplateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public DeleteAgendaTemplateCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DeleteAgendaTemplateResponse> Handle(
        DeleteAgendaTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.AgendaTemplates
            .FirstOrDefaultAsync(t => t.AgendaTemplateId == request.AgendaTemplateId, cancellationToken)
            ?? throw new NotFoundException("AgendaTemplate", request.AgendaTemplateId);

        if (template.DeletedAt != null)
            throw new ConflictException("Mẫu agenda đã bị xóa trước đó.", "AGENDA_TEMPLATE_DELETED");

        AgendaTemplateAuthorization.EnsureCanManageScope(_currentUser, template.CampusId);
        var actorId = _currentUser.UserId!.Value;

        // A template that is currently set as a default must be unset first (keeps defaults consistent
        // and avoids the agenda_template_defaults RESTRICT FK becoming a dangling soft-deleted target).
        var isDefault = await _db.AgendaTemplateDefaults
            .AnyAsync(d => d.AgendaTemplateId == template.AgendaTemplateId, cancellationToken);
        if (isDefault)
            throw new ConflictException(
                "Mẫu agenda đang được đặt làm mặc định. Hãy đổi mặc định sang mẫu khác trước khi xóa.",
                "AGENDA_TEMPLATE_IS_DEFAULT");

        var now = _clock.UtcNow;
        template.DeletedAt = now;
        template.DeletedBy = actorId;
        template.UpdatedAt = now;
        template.UpdatedBy = actorId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "DELETE_AGENDA_TEMPLATE",
            EntityType = "AgendaTemplate",
            EntityId = template.AgendaTemplateId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new DeleteAgendaTemplateResponse(template.AgendaTemplateId, "Đã xóa mẫu agenda.");
    }
}
