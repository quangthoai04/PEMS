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
        string createdSource,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
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
            .Select(c => new { c.CampusCode, c.CampusId, c.Status })
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

        // Planned start must not be in the past (1-day grace covers client/server timezone skew);
        // end must be after start (also guarded by the SQL CHECK and form validation).
        var earliestAllowedStart = utcNow.AddDays(-1);
        foreach (var slot in f.CampusVisits)
        {
            if (slot.EndDatetime <= slot.StartDatetime)
                throw new BusinessRuleException(
                    "Thời gian kết thúc phải sau thời gian bắt đầu.", VisitRequestErrorCodes.InvalidVisitTime);

            if (slot.StartDatetime < earliestAllowedStart)
                throw new BusinessRuleException(
                    "Thời gian thăm không được ở quá khứ.", VisitRequestErrorCodes.InvalidVisitTime);
        }

        var requestCode = GenerateRequestCode(utcNow);

        var visitScope   = f.VisitScope == VisitScopes.MultiCampus
            ? VisitScopes.MultiCampus
            : VisitScopes.SingleCampus;

        var visitRequest = new VisitRequest
        {
            // VisitRequestId is DB-generated (BIGINT AUTO_INCREMENT).
            RequestCode          = requestCode,
            VisitorUserId        = visitorUserId,
            PartnerId            = f.PartnerId,
            CreatedSource        = createdSource,
            RegistrantFullName   = f.RegistrantFullName,
            RegistrantNationality = f.RegistrantNationality,
            RegistrantOrganization = f.RegistrantOrganization,
            RegistrantJobTitle   = f.RegistrantPosition,
            RegistrantPhone      = f.RegistrantPhone,
            RegistrantEmail      = f.RegistrantEmail,
            DelegationName       = f.DelegationName,
            VisitScope           = visitScope,
            VisitType            = f.VisitType,
            VisitTypeOther       = f.VisitTypeOther,
            Purpose              = f.Purpose,
            WorkingContent       = f.WorkingContent,
            ExpectedGuestCount   = f.ExpectedGuestCount,
            ContactPersonFullName = f.ContactPerson?.FullName,
            ContactPersonOrganization = f.ContactPerson?.Organization,
            ContactPersonPhone   = f.ContactPerson?.Phone,
            ContactPersonEmail   = f.ContactPerson?.Email,
            WorkingLanguage      = f.WorkingLanguage,
            InterpreterNote      = f.InterpreterNote,
            TransportationType   = f.TransportationType,
            TransportationDetail = f.TransportationDetail,
            MediaConsentStatus   = f.MediaConsentStatus,
            MediaConsentNote     = f.MediaConsentNote,
            NoteToFptu           = f.Notes,
            Status               = VisitRequestStatuses.PendingApproval, // overwritten by routing service
            SubmittedAt          = utcNow,
            RowVersion           = 0,
            CreatedAt            = utcNow,
            CreatedBy            = visitorUserId
        };

        // ── Campus instances (added via navigation so EF sets the FK after insert) ──
        // UC-17 leaves host assignment NULL and status WAITING_REQUEST_APPROVAL; host is
        // assigned only after the request is approved (approval flow, not here).
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
                InstanceCode         = $"{requestCode}-C{idx:D2}",
                PlannedStartAt       = slot.StartDatetime,
                PlannedEndAt         = slot.EndDatetime,
                Status               = VisitInstanceStatuses.WaitingRequestApproval,
                CurrentHostUserId    = null,
                HostAssignedBy       = null,
                HostAssignedAt       = null,
                HostAssignmentSource = null,
                RowVersion           = 0,
                CreatedAt            = utcNow,
                CreatedBy            = visitorUserId
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
                Email            = visitor.Email,
                MemberType       = "GUEST",
                DisplayOrder     = order++,
                IsRepresentative = false,
                CreatedAt        = utcNow,
                CreatedBy        = visitorUserId
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
                    Email            = null,
                    MemberType       = "EXTERNAL_SUPPORT", // User explicitly requested EXTERNAL_SUPPORT
                    DisplayOrder     = order++,
                    IsRepresentative = false,
                    CreatedAt        = utcNow,
                    CreatedBy        = visitorUserId
                });
            }
        }

        _db.VisitRequests.Add(visitRequest);
        return visitRequest;
    }

    // VR + YYYYMMDD + 7 random hex chars → e.g. VR20260618A3F9C12
    private static string GenerateRequestCode(DateTime utcNow)
    {
        var datePart   = utcNow.ToString("yyyyMMdd");
        var randomPart = Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();
        return $"VR{datePart}{randomPart}";
    }
}
