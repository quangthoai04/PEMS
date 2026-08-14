using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Shared;

using PEMS.Application.Delegations.Common;
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

        // No role gate. A registrant may be a VISITOR, STAFF or STAFF LEADER account (plan §2.1), and
        // the edit/resubmit commands this screen feeds authorize on the registrant relation alone —
        // a role check here would 403 a staff registrant out of a request the backend would accept.
        var userId = _currentUser.UserId.Value;

        var visit = await _context.VisitRequests
            .AsNoTracking()
            .Include(v => v.CampusInstances)
            .Include(v => v.GuestMembers)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        // The editor screen mirrors the edit/resubmit commands, which are registrant-only.
        if (!VisitRequestOwnership.IsRegistrant(visit, userId))
            throw new ForbiddenException("Chỉ người đăng ký mới được sửa đơn này.");

        var instances = visit.CampusInstances;
        // planned_start_at is local wall-clock DATETIME → compare against VietnamNow.
        var vnNow = _clock.VietnamNow;

        // ── Which door is this request standing at, and is it open? ───────────────────────────────
        // Only the FIRST question is answered here. This screen used to answer the second one too,
        // with its own copy of the rule — PENDING_APPROVAL plus every campus WAITING_REQUEST_APPROVAL
        // plus a bare AddHours(24) — and that copy drifted away from the list that offers the button
        // and from the command that accepts the payload. A registrant whose campuses were still
        // waiting for their operational contacts to confirm was shown "Sửa đơn" and then told the
        // request was no longer editable, because those two disagreed about a request nobody had
        // decided anything about. The verdict now comes from the canonical guard in both branches, so
        // list, editor and command cannot answer the same state three ways.
        //
        // The cutoff is VisitMutationPolicy.RequiredLeadHours — 6 hours measured on the EXISTING
        // schedule, answering "is this action still open". It is NOT the 72-hour registration floor,
        // which is a rule about a NEWLY PROPOSED start and is enforced where that new date is actually
        // known (VisitRequestV2EditService). Deciding whether to open the editor from the registration
        // floor is what would freeze a request that merely needs a typo fixed.
        var isResubmittable = visit.Status == VisitRequestStatuses.Rejected
            && instances.Count > 0
            && instances.All(i => i.Status == VisitInstanceStatuses.Rejected);

        if (isResubmittable)
        {
            // Same canonical call the resubmit command makes. Its lead time is deliberately NOT
            // measured against the old, already-passed schedule; it is enforced against the new one.
            VisitMutationGuard.EnsureRequestLevelAllowed(
                VisitMutationAction.ResubmitRejectedRequest, visit, vnNow,
                c => c.Status == VisitInstanceStatuses.Rejected,
                VisitRequestErrorCodes.VisitRequestNotResubmittable);
        }
        else
        {
            // Anything not fully rejected is asked as a whole-request pending edit — including the
            // states that must be REFUSED. Asking the guard rather than pre-filtering them out is what
            // gets the caller the real reason: a cutoff refusal keeps its own
            // VISIT_MUTATION_CUTOFF_REACHED code with cutoffAt/plannedStartAt/requiredLeadHours
            // attached, and a mixed request whose sibling is already approved is told to edit the
            // still-pending campus on its own instead of being handed a flat "không thể sửa".
            VisitMutationGuard.EnsureRequestLevelAllowed(
                VisitMutationAction.EditPendingRequest, visit, vnNow,
                c => c.Status is VisitInstanceStatuses.WaitingContactConfirmation
                              or VisitInstanceStatuses.WaitingRequestApproval,
                VisitRequestErrorCodes.VisitRequestNotEditable);
        }

        // Past the guard, the mode IS the branch that survived it.
        var isEditablePending = !isResubmittable;

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

        // ── Pure V2. This is a registrant-only, single-form editor, so the registrant sees every
        // campus; FORM CONTENT comes from each campus's own detail (including its own operational
        // contact), while Registrant and Partner stay request-level. ──

        // The flat single-form editor cannot represent a request whose campuses hold DIFFERENT content —
        // that needs the per-campus editor. Guard it with a stable coded 409.
        if (visit.HasMixedCampusDetails)
        {
            throw new ConflictException(
                "Đơn này có nội dung khác nhau theo từng cơ sở; vui lòng dùng màn hình chỉnh sửa theo cơ sở.",
                VisitFormV2ErrorCodes.FormVersionUpgradeRequired);
        }

        var orderedInstances = instances.OrderBy(i => i.PlannedStartAt).ToList();

        // Not mixed (guarded above) ⇒ every campus holds identical content, so a representative
        // instance's per-campus detail IS the single-form value. The owner is authorized for all
        // instances. A missing detail is a hard data error, never a fall back to request-level fields.
        var allInstanceIds = orderedInstances.Select(i => i.VisitInstanceId).ToList();
        var content = await _formReadService.ResolveCampusFormContentAsync(visit, allInstanceIds, cancellationToken);
        if (!content.TryGetValue(orderedInstances[0].VisitInstanceId, out var rep))
            throw new InvalidOperationException("VISIT_FORM_DETAIL_MISSING");

        string delegationName = rep.DelegationName;
        string? visitType = rep.VisitType;
        string? visitTypeOther = rep.VisitTypeOther;
        string? purpose = rep.Purpose;
        string? workingContent = rep.WorkingContent;
        string? workingLanguage = rep.WorkingLanguage;
        string? transportationNote = rep.TransportationNote;
        string? mediaConsentStatus = rep.MediaConsentStatus;
        string? notes = rep.Notes;
        List<EditableGuestMemberDto> visitorMembers = rep.Visitors.Select(MapRow).ToList();
        List<EditableGuestMemberDto> supportMembers = rep.SupportMembers.Select(MapRow).ToList();

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

            WorkingLanguage = workingLanguage,
            TransportationNote = transportationNote,
            MediaConsentStatus = mediaConsentStatus,
            Notes = notes,
            PartnerId = visit.PartnerId.HasValue ? (long)visit.PartnerId.Value : null,
            PartnerName = partnerName,
            PartnerIsActive = partnerIsActive,
            PartnerProfileStatus = partnerProfileStatus,

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
                    // The editor loads each campus's OWN contact. Prefilling every campus from a
                    // representative one is how an edit used to overwrite a sibling's contact with a
                    // value its owner never entered.
                    OperationalContactFullName = ContactOf(i, content)?.FullName ?? "",
                    OperationalContactOrganization = ContactOf(i, content)?.Organization ?? "",
                    OperationalContactJobTitle = ContactOf(i, content)?.JobTitle ?? "",
                    OperationalContactPhone = ContactOf(i, content)?.Phone ?? "",
                    OperationalContactEmail = ContactOf(i, content)?.Email ?? "",
                    ContactConfirmed = i.OperationalContactUserId is not null,
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
        OrganizationPartnerId = m.OrganizationPartnerId,
        JobTitle = m.JobTitle,
        Nationality = m.Nationality,
    };

    // Maps a v2 per-campus member row (resolved via IVisitFormReadService) to the flat editable DTO.
    private static EditableGuestMemberDto MapRow(VisitFormMemberRow r) => new()
    {
        FullName = r.FullName,
        Organization = r.Organization,
        OrganizationPartnerId = r.OrganizationPartnerId,
        JobTitle = r.JobTitle,
        Nationality = r.Nationality,
    };

    /// <summary>This campus's own contact snapshot; null when its detail row is missing.</summary>
    private static VisitFormOperationalContact? ContactOf(
        Domain.Entities.Delegations.VisitRequestCampus instance,
        IReadOnlyDictionary<ulong, VisitCampusFormContent> content)
        => content.TryGetValue(instance.VisitInstanceId, out var d) ? d.OperationalContact : null;
}
