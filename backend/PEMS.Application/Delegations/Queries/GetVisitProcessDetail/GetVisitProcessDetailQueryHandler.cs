using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetVisitProcessDetail;

public sealed class GetVisitProcessDetailQueryHandler
    : IRequestHandler<GetVisitProcessDetailQuery, VisitProcessDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitProcessDetailQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<VisitProcessDetailDto> Handle(
        GetVisitProcessDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId
                                      && c.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var visit = instance.VisitRequest;

        var acceptedParticipantRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                        && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = roleCode == RoleCodes.Staff
            && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && _currentUser.PrimaryCampusId == instance.CampusId;
        bool isHo = roleCode == RoleCodes.Ho;
        bool isVisitorOwner = roleCode == RoleCodes.Visitor && visit.VisitorUserId == userId;
        bool isAcceptedParticipant = acceptedParticipantRole != null;

        if (!(isHost || isStaffLeaderOfCampus || isHo || isVisitorOwner || isAcceptedParticipant))
            throw new ForbiddenException("Bạn không có quyền xem chi tiết tiếp khách này.");

        var relation = isHost ? "HOST"
            : isStaffLeaderOfCampus ? "STAFF_LEADER"
            : isHo ? "HO"
            : isVisitorOwner ? "VISITOR_OWNER"
            : acceptedParticipantRole switch
            {
                ParticipantRoles.IcSupport => "IC_SUPPORT",
                ParticipantRoles.DeptSupport => "DEPT_SUPPORT",
                ParticipantRoles.Student => "STUDENT",
                _ => "NONE",
            };

        bool isLive = instance.Status != VisitInstanceStatus.Closed
            && instance.Status != VisitInstanceStatus.Cancelled;
        bool canEditBefore = isHost && isLive
            && (instance.Status == VisitInstanceStatus.Assigned || instance.Status == VisitInstanceStatus.BeforeVisit);

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        string? hostName = null;
        if (instance.CurrentHostUserId.HasValue)
        {
            hostName = await _db.Users
                .Where(u => u.UserId == instance.CurrentHostUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var agenda = await _db.VisitAgendas
            .Where(a => a.VisitInstanceId == instance.VisitInstanceId)
            .OrderBy(a => a.SequenceOrder).ThenBy(a => a.StartTime)
            .Select(a => new AgendaItemDto
            {
                AgendaId = a.AgendaId,
                Title = a.Title,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Description = a.Description,
                Location = a.Location,
                SourceTemplateItemId = a.SourceTemplateItemId,
                ResponsibleUserId = a.ResponsibleUserId,
            })
            .ToListAsync(cancellationToken);

        // Enrich in-memory (avoid correlated subqueries on optional FKs — Pomelo translation pitfall):
        //   responsible_user_id            → users (real assigned person: name + email)
        //   source_template_item_id        → agenda_template_items.responsible_role_label (suggested role)
        var responsibleUserIds = agenda
            .Where(a => a.ResponsibleUserId.HasValue)
            .Select(a => a.ResponsibleUserId!.Value).Distinct().ToList();
        if (responsibleUserIds.Count > 0)
        {
            var userById = (await _db.Users
                    .Where(u => responsibleUserIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.FullName, u.Email })
                    .ToListAsync(cancellationToken))
                .ToDictionary(u => u.UserId);
            foreach (var a in agenda)
            {
                if (a.ResponsibleUserId.HasValue && userById.TryGetValue(a.ResponsibleUserId.Value, out var u))
                {
                    a.ResponsibleUserName = u.FullName;
                    a.ResponsibleUserEmail = u.Email;
                }
            }
        }

        var sourceTemplateItemIds = agenda
            .Where(a => a.SourceTemplateItemId.HasValue)
            .Select(a => a.SourceTemplateItemId!.Value).Distinct().ToList();
        if (sourceTemplateItemIds.Count > 0)
        {
            var labelByItemId = (await _db.AgendaTemplateItems
                    .Where(ti => sourceTemplateItemIds.Contains(ti.AgendaTemplateItemId))
                    .Select(ti => new { ti.AgendaTemplateItemId, ti.ResponsibleRoleLabel })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.AgendaTemplateItemId, x => x.ResponsibleRoleLabel);
            foreach (var a in agenda)
            {
                if (a.SourceTemplateItemId.HasValue
                    && labelByItemId.TryGetValue(a.SourceTemplateItemId.Value, out var label))
                {
                    a.TemplateResponsibleRoleLabel = label;
                }
            }
        }

        return new VisitProcessDetailDto
        {
            VisitRequestId = instance.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = visit.DelegationName,
            InstanceStatus = instance.Status,
            PlannedStartAt = instance.PlannedStartAt,
            PlannedEndAt = instance.PlannedEndAt,
            CampusName = campusName,
            HostUserId = instance.CurrentHostUserId,
            HostName = hostName,
            Relation = relation,
            CanEditBefore = canEditBefore,
            Agenda = agenda,
        };
    }
}
