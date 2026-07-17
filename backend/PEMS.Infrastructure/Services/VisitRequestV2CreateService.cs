using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Per-campus form v2 create service. Builds the whole aggregate in the caller's open transaction and flushes
/// to resolve DB-generated ids for the composite member links; the caller commits. See
/// <see cref="IVisitRequestV2CreateService"/>.
/// </summary>
public sealed class VisitRequestV2CreateService : IVisitRequestV2CreateService
{
    private const int MinDurationMinutes = 30;
    private const string AccessActive = "ACTIVE";
    private const string AccessPending = "PENDING_CONFIRMATION";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;

    public VisitRequestV2CreateService(IApplicationDbContext db) => _db = db;

    public async Task<VisitRequest> CreateV2Async(
        VisitRequestFormDataV2 form,
        ulong? registrantUserId,
        string createdSource,
        DateTime vietnamNow,
        CancellationToken cancellationToken = default)
    {
        if (form.CampusVisits is null || form.CampusVisits.Count == 0)
            throw new BusinessRuleException("Cần ít nhất một cơ sở.", VisitRequestErrorCodes.InvalidVisitTime);

        var creatorUserId = registrantUserId;

        // ── Campus resolution + no-dup ──
        var codes = form.CampusVisits
            .Select(c => c.CampusId?.Trim() ?? string.Empty)
            .Select(c => c.ToUpperInvariant())
            .ToList();
        if (codes.Any(string.IsNullOrEmpty))
            throw new BusinessRuleException("Thiếu mã cơ sở.", VisitRequestErrorCodes.CampusNotFound);
        if (codes.Distinct().Count() != codes.Count)
            throw new BusinessRuleException("Không được chọn trùng cơ sở.", VisitRequestErrorCodes.CampusNotFound);

        var campusIdsByCode = await _db.Campuses
            .Where(c => codes.Contains(c.CampusCode))
            .Select(c => new { c.CampusCode, c.CampusId })
            .ToDictionaryAsync(c => c.CampusCode, c => c.CampusId, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var code in codes)
            if (!campusIdsByCode.ContainsKey(code))
                throw new BusinessRuleException($"Cơ sở '{code}' không tồn tại.", VisitRequestErrorCodes.CampusNotFound);

        // ── Operational-availability recheck (UC-86): ACTIVE, exactly one IC dept, exactly one valid Staff
        //    Leader (= the coordinator). Mirrors the v1 create service so v2 routes identically per campus. ──
        var snapshots = await CampusAvailabilityEvaluator.EvaluateAsync(
            _db, campusIdsByCode.Values.ToList(), cancellationToken);
        var campusByCode = new Dictionary<string, CampusAvailabilitySnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, campusId) in campusIdsByCode)
        {
            var s = snapshots.TryGetValue(campusId, out var snap)
                ? snap
                : throw new BusinessRuleException($"Cơ sở '{code}' không tồn tại.", VisitRequestErrorCodes.CampusNotFound);
            if (!string.Equals(s.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException($"Cơ sở '{code}' hiện không hoạt động.", VisitRequestErrorCodes.CampusInactive);
            if (s.ActiveIcDepartmentCount == 0)
                throw new BusinessRuleException($"Cơ sở {s.Name} chưa có phòng ban IC đang hoạt động.", VisitRequestErrorCodes.CampusHasNoActiveIcDepartment);
            if (s.ValidStaffLeaderCount == 0)
                throw new BusinessRuleException($"Cơ sở {s.Name} chưa có Staff Leader đang hoạt động.", VisitRequestErrorCodes.CampusHasNoActiveStaffLeader);
            if (!s.IsAvailableForVisitRegistration)
                throw new BusinessRuleException($"Cấu hình tiếp nhận của cơ sở {s.Name} không hợp lệ.", VisitRequestErrorCodes.CampusStaffLeaderConfigurationInvalid);
            campusByCode[code] = s;
        }

        // ── Per-campus schedule validation (DB/clock-dependent; structural rules already ran in the validator) ──
        var earliestAllowedStart = vietnamNow.AddDays(-1);
        foreach (var cv in form.CampusVisits)
        {
            if (cv.PlannedEndAt <= cv.PlannedStartAt)
                throw new BusinessRuleException("Thời gian kết thúc phải sau thời gian bắt đầu.", VisitRequestErrorCodes.InvalidVisitTime);
            if ((cv.PlannedEndAt - cv.PlannedStartAt).TotalMinutes < MinDurationMinutes)
                throw new BusinessRuleException("Mỗi buổi thăm phải kéo dài tối thiểu 30 phút.", VisitRequestErrorCodes.InvalidVisitTime);
            if (cv.PlannedStartAt < earliestAllowedStart)
                throw new BusinessRuleException("Thời gian thăm không được ở quá khứ.", VisitRequestErrorCodes.InvalidVisitTime);
        }

        // ── Partner (optional) resolves the registrant organisation display, same as v1 ──
        var registrantOrg = form.Registrant.Organization;
        if (form.PartnerId.HasValue)
        {
            var partner = await _db.Partners.FirstOrDefaultAsync(p => p.PartnerId == form.PartnerId.Value, cancellationToken);
            if (partner == null || partner.CooperationStatus != "ACTIVE" || partner.ProfileStatus != "APPROVED")
                throw new BusinessRuleException("Tổ chức/đối tác đã chọn không hợp lệ.", "INVALID_PARTNER");
            registrantOrg = string.IsNullOrWhiteSpace(partner.ShortName) ? partner.Name : $"{partner.Name} ({partner.ShortName})";
        }

        // ── Backend-derived scope + mixed flag (NEVER from the client). has_mixed compares only normalized
        //    COPYABLE form content + member sets — not campus_id, not schedule. Shared with edit/resubmit. ──
        var scope = VisitRequestV2Canonical.ScopeOf(form.CampusVisits.Count);
        var hasMixed = VisitRequestV2Canonical.ComputeHasMixed(form.CampusVisits);

        // Compatibility projection = the smallest-campus_id campus (transition only; real data is per-instance).
        var projection = form.CampusVisits
            .OrderBy(cv => campusByCode[cv.CampusId.Trim().ToUpperInvariant()].CampusId)
            .First();

        // ── Identity (plan §16.4): same normalized email → one ACTIVE account; different → request is created
        //    now but the contact stays PENDING_CONFIRMATION with an INITIAL_CLAIM (72h). ──
        var registrantEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(form.Registrant.Email);
        var contactEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(form.PrimaryContact.Email);
        var contactIsRegistrant = registrantEmailNorm == contactEmailNorm;

        var fingerprint = VisitRequestV2Canonical.BuildFingerprint(
            registrantEmailNorm, contactEmailNorm, scope, form.CampusVisits);

        var request = new VisitRequest
        {
            RequestCode = GenerateRequestCode(vietnamNow),
            SubmissionId = form.SubmissionId,
            BusinessFingerprint = fingerprint,
            VisitorUserId = contactIsRegistrant ? registrantUserId : null,
            RegistrantUserId = registrantUserId,
            PartnerId = form.PartnerId,
            CreatedSource = createdSource,
            FormSchemaVersion = FormSchemaVersions.PerCampus,
            HasMixedCampusDetails = hasMixed,
            PrimaryContactAccessStatus = contactIsRegistrant ? AccessActive : AccessPending,
            PrimaryContactVerifiedAt = contactIsRegistrant ? vietnamNow : null,
            RegistrantFullName = form.Registrant.FullName,
            RegistrantNationality = form.Registrant.Nationality,
            RegistrantOrganization = registrantOrg,
            RegistrantJobTitle = form.Registrant.JobTitle,
            RegistrantPhone = form.Registrant.Phone,
            RegistrantEmail = form.Registrant.Email,
            VisitScope = scope,
            // Compatibility projection (smallest-campus snapshot) — read paths use the per-instance detail.
            DelegationName = projection.DelegationName,
            VisitType = projection.VisitType,
            VisitTypeOther = projection.VisitTypeOther,
            Purpose = projection.Purpose,
            WorkingContent = projection.WorkingContent,
            ContactPersonFullName = form.PrimaryContact.FullName,
            ContactPersonOrganization = form.PrimaryContact.Organization,
            ContactPersonPhone = form.PrimaryContact.Phone,
            ContactPersonEmail = form.PrimaryContact.Email,
            WorkingLanguage = projection.WorkingLanguage,
            TransportationNote = Clean(projection.TransportationNote),
            MediaConsentStatus = projection.MediaConsentStatus,
            MediaConsentNote = projection.MediaConsentNote,
            NoteToFptu = projection.Notes,
            Status = VisitRequestStatuses.PendingApproval,
            SubmittedAt = vietnamNow,
            RowVersion = 0,
            CreatedAt = vietnamNow,
            CreatedBy = creatorUserId,
        };

        // ── Instances + per-campus form detail (via navigation → shared PK after insert) ──
        foreach (var cv in form.CampusVisits)
        {
            var campus = campusByCode[cv.CampusId.Trim().ToUpperInvariant()];
            request.CampusInstances.Add(new VisitRequestCampus
            {
                CampusId = campus.CampusId,
                PlannedStartAt = cv.PlannedStartAt,
                PlannedEndAt = cv.PlannedEndAt,
                Status = VisitInstanceStatuses.WaitingRequestApproval,
                CoordinatorUserId = campus.ValidStaffLeaderUserId,
                CoordinatorAssignedBy = creatorUserId,
                CoordinatorAssignedAt = vietnamNow,
                RowVersion = 0,
                CreatedAt = vietnamNow,
                CreatedBy = creatorUserId,
                FormDetail = new VisitInstanceFormDetail
                {
                    DelegationName = cv.DelegationName,
                    VisitType = cv.VisitType,
                    VisitTypeOther = cv.VisitTypeOther,
                    Purpose = cv.Purpose,
                    WorkingContent = cv.WorkingContent,
                    OperationalContactFullName = cv.OperationalContact.FullName,
                    OperationalContactOrganization = cv.OperationalContact.Organization,
                    OperationalContactPhone = cv.OperationalContact.Phone,
                    OperationalContactEmail = cv.OperationalContact.Email,
                    WorkingLanguage = cv.WorkingLanguage,
                    TransportationNote = Clean(cv.TransportationNote),
                    MediaConsentStatus = cv.MediaConsentStatus,
                    MediaConsentNote = cv.MediaConsentNote,
                    NoteToFptu = cv.Notes,
                    FormRevision = 1,
                    ApprovalRevision = 1,
                    RowVersion = 0,
                    CreatedAt = vietnamNow,
                    CreatedBy = creatorUserId,
                },
            });
        }

        _db.VisitRequests.Add(request);

        // ── Per-campus INDEPENDENT members. Even a UI "copy from campus A" produces distinct guest_member_id
        //    rows here — new campuses never share a mutable member row. Members are staged per campus so the
        //    links can be built after the flush. ──
        var membersByCampusIndex = new List<List<VisitGuestMember>>();
        foreach (var cv in form.CampusVisits)
        {
            var rows = new List<VisitGuestMember>();
            uint order = 1;
            foreach (var v in cv.Visitors)
                rows.Add(new VisitGuestMember
                {
                    FullName = v.FullName, Organization = v.Organization, JobTitle = v.JobTitle,
                    Nationality = v.Nationality, MemberType = "GUEST", DisplayOrder = order++,
                    CreatedAt = vietnamNow, CreatedBy = creatorUserId,
                });
            foreach (var m in cv.ExternalSupportMembers)
                rows.Add(new VisitGuestMember
                {
                    FullName = m.FullName, Organization = m.Organization, JobTitle = m.JobTitle,
                    Nationality = m.Nationality, MemberType = "EXTERNAL_SUPPORT", DisplayOrder = order++,
                    CreatedAt = vietnamNow, CreatedBy = creatorUserId,
                });
            // Added via the request navigation so EF fills VisitRequestId (FK) from the parent on insert.
            foreach (var r in rows) request.GuestMembers.Add(r);
            membersByCampusIndex.Add(rows);
        }

        // ── FLUSH #1 — resolves request id, instance ids, form-detail shared PKs, guest_member ids. ──
        await _db.SaveChangesAsync(cancellationToken);

        // ── Composite links (visit_request_id + visit_instance_id + guest_member_id) + baseline revisions. ──
        var orderedInstances = request.CampusInstances.OrderBy(c => c.CampusId).ToList();
        // Re-pair each campus_visit input to its persisted instance by campus id (input order preserved).
        for (var i = 0; i < form.CampusVisits.Count; i++)
        {
            var campusId = campusByCode[form.CampusVisits[i].CampusId.Trim().ToUpperInvariant()].CampusId;
            var instance = request.CampusInstances.First(c => c.CampusId == campusId);
            uint linkOrder = 0;
            foreach (var member in membersByCampusIndex[i])
            {
                _db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
                {
                    VisitRequestId = request.VisitRequestId,
                    VisitInstanceId = instance.VisitInstanceId,
                    GuestMemberId = member.GuestMemberId,
                    DisplayOrder = linkOrder++,
                    CreatedAt = vietnamNow,
                    CreatedBy = creatorUserId,
                });
            }

            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = 1,
                ApprovalRevision = 1,
                SourceType = "CREATE",
                SnapshotJson = JsonSerializer.Serialize(SnapshotOf(instance.FormDetail!, membersByCampusIndex[i]), Json),
                AppliedBy = creatorUserId,
                AppliedAt = vietnamNow,
            });
        }

        _db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
        {
            VisitRequestId = request.VisitRequestId,
            RequestRevision = 1,
            SourceType = "CREATE",
            SnapshotJson = JsonSerializer.Serialize(new
            {
                request.RegistrantFullName, request.RegistrantOrganization, request.RegistrantJobTitle,
                request.RegistrantPhone, request.RegistrantEmail,
                request.ContactPersonFullName, request.ContactPersonOrganization,
                request.ContactPersonPhone, request.ContactPersonEmail,
            }, Json),
            AppliedBy = creatorUserId,
            AppliedAt = vietnamNow,
        });

        // ── Identity INITIAL_CLAIM (only when contact email != registrant email). The request already exists
        //    and campus approval never waits for the claim. Raw tokens are never stored here. ──
        if (!contactIsRegistrant)
        {
            var claim = new VisitRequestIdentityChange
            {
                VisitRequestId = request.VisitRequestId,
                ChangeKind = "INITIAL_CLAIM",
                TargetRelation = "PRIMARY_CONTACT",
                ConfirmationMethod = "GOOGLE_SSO",
                OldUserId = null,
                NewUserId = null,
                OldEmailNormalized = null,
                NewEmailNormalized = contactEmailNorm,
                NewEmailMasked = MaskEmail(contactEmailNorm),
                PendingSnapshotJson = JsonSerializer.Serialize(new
                {
                    form.PrimaryContact.FullName, form.PrimaryContact.Organization,
                    form.PrimaryContact.Phone, email = contactEmailNorm,
                }, Json),
                Status = "PENDING",
                ExpectedRequestRowVersion = 0,
                RequestedBy = registrantUserId ?? 0,
                RequestedAt = vietnamNow,
                ExpiresAt = vietnamNow.AddHours(72),
                ResendCount = 0,
                CreatedAt = vietnamNow,
            };
            _db.VisitRequestIdentityChanges.Add(claim);
            await _db.SaveChangesAsync(cancellationToken); // resolve claim id for its event FK

            _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
            {
                IdentityChangeId = claim.IdentityChangeId,
                VisitRequestId = request.VisitRequestId,
                EventType = "CREATED",
                FromStatus = null,
                ToStatus = "PENDING",
                ActorUserId = registrantUserId,
                EmailMasked = MaskEmail(contactEmailNorm),
                CorrelationId = form.SubmissionId,
                CreatedAt = vietnamNow,
            });
        }

        // ── Create audit (masked; no OTP/token/raw PII) ──
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = creatorUserId,
            Action = "VISIT_REQUEST_CREATED_V2",
            EntityType = "VisitRequest",
            EntityId = request.VisitRequestId,
            VisitRequestId = request.VisitRequestId,
            CorrelationId = form.SubmissionId,
            SourceType = "CREATE",
            Reason = $"scope={scope};mixed={(hasMixed ? 1 : 0)};campuses={orderedInstances.Count}",
            CreatedAt = vietnamNow,
        });

        // ── FLUSH #2 — links + revisions + identity + audit. Caller commits. ──
        await _db.SaveChangesAsync(cancellationToken);

        return request;
    }

    private static object SnapshotOf(VisitInstanceFormDetail d, IEnumerable<VisitGuestMember> members) => new
    {
        d.DelegationName, d.VisitType, d.VisitTypeOther, d.Purpose, d.WorkingContent,
        d.OperationalContactFullName, d.OperationalContactOrganization, d.OperationalContactPhone, d.OperationalContactEmail,
        d.WorkingLanguage, d.TransportationNote, d.MediaConsentStatus, d.MediaConsentNote, d.NoteToFptu,
        Members = members.Select(m => new { m.FullName, m.Organization, m.JobTitle, m.Nationality, m.MemberType, m.DisplayOrder }),
    };

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Mask the local part keeping first/last char: "abcd@x.com" → "a**d@x.com"; "ab@x.com" → "a*@x.com".
    private static string MaskEmail(string normalizedEmail)
    {
        var at = normalizedEmail.IndexOf('@');
        if (at <= 0) return "***";
        var local = normalizedEmail[..at];
        var domain = normalizedEmail[at..];
        if (local.Length <= 2) return $"{local[0]}*{domain}";
        return $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}{domain}";
    }

    private static string GenerateRequestCode(DateTime vietnamNow)
        => $"VR{vietnamNow:yyyyMMdd}{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}";
}
