using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Campuses.Common;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.VisitLinks.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Policies;
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

    /// <summary>
    /// Only used to scope which partner profiles this caller was entitled to pick for a delegation
    /// member. Optional because the aggregate builder is also driven directly (tests, in-process
    /// callers) where no session exists — and "no session" resolves to the PUBLIC option set with no
    /// campus, i.e. the narrowest one. An absent dependency therefore makes the check stricter, never
    /// laxer, so a missed wiring cannot quietly open the door.
    /// </summary>
    private readonly ICurrentUserService? _currentUser;

    public VisitRequestV2CreateService(IApplicationDbContext db, ICurrentUserService? currentUser = null)
    {
        _db = db;
        _currentUser = currentUser;
    }

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
        //    The minimum lead time is enforced HERE, against the server's own clock, and not merely in the
        //    form: it used to accept anything that was not already in the past, so a direct API call — or a
        //    form filled in while the deadline passed — could file a visit for tomorrow morning and leave a
        //    Staff Leader no time to arrange it. Same floor as pending-edit and resubmit.
        var earliestAllowedStart = vietnamNow.AddHours(VisitMutationPolicy.MinScheduleLeadHours);
        foreach (var cv in form.CampusVisits)
        {
            if (cv.PlannedEndAt <= cv.PlannedStartAt)
                throw new BusinessRuleException("Thời gian kết thúc phải sau thời gian bắt đầu.", VisitRequestErrorCodes.InvalidVisitTime);
            if ((cv.PlannedEndAt - cv.PlannedStartAt).TotalMinutes < MinDurationMinutes)
                throw new BusinessRuleException("Mỗi buổi thăm phải kéo dài tối thiểu 30 phút.", VisitRequestErrorCodes.InvalidVisitTime);
            if (cv.PlannedStartAt < earliestAllowedStart)
                throw new BusinessRuleException(
                    VisitScheduleMessages.LeadTimeNotMet(earliestAllowedStart),
                    VisitRequestErrorCodes.InvalidVisitTime);
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

        // ── Per-MEMBER organization identity (PART-01) ──
        // Whoever the request as a whole names as its partner, each member carries their own choice:
        // one delegation routinely mixes organizations, so the request-level partner above says nothing
        // about the person on row 4.
        //
        // The rule is the FORM's, not the submitter's (PART-09). Staff-created used to be validated
        // against the wider internal set, which is what let a Staff Leader attach their own campus's
        // still-pending profile to a guest — offered by a dropdown that had widened for the same
        // reason. A registration form cites published organisations, whoever is filling it in.
        await GuestOrganizationPartnerPolicy.EnsureRequestFormSelectableAsync(
            _db,
            form.CampusVisits.SelectMany(cv =>
                cv.Visitors.Select(v => v.OrganizationPartnerId)
                    .Concat(cv.ExternalSupportMembers.Select(m => m.OrganizationPartnerId)))
                .Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);

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

        // ── The contact is EXTERNAL, and this is where every create path has to prove it. ──
        //
        // The two command handlers check the same thing first, so the ordinary user gets a red message
        // on the exact campus card rather than a banner. This is the backstop underneath them, and it is
        // not redundant: three callers reach this service (authenticated create, verify-and-create,
        // delegated OTP), the OTP path validated a form at INITIATE and creates from the payload sent at
        // VERIFY, and nothing else stands between an in-process caller and the aggregate.
        //
        // It also closes self-match. A registrant whose own address is the campus contact is linked as
        // that contact immediately, with no invitation and nobody confirming anything — which is exactly
        // the shortcut an internal registrant must not have, and the check below refuses their address
        // for the same reason it refuses anybody else's.
        foreach (var contactEmail in form.CampusVisits
                     .Select(cv => VisitRequestFingerprintBuilder.NormalizeEmail(cv.OperationalContact.Email))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            await OperationalContactEligibility.EnsureEmailMayHoldContactRoleAsync(
                _db, contactEmail, cancellationToken);

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
                    // Clean() so a blank/whitespace note lands as NULL, not as a row that reads
                    // "has a note" to every screen and diff downstream.
                    Notes = Clean(cv.Notes),
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
        // The member keys the FORM used, in the same order the rows were built. They exist only for the
        // length of this method: they are what lets the payload say "the contact is THIS person" about
        // somebody who has no id yet, and they are never stored (NP-03).
        var memberKeysByCampusIndex = new List<List<string?>>();
        foreach (var cv in form.CampusVisits)
        {
            var rows = new List<VisitGuestMember>();
            var keys = new List<string?>();
            uint order = 1;
            foreach (var v in cv.Visitors)
            {
                rows.Add(new VisitGuestMember
                {
                    FullName = v.FullName, Organization = v.Organization, JobTitle = v.JobTitle,
                    OrganizationPartnerId = v.OrganizationPartnerId,
                    Nationality = v.Nationality, MemberType = "GUEST", DisplayOrder = order++,
                    CreatedAt = vietnamNow, CreatedBy = creatorUserId,
                });
                keys.Add(v.ClientMemberKey);
            }
            foreach (var m in cv.ExternalSupportMembers)
            {
                rows.Add(new VisitGuestMember
                {
                    FullName = m.FullName, Organization = m.Organization, JobTitle = m.JobTitle,
                    OrganizationPartnerId = m.OrganizationPartnerId,
                    Nationality = m.Nationality, MemberType = "EXTERNAL_SUPPORT", DisplayOrder = order++,
                    CreatedAt = vietnamNow, CreatedBy = creatorUserId,
                });
                keys.Add(m.ClientMemberKey);
            }
            // Added via the request navigation so EF fills VisitRequestId (FK) from the parent on insert.
            foreach (var r in rows) request.GuestMembers.Add(r);
            membersByCampusIndex.Add(rows);
            memberKeysByCampusIndex.Add(keys);
        }

        // ── The contact's three shared fields come FROM the member when one was picked ──
        // Before the flush, so the row is INSERTED describing the right person rather than corrected
        // afterwards. A payload cannot then carry one member's key beside a different person's name:
        // whatever it says about the name, the stored snapshot is the member's own. Phone and email are
        // left alone — a delegation row has neither, and blanking them removes the only way to make
        // contact. An unresolvable key throws here and takes the whole transaction with it.
        for (var i = 0; i < form.CampusVisits.Count; i++)
        {
            var pickedKey = form.CampusVisits[i].OperationalContactClientMemberKey;
            if (string.IsNullOrWhiteSpace(pickedKey)) continue;

            var campusId = campusByCode[form.CampusVisits[i].CampusId.Trim().ToUpperInvariant()].CampusId;
            var picked = OperationalContactLink.FindByClientKey(
                OperationalContactLink.Pair(membersByCampusIndex[i], memberKeysByCampusIndex[i]), pickedKey);
            if (picked is not null)
                OperationalContactLink.ApplySnapshotFromMember(
                    request.CampusInstances.First(c => c.CampusId == campusId).FormDetail!, picked);
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

            // Who, of this campus's delegation, the operational contact IS (NP-03). Resolved HERE
            // because the member ids only exist after the flush above. The picked KEY is what the user
            // chose in "Đầu mối là ai trong đoàn?" — a stable per-row identity, not a position in the
            // list; without one the snapshot is matched, which is a guess and is treated as one.
            OperationalContactLink.Resolve(
                instance.FormDetail!,
                OperationalContactLink.Pair(membersByCampusIndex[i], memberKeysByCampusIndex[i]),
                form.CampusVisits[i].OperationalContactClientMemberKey);

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
                SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, instance.FormDetail!, membersByCampusIndex[i]),
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

        // ── Partner links, seeded from what the registrant actually chose (PART-02) ──
        // Runs AFTER flush #2 because it reads back the instance↔member links it works from. Doing it
        // here, in the create transaction, is the point: the relationship must exist the moment the
        // request does, not the first time somebody happens to open the minutes screen.
        if (await GuestPartnerLinkResolver.ResolveForRequestAsync(
                _db, request.VisitRequestId, vietnamNow, creatorUserId, cancellationToken) > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return request;
    }

    // The local snapshot shape is gone: it silently omitted operationalContactJobTitle, so revision 1
    // recorded no job title and the next edit's diff announced "(trống) → Trưởng phòng" for a field
    // that had been filled in at submit. VisitFormRevisionSnapshotBuilder is now the only writer.

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string MaskEmail(string normalizedEmail)
        => VisitRequestFingerprintBuilder.MaskEmail(normalizedEmail);

    private static string GenerateRequestCode(DateTime vietnamNow)
        => $"VR{vietnamNow:yyyyMMdd}{Guid.NewGuid().ToString("N")[..7].ToUpperInvariant()}";
}
