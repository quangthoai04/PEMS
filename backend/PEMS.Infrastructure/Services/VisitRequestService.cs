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
        PendingVisitRequestFormData f,
        string visitorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        // Frontend sends campus codes (e.g. "HN", "HCM") — resolve to UUIDs
        var requestedCodes = f.VisitSlots.Select(s => s.CampusId).Distinct().ToList();
        var campusIdMap = await _db.Campuses
            .Where(c => requestedCodes.Contains(c.CampusCode))
            .Select(c => new { c.CampusCode, c.CampusId })
            .ToDictionaryAsync(c => c.CampusCode, c => c.CampusId, cancellationToken);

        var requestId   = Guid.NewGuid().ToString();
        var requestCode = GenerateRequestCode(utcNow);

        var supportJson  = JsonSerializer.Serialize(f.SupportTeam, _json);
        var contactJson  = JsonSerializer.Serialize(f.ContactPoint, _json);
        var visitScope   = f.VisitScope == "MULTI_CAMPUS"
            ? VisitScopes.MultiCampus
            : VisitScopes.SingleCampus;

        var visitRequest = new VisitRequest
        {
            VisitRequestId       = requestId,
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
            CreatedBy            = f.RegisterEmail
        };

        _db.VisitRequests.Add(visitRequest);

        // ── Campus instances ──────────────────────────────────────────────────
        foreach (var (slot, idx) in f.VisitSlots.Select((s, i) => (s, i)))
        {
            // Resolve campus code → UUID; fall back to raw value if not found in DB
            var campusId = campusIdMap.TryGetValue(slot.CampusId, out var id) ? id : slot.CampusId;

            _db.VisitRequestCampuses.Add(new VisitRequestCampus
            {
                VisitInstanceId  = Guid.NewGuid().ToString(),
                VisitRequestId   = requestId,
                CampusId         = campusId,
                InstanceCode     = $"{requestCode}-C{idx + 1:D2}",
                PlannedStartAt   = slot.StartDatetime,
                PlannedEndAt     = slot.EndDatetime,
                Status           = "WAITING_REQUEST_APPROVAL",
                RowVersion       = 0,
                CreatedAt        = utcNow,
                CreatedBy        = f.RegisterEmail
            });
        }

        // ── Guest members ─────────────────────────────────────────────────────
        foreach (var visitor in f.Visitors)
        {
            _db.VisitGuestMembers.Add(new VisitGuestMember
            {
                GuestMemberId    = Guid.NewGuid().ToString(),
                VisitRequestId   = requestId,
                FullName         = visitor.FullName,
                Organization     = visitor.Organization,
                JobTitle         = visitor.JobTitle,
                Nationality      = visitor.Nationality,
                Email            = visitor.Email,
                IsRepresentative = false,
                CreatedAt        = utcNow,
                CreatedBy        = f.RegisterEmail
            });
        }

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
