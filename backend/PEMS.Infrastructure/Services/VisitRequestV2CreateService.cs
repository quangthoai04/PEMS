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
using PEMS.Shared;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Per-campus form v2 create service. Builds the whole aggregate in the caller's open transaction and flushes
/// to resolve DB-generated ids for the composite member links; the caller commits. See
/// <see cref="IVisitRequestV2CreateService"/>.
/// </summary>
public sealed class VisitRequestV2CreateService : IVisitRequestV2CreateService
{
    private const int MinDurationMinutes = 30;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;

    public VisitRequestV2CreateService(IApplicationDbContext db) => _db = db;

    public async Task<VisitRequest> CreateV2Async(
        VisitRequestFormDataV2 form,
        ulong? registrantUserId,
        string createdSource,
        DateTime vietnamNow,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, CampusHostProposalSeed>? hostProposals = null)
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
        var scope = VisitRequestV2Canonical.ScopeOf(form.CampusVisits);
        var hasMixed = VisitRequestV2Canonical.ComputeHasMixed(form.CampusVisits);

        // ── Operational contact per campus (plan §3.1 step 4). The contact email is REQUIRED now: it is the
        //    address that will be asked to take the campus on, and the column is NOT NULL. ──
        var registrantEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(form.Registrant.Email);
        foreach (var cv in form.CampusVisits)
            if (string.IsNullOrWhiteSpace(cv.OperationalContact.Email))
                throw new BusinessRuleException(
                    "Mỗi cơ sở phải có email đầu mối vận hành.",
                    VisitRequestErrorCodes.OperationalContactEmailRequired);

        // Self-match is decided ONLY by the normalized email of the registrant's own verified address —
        // never by a matching name or phone (plan §1.6). Both create paths have proven that address before
        // reaching here: the public one by OTP to it, the authenticated one because the endpoint is
        // self-registration and the JWT vouches for the caller's mailbox.
        var registrantIsVerified = registrantUserId is not null;

        var fingerprint = VisitRequestV2Canonical.BuildFingerprint(registrantEmailNorm, scope, form.CampusVisits);

        var request = new VisitRequest
        {
            RequestCode = GenerateRequestCode(vietnamNow),
            SubmissionId = form.SubmissionId,
            BusinessFingerprint = fingerprint,
            RegistrantUserId = registrantUserId,
            PartnerId = form.PartnerId,
            CreatedSource = createdSource,
            HasMixedCampusDetails = hasMixed,
            RegistrantFullName = form.Registrant.FullName,
            RegistrantNationality = form.Registrant.Nationality,
            RegistrantOrganization = registrantOrg,
            RegistrantJobTitle = form.Registrant.JobTitle,
            RegistrantPhone = PhoneNumber.NormalizeOrNull(form.Registrant.Phone),
            RegistrantEmail = form.Registrant.Email,
            VisitScope = scope,
            // Pure V2: form content — including the contact — is written per campus into
            // visit_instance_form_details. The request row holds identity, scope and lifecycle only.
            // Status starts behind the gate and is lowered to PENDING_APPROVAL below only if every campus
            // auto-linked; the aggregate service owns every later transition.
            Status = VisitRequestStatuses.PendingContactConfirmation,
            ContactGateRevision = 0,
            SubmittedAt = vietnamNow,
            EmailVerifiedAt = registrantIsVerified ? vietnamNow : null,
            RowVersion = 0,
            CreatedAt = vietnamNow,
            CreatedBy = creatorUserId,
        };

