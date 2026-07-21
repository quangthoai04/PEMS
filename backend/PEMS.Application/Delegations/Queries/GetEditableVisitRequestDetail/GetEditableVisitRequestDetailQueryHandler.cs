using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;

public sealed class GetEditableVisitRequestDetailQueryHandler
    : IRequestHandler<GetEditableVisitRequestDetailQuery, EditableVisitRequestDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitFormReadService _formReadService;

    public GetEditableVisitRequestDetailQueryHandler(
        IApplicationDbContext context, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitFormReadService formReadService)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
        _formReadService = formReadService;
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

        // ── Dual-read (per-campus form v2). This is a Visitor-owner-only, single-form editor, so the
        // owner sees every campus; the FORM CONTENT is version-specific (v1 = global projection,
        // v2 = per-campus detail) while Registrant / primary Contact / Partner stay request-level. ──
        var isV2 = visit.FormSchemaVersion >= FormSchemaVersions.PerCampus;

        // The flat single-form editor cannot represent a v2 request whose campuses hold DIFFERENT
        // content — that needs the per-campus v2 editor. Guard it with a stable coded 409.
        if (isV2 && visit.HasMixedCampusDetails)
        {
            throw new ConflictException(
                "Đơn này có nội dung khác nhau theo từng cơ sở; vui lòng dùng màn hình chỉnh sửa theo cơ sở.",
                VisitFormV2ErrorCodes.FormVersionUpgradeRequired);
        }

        // Form-content locals default to the v1 global projection; for a (non-mixed) v2 request they are
        // re-sourced from the per-campus detail + instance member links (never the global fields).
        string delegationName;
        string? visitType, visitTypeOther, purpose, workingContent, workingLanguage;
        string? transportationNote, mediaConsentStatus, mediaConsentNote, noteToFptu;
        List<EditableGuestMemberDto> visitorMembers, supportMembers;
        var orderedInstances = instances.OrderBy(i => i.PlannedStartAt).ToList();

        if (isV2)
        {
            // Non-mixed v2: every campus shares the same content — source it from a representative
            // instance's per-campus detail (owner is authorized for all instances).
            var allInstanceIds = orderedInstances.Select(i => i.VisitInstanceId).ToList();
            var content = await _formReadService.ResolveCampusFormContentAsync(visit, allInstanceIds, cancellationToken);
            if (!content.TryGetValue(orderedInstances[0].VisitInstanceId, out var rep))
                throw new InvalidOperationException("VISIT_FORM_DETAIL_MISSING");

            delegationName = rep.DelegationName;
            visitType = rep.VisitType;
            visitTypeOther = rep.VisitTypeOther;
            purpose = rep.Purpose;
            workingContent = rep.WorkingContent;
            workingLanguage = rep.WorkingLanguage;
            transportationNote = rep.TransportationNote;
            mediaConsentStatus = rep.MediaConsentStatus;
            mediaConsentNote = rep.MediaConsentNote;
            noteToFptu = rep.NoteToFptu;
            visitorMembers = rep.Visitors.Select(MapRow).ToList();
            supportMembers = rep.SupportMembers.Select(MapRow).ToList();
        }
        else
        {
            delegationName = visit.DelegationName;
            visitType = visit.VisitType;
            visitTypeOther = visit.VisitTypeOther;
            purpose = visit.Purpose;
            workingContent = visit.WorkingContent;
            workingLanguage = visit.WorkingLanguage;
            transportationNote = visit.TransportationNote;
            mediaConsentStatus = visit.MediaConsentStatus;
            mediaConsentNote = visit.MediaConsentNote;
            noteToFptu = visit.NoteToFptu;
            visitorMembers = visit.GuestMembers
                .Where(m => m.MemberType != "EXTERNAL_SUPPORT")
                .OrderBy(m => m.DisplayOrder)
                .Select(MapMember)
                .ToList();
            supportMembers = visit.GuestMembers
                .Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .OrderBy(m => m.DisplayOrder)
                .Select(MapMember)
                .ToList();
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

            DelegationName = delegationName,
            VisitType = visitType,
            VisitTypeOther = visitTypeOther,
            Purpose = purpose,
            WorkingContent = workingContent,

            ContactPersonFullName = visit.ContactPersonFullName,
            ContactPersonOrganization = visit.ContactPersonOrganization,
            ContactPersonPhone = visit.ContactPersonPhone,
            ContactPersonEmail = visit.ContactPersonEmail,

            WorkingLanguage = workingLanguage,
            TransportationNote = transportationNote,
            MediaConsentStatus = mediaConsentStatus,
            MediaConsentNote = mediaConsentNote,
            PartnerId = visit.PartnerId.HasValue ? (long)visit.PartnerId.Value : null,
            PartnerName = partnerName,
            PartnerIsActive = partnerIsActive,
            PartnerProfileStatus = partnerProfileStatus,
            NoteToFptu = noteToFptu,

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

            Visitors = visitorMembers,
            SupportMembers = supportMembers,

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

    private static EditableGuestMemberDto MapMember(Domain.Entities.Delegations.VisitGuestMember m) => new()
    {
        FullName = m.FullName,
        Organization = m.Organization,
        JobTitle = m.JobTitle,
        Nationality = m.Nationality,
    };

    // Maps a v2 per-campus member row (resolved via IVisitFormReadService) to the flat editable DTO.
    private static EditableGuestMemberDto MapRow(VisitFormMemberRow r) => new()
    {
        FullName = r.FullName,
        Organization = r.Organization,
        JobTitle = r.JobTitle,
        Nationality = r.Nationality,
    };
}
