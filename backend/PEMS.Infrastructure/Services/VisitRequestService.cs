using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Creates a <see cref="VisitRequest"/> aggregate — request + campus instances + guest members.
/// Adds entities to the context but does NOT call SaveChanges (the caller owns the transaction).
/// </summary>
public sealed class VisitRequestService : IVisitRequestService
{
    private readonly IApplicationDbContext _db;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public VisitRequestService(IApplicationDbContext db) => _db = db;

    public async Task<VisitRequest> CreateAsync(
        VisitRequestFormData f,
        ulong? visitorUserId,
        ulong? registrantUserId,
        string createdSource,
        DateTime vietnamNow,
        CancellationToken cancellationToken = default)
    {
        // The audit "creator" is the submitting actor (registrant) when known; the
        // contact owner stays only the action owner of the request itself.
        var creatorUserId = registrantUserId ?? visitorUserId;
        // ── Business validation: campus existence + ACTIVE state, planned times ──
        // (Structural validation — required fields, scope↔count, end>start — already ran
        //  in the FluentValidation pipeline. These checks need the database / clock.)
        var requestedCodes = f.CampusVisits
            .Select(s => s.CampusId?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Frontend sends campus codes (e.g. "HN", "HCM") — resolve to BIGINT campus_id.
        var campuses = await _db.Campuses
            .Where(c => requestedCodes.Contains(c.CampusCode))
            .Select(c => new { c.CampusCode, c.CampusId, c.Status, c.Name })
            .ToListAsync(cancellationToken);

        var campusByCode = campuses.ToDictionary(c => c.CampusCode, StringComparer.OrdinalIgnoreCase);

        foreach (var code in requestedCodes)
        {
            if (!campusByCode.TryGetValue(code, out var campus))
                throw new BusinessRuleException(
                    $"Cơ sở '{code}' không tồn tại.", VisitRequestErrorCodes.CampusNotFound);

            if (!string.Equals(campus.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException(
                    $"Cơ sở '{code}' hiện không hoạt động.", VisitRequestErrorCodes.CampusInactive);
        }

        // ── Campus-independent approval routing: every selected campus must have an ACTIVE
        // Staff Leader (STAFF + sub_role LEADER of that campus) who receives the instance
        // right after submit. Without one, the request would sit unprocessable — reject the
        // whole submit up-front (no half-created request). The resolved leader also becomes
        // the instance coordinator (the DB trigger requires the coordinator to be a Staff
        // Leader of the same campus). ──
        var requestedCampusIds = campuses.Select(c => c.CampusId).ToList();
        var staffLeadersByCampus = (await _db.Users
                .Where(u => u.Role.RoleCode == RoleCodes.Staff
                            && u.SubRole == UserSubRoles.Leader
                            && u.Status == UserStatuses.Active
                            && u.PrimaryCampusId.HasValue
                            && requestedCampusIds.Contains(u.PrimaryCampusId.Value))
                .Select(u => new { u.UserId, CampusId = u.PrimaryCampusId!.Value })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.CampusId)
            .ToDictionary(g => g.Key, g => g.First().UserId);

        foreach (var campus in campuses)
        {
            if (!staffLeadersByCampus.ContainsKey(campus.CampusId))
                throw new BusinessRuleException(
                    $"Cơ sở {campus.Name} chưa có Staff Leader đang hoạt động nên chưa thể tiếp nhận yêu cầu.",
                    VisitRequestErrorCodes.CampusHasNoActiveStaffLeader);
        }

        // Planned start must not be in the past (1-day grace covers client/server timezone skew);
        // end must be after start (also guarded by the SQL CHECK and form validation).
        var earliestAllowedStart = vietnamNow.AddDays(-1);

        var registrantOrg = f.RegistrantOrganization;
        if (f.PartnerId.HasValue)
        {
            var partner = await _db.Partners
                .FirstOrDefaultAsync(p => p.PartnerId == f.PartnerId.Value, cancellationToken);
            if (partner == null || partner.CooperationStatus != "ACTIVE" || partner.ProfileStatus != "APPROVED")
            {
                throw new BusinessRuleException(
                    "Tổ chức/đối tác đã chọn không hợp lệ hoặc không còn hoạt động.", "INVALID_PARTNER");
            }
            registrantOrg = string.IsNullOrWhiteSpace(partner.ShortName) ? partner.Name : $"{partner.Name} ({partner.ShortName})";
        }

        foreach (var slot in f.CampusVisits)
        {
            if (slot.EndDatetime <= slot.StartDatetime)
                throw new BusinessRuleException(
                    "Thời gian kết thúc phải sau thời gian bắt đầu.", VisitRequestErrorCodes.InvalidVisitTime);

            if (slot.StartDatetime < earliestAllowedStart)
                throw new BusinessRuleException(
                    "Thời gian thăm không được ở quá khứ.", VisitRequestErrorCodes.InvalidVisitTime);
        }

        var requestCode = GenerateRequestCode(vietnamNow);

        var visitScope   = f.VisitScope == VisitScopes.MultiCampus
            ? VisitScopes.MultiCampus
            : VisitScopes.SingleCampus;

        var visitRequest = new VisitRequest
        {
            // VisitRequestId is DB-generated (BIGINT AUTO_INCREMENT).
            RequestCode          = requestCode,
            VisitorUserId        = visitorUserId,
            RegistrantUserId     = registrantUserId,
            PartnerId            = f.PartnerId,
            CreatedSource        = createdSource,
            RegistrantFullName   = f.RegistrantFullName,
            RegistrantNationality = f.RegistrantNationality,
            RegistrantOrganization = registrantOrg,
            RegistrantJobTitle   = f.RegistrantPosition,
            RegistrantPhone      = f.RegistrantPhone,
            RegistrantEmail      = f.RegistrantEmail,
            DelegationName       = f.DelegationName,
            VisitScope           = visitScope,
            VisitType            = f.VisitType,
            VisitTypeOther       = f.VisitTypeOther,
            Purpose              = f.Purpose,
            WorkingContent       = f.WorkingContent,
            ContactPersonFullName = f.ContactPerson.FullName,
            ContactPersonOrganization = f.ContactPerson.Organization,
            ContactPersonPhone   = f.ContactPerson.Phone,
            ContactPersonEmail   = f.ContactPerson.Email,
            WorkingLanguage      = f.WorkingLanguage,
            TransportationNote   = string.IsNullOrWhiteSpace(f.TransportationNote) ? null : f.TransportationNote.Trim(),
            MediaConsentStatus   = f.MediaConsentStatus,
            MediaConsentNote     = f.MediaConsentNote,
            NoteToFptu           = f.Notes,
            Status               = VisitRequestStatuses.PendingApproval, // overwritten by routing service
            SubmittedAt          = vietnamNow,
            RowVersion           = 0,
            CreatedAt            = vietnamNow,
            CreatedBy            = creatorUserId
        };

        // ── Campus instances (added via navigation so EF sets the FK after insert) ──
        // Campus-independent approval: every instance starts WAITING_REQUEST_APPROVAL and is
        // routed straight to the campus Staff Leader (coordinator). Host + decision fields
        // stay NULL until that Staff Leader approves (approve = assign host in one action).
        var idx = 0;
        foreach (var slot in f.CampusVisits)
        {
            var code = slot.CampusId?.Trim() ?? string.Empty;
            if (!campusByCode.TryGetValue(code, out var campus))
                throw new BusinessRuleException(
                    $"Cơ sở '{code}' không tồn tại.", VisitRequestErrorCodes.CampusNotFound);

            idx++;
            visitRequest.CampusInstances.Add(new VisitRequestCampus
            {
                // VisitInstanceId / VisitRequestId are DB-generated / set via navigation.
                CampusId             = campus.CampusId,
                PlannedStartAt       = slot.StartDatetime,
                PlannedEndAt         = slot.EndDatetime,
                Status               = VisitInstanceStatuses.WaitingRequestApproval,
                CurrentHostUserId    = null,
                HostAssignedBy       = null,
                HostAssignedAt       = null,
                CoordinatorUserId    = staffLeadersByCampus[campus.CampusId],
                CoordinatorAssignedBy = creatorUserId,
                CoordinatorAssignedAt = vietnamNow,

                RowVersion           = 0,
                CreatedAt            = vietnamNow,
                CreatedBy            = creatorUserId
            });
        }

        // ── Guest members ─────────────────────────────────────────────────────
        uint order = 1;
        foreach (var visitor in f.Visitors)
        {
            visitRequest.GuestMembers.Add(new VisitGuestMember
            {
                FullName         = visitor.FullName,
                Organization     = visitor.Organization,
                JobTitle         = visitor.JobTitle,
                Nationality      = visitor.Nationality,
                MemberType       = "GUEST",
                DisplayOrder     = order++,
                CreatedAt        = vietnamNow,
                CreatedBy        = creatorUserId
            });
        }

        if (f.SupportMembers != null)
        {
            foreach (var support in f.SupportMembers)
            {
                visitRequest.GuestMembers.Add(new VisitGuestMember
                {
                    FullName         = support.FullName,
                    Organization     = support.Organization,
                    JobTitle         = support.JobTitle,
                    Nationality      = support.Nationality,
                    MemberType       = "EXTERNAL_SUPPORT", // User explicitly requested EXTERNAL_SUPPORT
                    DisplayOrder     = order++,
                    CreatedAt        = vietnamNow,
                    CreatedBy        = creatorUserId
                });
            }
        }

        _db.VisitRequests.Add(visitRequest);
        return visitRequest;
    }

    // VR + YYYYMMDD + 7 random hex chars → e.g. VR20260618A3F9C12
    private static string GenerateRequestCode(DateTime vietnamNow)
    {
        var datePart   = vietnamNow.ToString("yyyyMMdd");
        var randomPart = Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();
        return $"VR{datePart}{randomPart}";
    }
}
