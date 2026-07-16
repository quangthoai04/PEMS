using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AgendaTemplates.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.AgendaTemplates.Queries.GetAgendaSetupForInstance;

public sealed class GetAgendaSetupForInstanceQueryHandler
    : IRequestHandler<GetAgendaSetupForInstanceQuery, GetAgendaSetupForInstanceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitFormReadService _formReadService;

    public GetAgendaSetupForInstanceQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
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

        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = _currentUser.RoleCode == RoleCodes.Staff
            && string.Equals(_currentUser.SubRole, UserSubRoles.Leader, System.StringComparison.OrdinalIgnoreCase)
            && _currentUser.PrimaryCampusId == instance.CampusId;
        bool isHo = _currentUser.RoleCode == RoleCodes.Ho;

        if (!isHost && !isStaffLeaderOfCampus && !isHo)
            throw new ForbiddenException("Bạn không có quyền thiết lập lịch trình cho cơ sở này.");

        var relation = isHost ? "HOST" : isStaffLeaderOfCampus ? "STAFF_LEADER" : "HO";

        // visit_type comes from the submitted form, never from visit_request_campuses. v1 → the global
        // projection on visit_requests; v2 → the TARGET instance's per-campus detail (this setup screen is
        // keyed by one visit_instance_id, so a MIXED request still returns 200 with THIS instance's visit type,
        // never the global field and never a sibling). Resolved AFTER the authorization check above; missing v2
        // detail → 409 VISIT_FORM_DETAIL_MISSING (no global fallback). v1 keeps the global value, byte-identical.
        string visitType = instance.VisitRequest.VisitType;
        if (instance.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus)
        {
            var content = await _formReadService.ResolveCampusFormContentAsync(
                instance.VisitRequest, new[] { instance.VisitInstanceId }, cancellationToken);
            visitType = content[instance.VisitInstanceId].VisitType!;
        }

        bool isLive = instance.Status != VisitInstanceStatus.Cancelled && instance.Status != VisitInstanceStatus.Closed;
        // Applying/editing the agenda is the host's job during the preparation window only.
        bool canApply = isHost && isLive
            && (instance.Status == VisitInstanceStatus.Assigned || instance.Status == VisitInstanceStatus.BeforeVisit);

        var (defaultTemplateId, defaultScope) = await AgendaDefaultResolver.ResolveAsync(
            _db, instance.CampusId, visitType, cancellationToken);

        // Selectable templates: campus-scoped + GLOBAL, ACTIVE, not deleted (any visit type, so the host
        // can deliberately pick a different one). Items are EMBEDDED here so the host can preview each
        // template without calling the management-gated detail endpoint (which a plain-Staff host can't).
        var scopeKey = AgendaScope.KeyFor(instance.CampusId);
        var templates = await _db.AgendaTemplates.AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.DeletedAt == null
                        && t.Status == AgendaTemplateStatuses.Active
                        && (t.CampusScopeKey == scopeKey || t.CampusScopeKey == AgendaScope.Global))
            .OrderBy(t => t.VisitType == visitType ? 0 : 1)
            .ThenBy(t => t.CampusScopeKey)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var selectable = templates.Select(t => new AgendaSetupTemplateOption(
            t.AgendaTemplateId, t.CampusId, t.CampusScopeKey, t.VisitType, t.Name, t.Description, t.Status,
            t.Items.Count,
            defaultTemplateId == t.AgendaTemplateId,
            t.Items
                .OrderBy(i => i.StartOffsetMinutes).ThenBy(i => i.DisplayOrder)
                .Select(i => new AgendaTemplateItemView(
                    i.AgendaTemplateItemId, (int)i.DisplayOrder, (int)i.StartOffsetMinutes, (int)i.DurationMinutes,
                    i.Title, i.Description, i.Location, i.ResponsibleRoleLabel))
                .ToList())).ToList();

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
