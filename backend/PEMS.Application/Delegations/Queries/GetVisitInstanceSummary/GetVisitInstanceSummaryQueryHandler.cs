using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Application.Delegations.Queries.GetVisitProcessDetail;
using PEMS.Application.Delegations.Queries.GetVisitInstanceContribution;
using PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Enums;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceSummary;

public sealed class GetVisitInstanceSummaryQueryHandler : IRequestHandler<GetVisitInstanceSummaryQuery, ProcessSummaryPageDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInstanceSummaryQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ProcessSummaryPageDto> Handle(GetVisitInstanceSummaryQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;
        var userCampusId = _currentUser.PrimaryCampusId;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest).ThenInclude(v => v.CampusInstances)
            .Include(c => c.VisitRequest).ThenInclude(v => v.GuestMembers)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var visit = instance.VisitRequest;

        bool isStaffLeaderOfCampus = roleCode == RoleCodes.Staff
            && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && userCampusId == instance.CampusId;

        bool isHo = roleCode == RoleCodes.Ho;
        
        bool isHost = instance.CurrentHostUserId == userId;

        if (!(isStaffLeaderOfCampus || isHo || isHost))
        {
            throw new ForbiddenException("Bạn không có quyền xem thông tin tóm tắt chuyến thăm này.");
        }

        string relation = isHost ? "HOST" : (isHo ? "HO" : "STAFF_LEADER");

        // Campus name
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        // Host name
        string? hostName = null;
        if (instance.CurrentHostUserId.HasValue)
        {
            hostName = await _db.Users.Where(u => u.UserId == instance.CurrentHostUserId.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
        }

        // Mapping RequestSummary
        var requestCampusIds = visit.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        var campusNamesById = requestCampusIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Campuses
                .Where(c => requestCampusIds.Contains(c.CampusId))
                .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var requestSummary = new VisitProcessRequestSummaryDto
        {
            RegistrantName = visit.RegistrantFullName,
            RegistrantEmail = visit.RegistrantEmail,
            RegistrantPhone = visit.RegistrantPhone,
            RegistrantOrganization = visit.RegistrantOrganization,
            RegistrantJobTitle = visit.RegistrantJobTitle,
            RegistrantNationality = visit.RegistrantNationality,

            DelegationName = visit.DelegationName,
            VisitScope = visit.VisitScope,
            VisitType = visit.VisitType,
            VisitTypeOther = visit.VisitTypeOther,
            Purpose = visit.Purpose,
            WorkingContent = visit.WorkingContent,
            WorkingLanguage = visit.WorkingLanguage,
            MediaConsentStatus = visit.MediaConsentStatus,
            MediaConsentNote = visit.MediaConsentNote,
            TransportationType = visit.TransportationType,
            TransportationDetail = visit.TransportationDetail,
            NoteToFptu = visit.NoteToFptu,

            ContactPersonFullName = visit.ContactPersonFullName,
            ContactPersonOrganization = visit.ContactPersonOrganization,
            ContactPersonPhone = visit.ContactPersonPhone,
            ContactPersonEmail = visit.ContactPersonEmail,

            Campuses = visit.CampusInstances
                .OrderBy(c => c.PlannedStartAt)
                .Select(c => new VisitProcessCampusDto
                {
                    VisitInstanceId = c.VisitInstanceId,
                    CampusId = c.CampusId,
                    CampusName = campusNamesById.TryGetValue(c.CampusId, out var cn) ? cn : string.Empty,
                    PlannedStartAt = c.PlannedStartAt,
                    PlannedEndAt = c.PlannedEndAt,
                    IsCurrent = c.VisitInstanceId == instance.VisitInstanceId,
                })
                .ToList(),

            GuestMembers = visit.GuestMembers
                .Where(m => m.MemberType != "EXTERNAL_SUPPORT")
                .OrderBy(m => m.DisplayOrder)
                .Select(MapGuestMember)
                .ToList(),
            ExternalSupportMembers = visit.GuestMembers
                .Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .OrderBy(m => m.DisplayOrder)
                .Select(MapGuestMember)
                .ToList(),
        };

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

        var participants = await VisitParticipantListBuilder.BuildAsync(_db, instance.VisitInstanceId, cancellationToken);

        var logisticsQuery = from li in _db.VisitLogisticsItems
                             where li.VisitInstanceId == instance.VisitInstanceId && li.Status != LogisticsItemStatus.Cancelled
                             join d in _db.Departments on li.RequestedToDepartmentId equals d.DepartmentId into d_gj
                             from d in d_gj.DefaultIfEmpty()
                             join u in _db.Users on li.AssignedToUserId equals u.UserId into u_gj
                             from u in u_gj.DefaultIfEmpty()
                             select new ContributionLogisticsItemDto
                             {
                                 LogisticsItemId = li.LogisticsItemId,
                                 ItemType = li.ItemType,
                                 Title = li.Title,
                                 Status = li.Status,
                                 Priority = li.Priority,
                                 RequestedToDepartmentId = li.RequestedToDepartmentId,
                                 DepartmentName = d != null ? d.Name : null,
                                 AssignedToUserId = li.AssignedToUserId,
                                 AssignedToName = u != null ? u.FullName : null
                             };
        var logistics = await logisticsQuery.ToListAsync(cancellationToken);

        return new ProcessSummaryPageDto
        {
            Permissions = new ProcessSummaryPermissionDto
            {
                CanViewSummaryPage = true,
                Relation = relation,
                CanViewRequestSummary = true,
                CanViewAgendaSummary = true,
                CanViewParticipantSummary = true,
                CanViewLogisticsSummary = true,
                CanViewMinutesSummary = true,
                CanViewMediaSummary = true,
                CanViewNewsSummary = true,
                CanViewFeedbackSummary = true,
                CanViewTimeline = true,
                IsReadOnly = true,
                InstanceStatus = instance.Status,
                CampusName = campusName,
                DelegationName = visit.DelegationName,
                HostName = hostName,
                PlannedStartAt = instance.PlannedStartAt,
                PlannedEndAt = instance.PlannedEndAt
            },
            RequestSummary = requestSummary,
            AgendaSummary = agenda,
            ParticipantSummary = participants,
            LogisticsSummary = logistics,
            MinutesSummary = new ContributionSectionStatusDto { CanView = true, CanEdit = false, Placeholder = true },
            MediaSummary = new ContributionSectionStatusDto { CanView = true, CanEdit = false, Placeholder = true },
            NewsSummary = new ContributionSectionStatusDto { CanView = true, CanEdit = false, Placeholder = true }
        };
    }

    private static VisitProcessGuestMemberDto MapGuestMember(Domain.Entities.Delegations.VisitGuestMember m) => new()
    {
        GuestMemberId = m.GuestMemberId,
        MemberType = m.MemberType,
        FullName = m.FullName,
        Organization = m.Organization,
        JobTitle = m.JobTitle,
        Nationality = m.Nationality,
        DisplayOrder = (int)m.DisplayOrder,
    };
}
