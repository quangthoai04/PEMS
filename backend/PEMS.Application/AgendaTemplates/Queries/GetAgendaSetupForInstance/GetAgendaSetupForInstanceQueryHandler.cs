using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.AgendaTemplates.Queries.GetAgendaSetupForInstance;

public sealed class GetAgendaSetupForInstanceQueryHandler
    : IRequestHandler<GetAgendaSetupForInstanceQuery, GetAgendaSetupForInstanceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAgendaSetupForInstanceQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetAgendaSetupForInstanceDto> Handle(
        GetAgendaSetupForInstanceQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // visit_type comes from the original submitted form, never from visit_request_campuses.
        var visitType = instance.VisitRequest.VisitType;

        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = _currentUser.RoleCode == RoleCodes.Staff
            && string.Equals(_currentUser.SubRole, UserSubRoles.Leader, System.StringComparison.OrdinalIgnoreCase)
            && _currentUser.PrimaryCampusId == instance.CampusId;
        bool isHo = _currentUser.RoleCode == RoleCodes.Ho;

        if (!isHost && !isStaffLeaderOfCampus && !isHo)
            throw new ForbiddenException("Bạn không có quyền thiết lập lịch trình cho cơ sở này.");

        var relation = isHost ? "HOST" : isStaffLeaderOfCampus ? "STAFF_LEADER" : "HO";

        bool isLive = instance.Status != VisitInstanceStatus.Cancelled && instance.Status != VisitInstanceStatus.Closed;
        // Applying/editing the agenda is the host's job during the preparation window only.
        bool canApply = isHost && isLive
            && (instance.Status == VisitInstanceStatus.Assigned || instance.Status == VisitInstanceStatus.BeforeVisit);

        var (defaultTemplateId, defaultScope) = await AgendaDefaultResolver.ResolveAsync(
            _db, instance.CampusId, visitType, cancellationToken);

        // Selectable templates: campus-scoped + GLOBAL, ACTIVE, not deleted (any visit type, so the host
        // can deliberately pick a different one). Item counts via a set-based group query.
        var scopeKey = AgendaScope.KeyFor(instance.CampusId);
        var templates = await _db.AgendaTemplates.AsNoTracking()
            .Where(t => t.DeletedAt == null
                        && t.Status == AgendaTemplateStatuses.Active
                        && (t.CampusScopeKey == scopeKey || t.CampusScopeKey == AgendaScope.Global))
            .OrderBy(t => t.VisitType == visitType ? 0 : 1)
            .ThenBy(t => t.CampusScopeKey)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var ids = templates.Select(t => t.AgendaTemplateId).ToList();
        var itemCounts = ids.Count == 0
            ? new Dictionary<ulong, int>()
            : await _db.AgendaTemplateItems.AsNoTracking()
                .Where(i => ids.Contains(i.AgendaTemplateId))
                .GroupBy(i => i.AgendaTemplateId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var selectable = templates.Select(t => new AgendaTemplateSummary(
            t.AgendaTemplateId, t.CampusId, t.CampusScopeKey, t.VisitType, t.Name, t.Description, t.Status,
            itemCounts.TryGetValue(t.AgendaTemplateId, out var c) ? c : 0,
            defaultTemplateId == t.AgendaTemplateId)).ToList();

        var current = await _db.VisitAgendas.AsNoTracking()
            .Where(a => a.VisitInstanceId == instance.VisitInstanceId)
            .OrderBy(a => a.SequenceOrder)
            .Select(a => new AgendaRowView(
                a.AgendaId, a.SequenceOrder, a.Title, a.StartTime, a.EndTime,
                a.Location, a.Description, a.SourceTemplateItemId))
            .ToListAsync(cancellationToken);

        return new GetAgendaSetupForInstanceDto(
            instance.VisitInstanceId,
            instance.VisitRequestId,
            instance.CampusId,
            visitType,
            instance.PlannedStartAt,
            instance.PlannedEndAt,
            relation,
            canApply,
            defaultTemplateId,
            defaultScope,
            current.Count > 0,
            selectable,
            current);
    }
}
