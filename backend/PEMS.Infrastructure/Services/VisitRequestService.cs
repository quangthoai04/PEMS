using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
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
        ulong visitorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        // Frontend sends campus codes (e.g. "HN", "HCM") — resolve to BIGINT campus_id
        var requestedCodes = f.VisitSlots.Select(s => s.CampusId).Distinct().ToList();
        var campusIdMap = await _db.Campuses
            .Where(c => requestedCodes.Contains(c.CampusCode))
            .Select(c => new { c.CampusCode, c.CampusId })
            .ToDictionaryAsync(c => c.CampusCode, c => c.CampusId, cancellationToken);

        var requestCode = GenerateRequestCode(utcNow);

        var supportJson  = JsonSerializer.Serialize(f.SupportTeam, _json);
        var contactJson  = JsonSerializer.Serialize(f.ContactPoint, _json);
        var visitScope   = f.VisitScope == "MULTI_CAMPUS"
            ? VisitScopes.MultiCampus
            : VisitScopes.SingleCampus;

        var visitRequest = new VisitRequest
        {
            // VisitRequestId is DB-generated (BIGINT AUTO_INCREMENT).
            RequestCode          = requestCode,
            VisitorUserId        = visitorUserId,
            RegistrantFullName   = f.RegisterFullName,
            RegistrantNationality = f.RegisterNationality,
            RegistrantOrganization = f.RegisterOrganization,
            RegistrantJobTitle   = f.RegisterJobTitle,
            RegistrantPhone      = f.RegisterPhone,
            RegistrantEmail      = f.RegisterEmail,
            DelegationName       = f.DelegationName,
            VisitScope           = visitScope,
            Purpose              = f.Purpose,
            WorkingContent       = f.WorkingContent,
            ExpectedGuestCount   = f.Visitors.Count,
            SupportTeamJson      = supportJson,
            ContactPersonJson    = contactJson,
            WorkingLanguage      = f.Language == "VI" ? WorkingLanguages.Vietnamese : WorkingLanguages.English,
            TransportationNote   = f.Vehicle,
            NoteToFptu           = f.Notes,
            Status               = VisitRequestStatuses.PendingApproval, // overwritten by routing service
            SubmittedAt          = utcNow,
            RowVersion           = 0,
            CreatedAt            = utcNow,
            CreatedBy            = visitorUserId
        };

        // ── Campus instances (added via navigation so EF sets the FK after insert) ──
        var idx = 0;
        foreach (var slot in f.VisitSlots)
        {
            if (!campusIdMap.TryGetValue(slot.CampusId, out var campusId))
                throw new InvalidOperationException($"Unknown campus code '{slot.CampusId}'.");

            idx++;
            visitRequest.CampusInstances.Add(new VisitRequestCampus
            {
                // VisitInstanceId / VisitRequestId are DB-generated / set via navigation.
                CampusId         = campusId,
                InstanceCode     = $"{requestCode}-C{idx:D2}",
                PlannedStartAt   = slot.StartDatetime,
                PlannedEndAt     = slot.EndDatetime,
                Status           = "WAITING_REQUEST_APPROVAL",
                RowVersion       = 0,
                CreatedAt        = utcNow,
                CreatedBy        = visitorUserId
            });
        }

        // ── Guest members ─────────────────────────────────────────────────────
        foreach (var visitor in f.Visitors)
        {
            visitRequest.GuestMembers.Add(new VisitGuestMember
            {
                FullName         = visitor.FullName,
                Organization     = visitor.Organization,
                JobTitle         = visitor.JobTitle,
                Nationality      = visitor.Nationality,
                Email            = visitor.Email,
                IsRepresentative = false,
                CreatedAt        = utcNow,
                CreatedBy        = visitorUserId
            });
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