        // ── Instances + per-campus form detail (via navigation → shared PK after insert) ──
        // Each campus decides its own starting status from its own contact: a campus run by the registrant
        // themself is linked here and is immediately awaiting its Staff Leader; every other campus waits for
        // its invited person and holds the whole request behind the gate until then.
        var selfMatchedCampusCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cv in form.CampusVisits)
        {
            var campus = campusByCode[cv.CampusId.Trim().ToUpperInvariant()];
            var contactEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(cv.OperationalContact.Email);
            var selfMatch = registrantIsVerified && contactEmailNorm == registrantEmailNorm;
            if (selfMatch) selfMatchedCampusCodes.Add(campus.CampusCode);

            // The reception-host arrangement is recorded here and NOTHING else happens with it: no
            // current host, no decision, no participant row. Those all wait for the confirmation gate.
            var seed = hostProposals is not null
                       && hostProposals.TryGetValue(campus.CampusCode, out var found)
                ? found
                : CampusHostProposalSeed.WaitForLater;

            request.CampusInstances.Add(new VisitRequestCampus
            {
                CampusId = campus.CampusId,
                PlannedStartAt = cv.PlannedStartAt,
                PlannedEndAt = cv.PlannedEndAt,
                Status = selfMatch
                    ? VisitInstanceStatuses.WaitingRequestApproval
                    : VisitInstanceStatuses.WaitingContactConfirmation,
                OperationalContactUserId = selfMatch ? registrantUserId : null,
                OperationalContactConfirmedAt = selfMatch ? vietnamNow : null,
                OperationalContactConfirmationSource = selfMatch ? OperationalContactSources.RegistrantSelfMatch : null,
                HostSelectionMode = seed.Mode,
                ProposedHostUserId = seed.ProposedHostUserId,
                ProposedHostByUserId = seed.ProposedHostUserId is null ? null : seed.ProposedByUserId,
                ProposedHostAt = seed.ProposedHostUserId is null ? null : vietnamNow,
                ProposedHostActivationStatus = seed.ProposedHostUserId is null
                    ? null
                    : ProposedHostActivationStatuses.Pending,
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
                    // Organization + email are OPTIONAL (validator/frontend allow blank); the DB CHECKs
                    // reject an empty string, so normalize blank → NULL (which the CHECKs and the now-nullable
                    // columns accept). Name + phone stay required upstream.
                    OperationalContactOrganization = Clean(cv.OperationalContact.Organization),
                    OperationalContactJobTitle = Clean(cv.OperationalContact.JobTitle),
                    OperationalContactPhone = PhoneNumber.NormalizeOrNull(cv.OperationalContact.Phone),
                    OperationalContactEmail = cv.OperationalContact.Email.Trim(),
                    WorkingLanguage = cv.WorkingLanguage,
                    TransportationNote = Clean(cv.TransportationNote),
                    MediaConsentStatus = cv.MediaConsentStatus,
                    MediaConsentNote = cv.MediaConsentNote,
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
            // Request-level snapshot = the registrant, and only the registrant. Each campus's contact is
            // snapshotted in that campus's own form-detail revision above.
            SnapshotJson = JsonSerializer.Serialize(new
            {
                request.RegistrantFullName, request.RegistrantOrganization, request.RegistrantJobTitle,
                request.RegistrantPhone, request.RegistrantEmail,
            }, Json),
            AppliedBy = creatorUserId,
            AppliedAt = vietnamNow,
        });

        // ── One INITIAL_CONFIRMATION per campus whose contact is somebody other than the registrant
        //    (plan §3.1 step 4). A self-matched campus gets NO invitation and NO email — it is already
        //    linked — but it does get an event, because "auto-linked at submit" is a real transition an
        //    auditor must be able to see. Raw tokens are never stored here; the dispatcher mints the
        //    single-use hashed token after commit. ──
        var invitations = new List<(VisitRequestIdentityChange Change, string EmailNorm)>();
        foreach (var instance in request.CampusInstances)
        {
            var contactEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(
                instance.FormDetail!.OperationalContactEmail);

            if (instance.OperationalContactUserId is not null)
            {
                // No invitation row exists for a self-matched campus, and the event log is keyed to an
                // invitation — so the auto-link is recorded in the audit log instead. It still has to be
                // recorded: "this account got operating rights on this campus" is exactly what an auditor
                // comes looking for, and here nobody clicked anything to make it happen.
                _db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = registrantUserId,
                    Action = "OPERATIONAL_CONTACT_AUTO_CONFIRMED_REGISTRANT_MATCH",
                    EntityType = "VisitRequestCampus",
                    EntityId = instance.VisitInstanceId,
                    CampusId = instance.CampusId,
                    VisitRequestId = request.VisitRequestId,
                    VisitInstanceId = instance.VisitInstanceId,
                    CorrelationId = form.SubmissionId,
                    SourceType = "CREATE",
                    Reason = $"source={OperationalContactSources.RegistrantSelfMatch};email={MaskEmail(contactEmailNorm)}",
                    CreatedAt = vietnamNow,
                });
                continue;
            }

            var invitation = new VisitRequestIdentityChange
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                ChangeKind = IdentityChangeKinds.InitialConfirmation,
                ConfirmationMethod = "GOOGLE_SSO",
                OldUserId = null,
                NewUserId = null,
                OldEmailNormalized = null,
                NewEmailNormalized = contactEmailNorm,
                NewEmailMasked = MaskEmail(contactEmailNorm),
                PendingSnapshotJson = JsonSerializer.Serialize(new
                {
                    instance.FormDetail!.OperationalContactFullName,
                    instance.FormDetail!.OperationalContactOrganization,
                    instance.FormDetail!.OperationalContactJobTitle,
                    instance.FormDetail!.OperationalContactPhone,
                    email = contactEmailNorm,
                }, Json),
                Status = IdentityChangeStatuses.Pending,
                TokenVersion = 1,
                ExpectedRequestRowVersion = 0,
                RequestedBy = registrantUserId ?? 0,
                RequestedAt = vietnamNow,
                ExpiresAt = vietnamNow.AddHours(72),
                ResendCount = 0,
                CreatedAt = vietnamNow,
            };
            _db.VisitRequestIdentityChanges.Add(invitation);
            invitations.Add((invitation, contactEmailNorm));
        }

        if (invitations.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken); // resolve invitation ids for their event FKs
            foreach (var (invitation, emailNorm) in invitations)
                _db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
                {
                    IdentityChangeId = invitation.IdentityChangeId,
                    VisitRequestId = request.VisitRequestId,
                    VisitInstanceId = invitation.VisitInstanceId,
                    EventType = "CREATED",
                    FromStatus = null,
                    ToStatus = IdentityChangeStatuses.Pending,
                    ActorUserId = registrantUserId,
                    EmailMasked = MaskEmail(emailNorm),
                    CorrelationId = form.SubmissionId,
                    CreatedAt = vietnamNow,
                });
        }
        else
        {
            // Every campus auto-linked: nobody has to confirm anything, so the gate never closes and the
            // Staff Leaders can be told immediately.
            request.Status = VisitRequestStatuses.PendingApproval;
            request.ContactGateRevision = 1;
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
        d.WorkingLanguage, d.TransportationNote, d.MediaConsentStatus, d.MediaConsentNote,
        Members = members.Select(m => new { m.FullName, m.Organization, m.JobTitle, m.Nationality, m.MemberType, m.DisplayOrder }),
    };

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string MaskEmail(string normalizedEmail)
        => VisitRequestFingerprintBuilder.MaskEmail(normalizedEmail);

    private static string GenerateRequestCode(DateTime vietnamNow)
        => $"VR{vietnamNow:yyyyMMdd}{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}";
}
