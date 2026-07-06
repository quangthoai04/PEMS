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
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;

public sealed class GetEditableVisitRequestDetailQueryHandler
    : IRequestHandler<GetEditableVisitRequestDetailQuery, EditableVisitRequestDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public GetEditableVisitRequestDetailQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<EditableVisitRequestDetailDto> Handle(
        GetEditableVisitRequestDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        if (_currentUser.RoleCode != RoleCodes.Visitor)
            throw new ForbiddenException("Chỉ khách (Visitor) mới được sửa/gửi lại đơn đăng ký tham quan.");

        var userId = _currentUser.UserId.Value;

        var visit = await _context.VisitRequests
            .AsNoTracking()
            .Include(v => v.CampusInstances)
            .Include(v => v.GuestMembers)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        if (visit.VisitorUserId != userId)
            throw new ForbiddenException("Bạn chỉ được sửa đơn của chính mình.");

        var instances = visit.CampusInstances;
        // planned_start_at is local wall-clock DATETIME → compare against VietnamNow.
        var vnNow = _clock.VietnamNow;

        var isEditablePending = visit.Status == VisitRequestStatuses.PendingApproval
            && instances.Count > 0
            && instances.All(i => i.Status == VisitInstanceStatus.WaitingRequestApproval)
            && instances.Min(i => i.PlannedStartAt) >= vnNow.AddHours(24);
        var isResubmittable = visit.Status == VisitRequestStatuses.Rejected
            && instances.Count > 0
            && instances.All(i => i.Status == VisitInstanceStatus.Rejected);

        if (!isEditablePending && !isResubmittable)
            throw new BusinessRuleException(
                "Đơn không còn ở trạng thái có thể sửa hoặc gửi lại (đã có cơ sở ra quyết định, đơn đã hủy, hoặc lịch còn dưới 24 giờ).",
                VisitRequestErrorCodes.VisitRequestNotEditable);

        var campusIds = instances.Select(i => i.CampusId).Distinct().ToList();
        var campuses = campusIds.Count == 0
            ? new Dictionary<ulong, (string Code, string Name)>()
            : await _context.Campuses
                .Where(c => campusIds.Contains(c.CampusId))
                .Select(c => new { c.CampusId, c.CampusCode, c.Name })
                .ToDictionaryAsync(c => c.CampusId, c => (Code: c.CampusCode, Name: c.Name), cancellationToken);

        var deciderIds = instances.Where(i => i.DecidedBy.HasValue).Select(i => i.DecidedBy!.Value).Distinct().ToList();
        var deciderNames = deciderIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _context.Users
                .Where(u => deciderIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        // Resolve the linked partner (if any) so the FE can decide whether the EXISTING_PARTNER
        // link is still valid — a partner that is no longer ACTIVE+APPROVED must NOT block the
        // edit form (the FE falls back to the free-text org and clears partner_id).
        string? partnerName = null;
        bool partnerIsActive = false;
        string? partnerProfileStatus = null;
        if (visit.PartnerId.HasValue)
        {
            var partner = await _context.Partners
                .AsNoTracking()
                .Where(p => p.PartnerId == visit.PartnerId.Value)
                .Select(p => new { p.Name, p.ShortName, p.CooperationStatus, p.ProfileStatus })
                .FirstOrDefaultAsync(cancellationToken);
            if (partner != null)
            {
                partnerName = string.IsNullOrWhiteSpace(partner.ShortName)
                    ? partner.Name
                    : $"{partner.Name} ({partner.ShortName})";
                partnerProfileStatus = partner.ProfileStatus;
                partnerIsActive = partner.CooperationStatus == "ACTIVE" && partner.ProfileStatus == "APPROVED";
            }
        }

        return new EditableVisitRequestDetailDto
        {
            VisitRequestId = (long)visit.VisitRequestId,
            RequestCode = visit.RequestCode,
            RequestStatus = visit.Status,
            VisitScope = visit.VisitScope,
            Mode = isEditablePending ? "EDIT" : "RESUBMIT",
            IsEditablePending = isEditablePending,
            IsResubmittable = isResubmittable,

            RegistrantFullName = visit.RegistrantFullName,
            RegistrantNationality = visit.RegistrantNationality,
            RegistrantOrganization = visit.RegistrantOrganization,
            RegistrantJobTitle = visit.RegistrantJobTitle,
            RegistrantPhone = visit.RegistrantPhone,
            RegistrantEmail = visit.RegistrantEmail,

            DelegationName = visit.DelegationName,
            VisitType = visit.VisitType,
            VisitTypeOther = visit.VisitTypeOther,
            Purpose = visit.Purpose,
            WorkingContent = visit.WorkingContent,

            ContactPersonFullName = visit.ContactPersonFullName,
            ContactPersonOrganization = visit.ContactPersonOrganization,
            ContactPersonPhone = visit.ContactPersonPhone,
            ContactPersonEmail = visit.ContactPersonEmail,

            WorkingLanguage = visit.WorkingLanguage,
            TransportationNote = visit.TransportationNote,
            MediaConsentStatus = visit.MediaConsentStatus,
            MediaConsentNote = visit.MediaConsentNote,
            PartnerId = visit.PartnerId.HasValue ? (long)visit.PartnerId.Value : null,
            PartnerName = partnerName,
            PartnerIsActive = partnerIsActive,
            PartnerProfileStatus = partnerProfileStatus,
            NoteToFptu = visit.NoteToFptu,

            CampusVisits = instances
                .OrderBy(i => i.PlannedStartAt)
                .Select(i => new EditableCampusSlotDto
                {
                    VisitInstanceId = (long)i.VisitInstanceId,
                    CampusId = (long)i.CampusId,
                    CampusCode = campuses.TryGetValue(i.CampusId, out var ci) ? ci.Code : "",
                    CampusName = campuses.TryGetValue(i.CampusId, out var cn) ? cn.Name : "",
                    PlannedStartAt = i.PlannedStartAt,
                    PlannedEndAt = i.PlannedEndAt,
                    InstanceStatus = i.Status,
                })
                .ToList(),

            Visitors = visit.GuestMembers
                .Where(m => m.MemberType != "EXTERNAL_SUPPORT")
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new EditableGuestMemberDto
                {
                    FullName = m.FullName,
                    Organization = m.Organization,
                    JobTitle = m.JobTitle,
                    Nationality = m.Nationality,
                })
                .ToList(),
            SupportMembers = visit.GuestMembers
                .Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new EditableGuestMemberDto
                {
                    FullName = m.FullName,
                    Organization = m.Organization,
                    JobTitle = m.JobTitle,
                    Nationality = m.Nationality,
                })
                .ToList(),

            ResubmissionCount = (int)visit.ResubmissionCount,
            LastResubmittedAt = visit.LastResubmittedAt,

            PreviousDecisions = isResubmittable
                ? instances
                    .OrderBy(i => i.PlannedStartAt)
                    .Select(i => new PreviousCampusDecisionDto
                    {
                        VisitInstanceId = (long)i.VisitInstanceId,
                        CampusId = (long)i.CampusId,
                        CampusName = campuses.TryGetValue(i.CampusId, out var pc) ? pc.Name : "",
                        DecisionNote = i.DecisionNote,
                        DecidedByName = i.DecidedBy is { } db && deciderNames.TryGetValue(db, out var dn) ? dn : null,
                        DecidedAt = i.DecidedAt,
                    })
                    .ToList()
                : new List<PreviousCampusDecisionDto>(),
        };
    }
}
