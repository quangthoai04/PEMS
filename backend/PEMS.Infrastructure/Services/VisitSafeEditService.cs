using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Validation;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Policies;
using PEMS.Shared;

namespace PEMS.Infrastructure.Services;

/// <summary>See <see cref="IVisitSafeEditService"/>. The classifier (<see cref="VisitFieldClassifier"/>)
/// is the single gate for GENERIC safe fields — anything outside SAFE/PRIVACY_URGENT fails closed here
/// regardless of what the endpoint accepted structurally. Operational-contact same-person metadata and
/// relation (plan CanhIter3FixBug) are a SEPARATE, dedicated domain mutation applied directly by this
/// service — they intentionally never pass through the classifier (see <see cref="VisitFieldClassifier"/>'s
/// own doc for why those fields stay classified <c>ApprovalSensitive</c> regardless).</summary>
public sealed class VisitSafeEditService : IVisitSafeEditService
{
    private readonly IApplicationDbContext _db;
    private readonly IOperationalContactInvitationService _invitations;

    public VisitSafeEditService(IApplicationDbContext db, IOperationalContactInvitationService invitations)
    {
        _db = db;
        _invitations = invitations;
    }

    private sealed record Change(
        string FieldPath, ulong? InstanceId, string? OldValue, string? NewValue, string Class,
        Action Apply);

    /// <summary>
    /// ONE instance's staged same-person contact-metadata/relation edit (plan CanhIter3FixBug). Built
    /// entirely during the VALIDATE/PLAN pass — every field here is already resolved (normalized profile,
    /// eligibility, mismatch check, old/new relation display names) — and applied only in the APPLY pass,
    /// strictly after <c>VisitRevisionBaselineGuard</c> has captured this instance's "before" snapshot
    /// (decision T: contact mutation must never happen before baseline capture, or the "before" the
    /// history renderer shows would already be the "after"). <see cref="Instance"/>/<see cref="Detail"/>
    /// are read-only references at plan time; they are not written to until <c>ApplyContactPlan</c> runs.
    /// </summary>
    private sealed record ContactMutationPlan(
        VisitRequestCampus Instance,
        VisitInstanceFormDetail Detail,
        bool ProfileChanged,
        OperationalContactProfileMutation.NormalizedProfile NormalizedProfile,
        AuditLog? ProfileAudit,
        bool RelationChanged,
        ulong? NewRelationId,
        AuditLog? RelationAudit);

    public async Task<VisitRequestSafeEditResponse> ApplySafeEditAsync(
        VisitRequest request, VisitRequestSafeEditDto patch, ulong actorId, DateTime now,
        CancellationToken ct)
    {
        // ── 0. FOR-UPDATE row-version guard (same discipline as the v2 edit flows) ──
        var currentVersion = (await _db.VisitRequests
                .FromSqlRaw("SELECT * FROM visit_requests WHERE visit_request_id = {0} FOR UPDATE",
                    request.VisitRequestId)
                .AsNoTracking()
                .Select(v => (int?)v.RowVersion)
                .ToListAsync(ct))
            .Single();
        if (currentVersion != patch.ExpectedRequestRowVersion || request.RowVersion != currentVersion)
            throw new ConflictException(
                "Đơn đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                VisitFormV2ErrorCodes.VisitFormConcurrencyConflict);

        var changes = new List<Change>();

        // Campus names for refusal messages — a multi-campus user needs to know WHICH campus closed the
        // window, and "Không thể sửa" without a name sends them to look at all of them.
        var campusIds = request.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        var campusNames = await _db.Campuses.AsNoTracking()
            .Where(c => campusIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, ct);

        // ── 1. Request-level safe subset (registrant display fields + contact snapshot, never emails) ──
        if (patch.Registrant is { } reg)
        {
            if (string.IsNullOrWhiteSpace(reg.FullName))
                throw new BusinessRuleException("Họ tên người đăng ký không được để trống.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
            if (string.IsNullOrWhiteSpace(reg.Nationality))
                throw new BusinessRuleException("Quốc tịch người đăng ký không được để trống.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

            // Partner identity travels ATOMICALLY with the organization text: the client sends only an
            // id, and the canonical display name is ALWAYS resolved here, server-side — never trusted
            // from client-supplied text sitting next to a client-supplied id (plan §3.3/§7.4).
            var registrantOrg = Clean(reg.Organization);
            if (reg.PartnerId.HasValue)
            {
                await GuestOrganizationPartnerPolicy.EnsureRequestFormSelectableAsync(
                    _db, new[] { reg.PartnerId.Value }, ct);
                var partner = await _db.Partners.AsNoTracking()
                    .FirstAsync(p => p.PartnerId == reg.PartnerId.Value, ct);
                registrantOrg = string.IsNullOrWhiteSpace(partner.ShortName)
                    ? partner.Name : $"{partner.Name} ({partner.ShortName})";
            }

            Diff(changes, VisitFieldClassifier.RegistrantFullName, null,
                request.RegistrantFullName, reg.FullName.Trim(), v => request.RegistrantFullName = v!);

            // Nationality (Patch 4): compared in CANONICAL space, not raw text — mirrors
            // VisitRequestV2EditService.ApplyCommonFields. A safe edit that only touches, say, the
            // organization must not be blocked — or silently re-cased/rewritten — just because the
            // registrant's legacy nationality spelling does not match the canonical form byte for
            // byte. Nothing is written or diffed unless the two sides resolve to DIFFERENT countries;
            // only then must the new one resolve at all.
            var trimmedNationality = reg.Nationality.Trim();
            var nationalityResolved = CountryName.TryResolve(trimmedNationality, out var nationalityCanonical);
            var currentNationalityResolved = CountryName.TryResolve(request.RegistrantNationality, out var currentNationalityCanonical);
            var effectiveCurrentNationality = currentNationalityResolved ? currentNationalityCanonical! : request.RegistrantNationality;
            var effectiveIncomingNationality = nationalityResolved ? nationalityCanonical! : trimmedNationality;
            if (!string.Equals(effectiveCurrentNationality, effectiveIncomingNationality, StringComparison.Ordinal))
            {
                if (!nationalityResolved)
                    throw new BusinessRuleException(
                        $"Quốc tịch người đăng ký không hợp lệ: '{trimmedNationality}'. {CountryName.FormatHint}",
                        VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
                Diff(changes, VisitFieldClassifier.RegistrantNationality, null,
                    request.RegistrantNationality, nationalityCanonical, v => request.RegistrantNationality = v!);
            }
            Diff(changes, VisitFieldClassifier.RegistrantOrganization, null,
                request.RegistrantOrganization, registrantOrg, v => request.RegistrantOrganization = v);
            Diff(changes, VisitFieldClassifier.RegistrantPartnerId, null,
                request.PartnerId?.ToString(), reg.PartnerId?.ToString(),
                v => request.PartnerId = v is null ? (ulong?)null : ulong.Parse(v));
            Diff(changes, VisitFieldClassifier.RegistrantJobTitle, null,
                request.RegistrantJobTitle, Clean(reg.JobTitle), v => request.RegistrantJobTitle = v);

            // Validated AND normalized here, not just trusted from the (already-checked) validator: the
            // classifier/Diff pipeline below has no format opinion of its own, so an invalid value that
            // somehow reached this far (a caller bypassing the MediatR pipeline in a test, a future
            // refactor) must still be refused rather than silently persisted as typed — the exact
            // regression this guards against was a Safe Edit call storing "+821012340001123213sd"
            // verbatim because nothing between the wire and the column ever checked its shape.
            var cleanedPhone = Clean(reg.Phone);
            string? newPhone = null;
            if (cleanedPhone is not null && !PhoneNumber.TryNormalize(cleanedPhone, out newPhone))
                throw new BusinessRuleException(
                    $"Số điện thoại người đăng ký không hợp lệ. {PhoneNumberRules.FormatHint}",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
            Diff(changes, VisitFieldClassifier.RegistrantPhone, null,
                request.RegistrantPhone, newPhone, v => request.RegistrantPhone = v);
        }

        // ── 2. Per-instance safe subset ──
        // correlationId is minted here (not later) so contact-metadata/relation audits built during the
        // VALIDATE/PLAN pass below already carry it — same value the generic VISIT_SAFE_FIELDS_UPDATED
        // audit and every revision-history row use, per call.
        var correlationId = Guid.NewGuid().ToString("N");
        var touchedInstances = new List<VisitRequestCampus>();
        var contactPlans = new List<ContactMutationPlan>();
        var contactAppliedChanges = new List<SafeEditAppliedChange>();
        // Deterministic order (plan CanhIter3FixBug, decision W) — so two concurrent multi-instance Safe
        // Edit calls touching an overlapping instance set can never lock those instances in different
        // orders against each other.
        foreach (var ip in (patch.Instances ?? new List<SafeInstancePatchDto>())
                     .OrderBy(i => i.VisitInstanceId))
        {
            var instance = request.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == ip.VisitInstanceId)
                ?? throw new BusinessRuleException("Cơ sở được sửa không thuộc đơn này.",
                    VisitFormV2ErrorCodes.VisitInstanceScopeForbidden);

            // Authoritative per-instance concurrency (plan CanhIter3FixBug, decision R): a bare in-memory
            // comparison would let a concurrent UpdateOperationalContactProfile edit on the SAME instance
            // silently win or lose depending on load order — row_version is a plain int with no EF
            // concurrency token behind it (confirmed: IsConcurrencyToken is used nowhere in this
            // codebase). Replicates VisitInstanceConcurrencyGuard's SELECT ... FOR UPDATE re-read logic
            // (rather than calling it directly) so this endpoint keeps its existing
            // VisitFormConcurrencyConflict code, which VisitSafeEditV2Tests already asserts.
            await EnsureInstanceUnchangedAsync(instance, ip.ExpectedRowVersion, ct);

            var detail = instance.FormDetail
                ?? throw new ConflictException("Thiếu dữ liệu biểu mẫu theo cơ sở.",
                    VisitFormV2ErrorCodes.VisitFormDetailMissing);

            if (ip.MediaConsentStatus is not (null or "AGREED" or "DECLINED"))
                throw new BusinessRuleException("Trạng thái truyền thông không hợp lệ.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

            // ── GENERIC Safe fields answer to the classifier/lifecycle gate below — but ONLY when this
            //    instance's patch actually touches one of them (plan CanhIter3FixBug, decision M). A
            //    contact-only patch must never be refused by a lifecycle window (Assigned/BeforeVisit
            //    only) that has nothing to do with contact editing's own, wider window — that check
            //    happens separately, inside PlanContactMutation, only when OperationalContact is present.
            //
            //    LIFECYCLE + CUTOFF for THIS campus only — a sibling that is under way says nothing about
            //    a campus still days out, and coupling them is what let one campus's timing freeze
            //    another's notes. WAITING_REQUEST_APPROVAL is deliberately NOT accepted here: a still-
            //    pending campus belongs to per-campus pending-edit, which can change everything. ──
            var hasGenericField = ip.TransportationNote is not null || ip.Notes is not null
                || ip.MediaConsentStatus is not null;
            if (hasGenericField)
                VisitMutationGuard.EnsureAllowed(
                    VisitMutationAction.SubmitSafeEdit, request.Status, instance, now,
                    VisitViewerRelations.Requester, VisitRequestErrorCodes.VisitRequestNotEditable,
                    campusNames);

            // Every instance field is OPTIONAL: null means "not part of this edit", which is how the
            // client sends only what changed. Previously all four were mandatory, so a one-word note
            // correction re-submitted the media-consent decision of every campus in the request — and a
            // stale value in the form would silently overwrite a newer one.
            var before = changes.Count;
            if (ip.TransportationNote is not null)
                Diff(changes, VisitFieldClassifier.TransportationNote, instance.VisitInstanceId,
                    detail.TransportationNote, Clean(ip.TransportationNote), v => detail.TransportationNote = v);
            if (ip.Notes is not null)
                Diff(changes, VisitFieldClassifier.Notes, instance.VisitInstanceId,
                    detail.Notes, Clean(ip.Notes), v => detail.Notes = v);
            if (ip.MediaConsentStatus is not null)
                Diff(changes, VisitFieldClassifier.MediaConsentStatus, instance.VisitInstanceId,
                    detail.MediaConsentStatus, ip.MediaConsentStatus, v => detail.MediaConsentStatus = v!);
            if (changes.Count > before)
                touchedInstances.Add(instance);

            // ── Same-person operational-contact correction (plan CanhIter3FixBug) — a dedicated domain
            //    mutation orchestrated here, never routed through the classifier above (those fields stay
            //    ApprovalSensitive there for legacy/general-Amendment compatibility — see
            //    VisitFieldClassifier). VALIDATE/PLAN ONLY: nothing on `detail`/`instance` is written by
            //    PlanContactMutation — every field it touches is read-only at this point. The actual
            //    mutation happens later, in the apply phase, strictly after this call's baselines are
            //    captured (decision T) — see ApplyContactPlanAsync. ──
            if (ip.OperationalContact is { } cp)
            {
                var plan = PlanContactMutation(request, instance, detail, cp, actorId, now, correlationId);
                if (plan is not null)
                {
                    contactPlans.Add(plan);
                    if (plan.ProfileChanged)
                        contactAppliedChanges.Add(new SafeEditAppliedChange(
                            "instance.operationalContact.profile", instance.VisitInstanceId,
                            AmendmentChangeClasses.Contact));
                    if (plan.RelationChanged)
                        contactAppliedChanges.Add(new SafeEditAppliedChange(
                            VisitFieldClassifier.OperationalContactGuestMemberId, instance.VisitInstanceId,
                            AmendmentChangeClasses.Contact));
                    if (!touchedInstances.Contains(instance))
                        touchedInstances.Add(instance);
                }
            }
        }

        if (changes.Count == 0 && contactPlans.Count == 0)
            throw new BusinessRuleException("Không có thay đổi nào để áp dụng.",
                VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

        // ── 3. Classifier gate — fail closed on anything that is not SAFE/PRIVACY_URGENT ──
        foreach (var c in changes)
            if (c.Class is not (AmendmentChangeClasses.Safe or AmendmentChangeClasses.PrivacyUrgent))
                throw new BusinessRuleException(
                    $"Trường '{c.FieldPath}' không thuộc nhóm sửa nhanh. Vui lòng gửi đề xuất thay đổi (amendment).",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

        // ── 4. Request-level scope. The registrant/contact block is SHARED by every campus, so it is
        //        all-or-nothing: refused outright while any campus has passed the point of no return
        //        (its delegation is already on site reading that name), and the deadline comes from the
        //        earliest campus still ahead.
        //
        //        The old check asked only "is the earliest ACTIVE campus < 24h away", and skipped
        //        entirely when that set was EMPTY — so once every campus had moved to DURING_VISIT the
        //        guard evaluated `earliest is null` and let the edit straight through. An empty set now
        //        falls back to the earliest campus overall, never to "no campus, allow".
        if (changes.Any(c => c.InstanceId is null))
        {
            VisitMutationGuard.EnsureRequestLevelAllowed(
                VisitMutationAction.SubmitSafeEdit, request, now,
                c => c.Status is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit,
                VisitRequestErrorCodes.VisitRequestNotEditable,
                campusNames);
        }

        // ── 5. Apply + revisions + audit, one commit ──

        // Baselines BEFORE anything writes. Every touched campus is about to reach revision N+1 (or, for
        // a contact-only touch, just a RowVersion bump — decision B), and the request block is about to
        // be rewritten — so this is the last point at which the "before" values still exist to be
        // recorded. A safe edit is exactly the case that used to produce an empty drawer: it changes a
        // note or a phone number, and with no revision N to diff against, the history reported "no
        // recorded changes" for a change the user had just made. Nothing from the per-instance loop above
        // (generic `changes` closures, staged `contactPlans`) has written to any entity yet — this is the
        // barrier decision T requires.
        var requestBaselineJson = VisitRevisionBaselineGuard.CaptureRequestSnapshot(request);
        foreach (var instance in touchedInstances)
            if (instance.FormDetail is { } beforeDetail)
                await VisitRevisionBaselineGuard.EnsureInstanceBaselineAsync(
                    _db, request, instance, beforeDetail, actorId, now, ct);

        foreach (var c in changes) c.Apply();
        foreach (var plan in contactPlans)
            await ApplyContactPlanAsync(plan, ct);

        foreach (var instance in touchedInstances)
        {
            var detail = instance.FormDetail!;
            // Whether THIS instance had a genuine generic-Safe field change (Notes/TransportationNote/
            // MediaConsentStatus) — independent of whether it also had a contact-only touch. Only a
            // genuine form-content change bumps FormRevision/inserts revision history (decision B); a
            // contact-only touch still gets its RowVersion bumps below, unconditionally.
            var hasFormContentChange = changes.Any(c => c.InstanceId == instance.VisitInstanceId);

            if (hasFormContentChange)
                detail.FormRevision += 1;
            detail.RowVersion += 1;
            detail.UpdatedAt = now;
            detail.UpdatedBy = actorId;
            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;

            if (!hasFormContentChange)
                continue; // contact-only: FormRevision did not move, so a revision-history row here would
                          // collide with the unique (VisitInstanceId, FormRevision) index (decision B).

            var members = MembersOf(request, instance);
            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = detail.FormRevision,
                ApprovalRevision = detail.ApprovalRevision,
                SourceType = FormRevisionSourceTypes.SafeEdit,
                SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, detail, members),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        if (changes.Any(c => c.InstanceId is null))
        {
            await VisitRevisionBaselineGuard.EnsureRequestBaselineAsync(
                _db, request, requestBaselineJson, actorId, now, ct);

            // NOT a raw MaxAsync: EnsureRequestBaselineAsync just staged (unflushed) a possible
            // RECOVERED_BASELINE row two lines above, which a database MAX cannot see yet — the
            // shared helper unions the DB max with EF's own .Local staged rows so the two can never
            // collide on the same revision number (see VisitRevisionBaselineGuard for detail).
            var nextRevision = await VisitRevisionBaselineGuard.NextRequestRevisionAsync(
                _db, request.VisitRequestId, ct);
            _db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                RequestRevision = nextRevision,
                SourceType = FormRevisionSourceTypes.SafeEdit,
                SnapshotJson = VisitFormRevisionSnapshotBuilder.Request(request),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        // ── 6. Canonical recompute (instance copyable content may flip has_mixed / the projection) ──
        if (touchedInstances.Count > 0)
            await V2CanonicalRefresh.RecomputeAsync(_db, request, ct);

        request.RowVersion += 1;
        request.UpdatedAt = now;
        request.UpdatedBy = actorId;

        // No empty generic audit (plan CanhIter3FixBug, decision G): a contact-only call has nothing to
        // report here — its ProfileUpdated/RelationUpdated audits were already staged onto _db.AuditLogs
        // by ApplyContactPlanAsync above.
        if (changes.Count > 0)
        {
            var audit = new AuditLog
            {
                ActorUserId = actorId,
                Action = "VISIT_SAFE_FIELDS_UPDATED",
                EntityType = "VisitRequest",
                EntityId = request.VisitRequestId,
                VisitRequestId = request.VisitRequestId,
                CorrelationId = correlationId,
                SourceType = FormRevisionSourceTypes.SafeEdit,
                CreatedAt = now,
            };
            foreach (var c in changes)
                audit.Changes.Add(new AuditLogChange
                {
                    FieldName = c.InstanceId is null ? c.FieldPath : $"{c.FieldPath}#{c.InstanceId}",
                    OldValueText = c.OldValue,
                    NewValueText = c.NewValue,
                    CreatedAt = now,
                });
            _db.AuditLogs.Add(audit);
        }

        await _db.SaveChangesAsync(ct);

        return new VisitRequestSafeEditResponse(
            request.VisitRequestId,
            changes.Select(c => new SafeEditAppliedChange(c.FieldPath, c.InstanceId, c.Class))
                .Concat(contactAppliedChanges)
                .ToList(),
            request.RowVersion,
            request.CampusInstances.ToDictionary(c => c.VisitInstanceId, c => c.RowVersion),
            "Đã cập nhật các thông tin cho phép sửa nhanh.");
    }

    /// <summary>
    /// VALIDATE/PLAN pass for ONE instance's same-person contact-metadata/relation edit (plan
    /// CanhIter3FixBug). Returns null when the block is a genuine no-op (neither metadata nor relation
    /// actually differs) — that is never an error by itself (decision O); the caller's top-level guard
    /// is the only place a truly empty whole-request edit is refused. Everything here is read-only against
    /// <paramref name="detail"/>/<paramref name="instance"/> — see <see cref="ApplyContactPlanAsync"/> for
    /// the actual mutation, deferred until after this call's baselines are captured.
    /// </summary>
    private ContactMutationPlan? PlanContactMutation(
        VisitRequest request, VisitRequestCampus instance, VisitInstanceFormDetail detail,
        SafeContactPatchDto cp, ulong actorId, DateTime now, string correlationId)
    {
        // Contact-specific lifecycle (plan CanhIter3FixBug, decision M) — WIDER than generic Safe Edit's
        // Assigned/BeforeVisit-only window (WaitingContactConfirmation/WaitingRequestApproval/Assigned/
        // BeforeVisit), shared with UpdateOperationalContactProfileCommandHandler via OperationalContactLink
        // so the two doors cannot drift on this rule.
        OperationalContactLink.EnsureProfileUpdateLifecycleAllowed(request, instance);

        // The address is not this door's to move — same invariant UpdateOperationalContactProfileCommandHandler
        // enforces.
        var currentEmail = VisitRequestFingerprintBuilder.NormalizeEmail(detail.OperationalContactEmail);
        var incomingEmail = VisitRequestFingerprintBuilder.NormalizeEmail(cp.Email);
        if (!string.Equals(currentEmail, incomingEmail, StringComparison.Ordinal))
            throw new ConflictException(
                "Email đầu mối vận hành đã thay đổi. Đổi email là thay đổi người phụ trách và phải qua bước xác nhận.",
                OperationalContactErrorCodes.ChangeConflict);

        // FullName/JobTitle are NotEmpty at the FluentValidation layer already (Phase 1); Organization is
        // not (mirrors the Nationality manual-check pattern already used for the registrant block above).
        if (string.IsNullOrWhiteSpace(cp.Organization))
            throw new BusinessRuleException("Đơn vị công tác đầu mối vận hành không được để trống.",
                VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

        var normalized = OperationalContactProfileMutation.Normalize(cp.FullName, cp.Organization, cp.JobTitle, cp.Phone);

        var profileAudit = new AuditLog
        {
            ActorUserId = actorId,
            Action = OperationalContactHistoryAudit.ProfileUpdated,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            CorrelationId = correlationId,
            SourceType = "IDENTITY",
            CreatedAt = now,
        };
        var profileChanged = OperationalContactProfileMutation.AddProfileChanges(profileAudit, detail, normalized, now);

        // Effective-relation invariant (plan CanhIter3FixBug, decision N) — checked whenever the contact
        // block is present, NOT only inside an "if MemberLink supplied" branch: a metadata edit that
        // leaves the relation field untouched must still be proven consistent with whichever member the
        // contact is CURRENTLY linked to, or a typo fix could silently desync an existing, correct link.
        var effectiveRelationId = cp.MemberLink is { } link ? link.GuestMemberId : detail.OperationalContactGuestMemberId;
        var members = V2CanonicalRefresh.MembersOf(request, instance);
        if (effectiveRelationId is { } relId)
        {
            OperationalContactLink.EnsureGuestMemberIdEligible(members, relId);
            var member = members.First(m => m.GuestMemberId == relId);
            var proposedKey = PersonIdentity.Key(normalized.FullName, normalized.JobTitle, normalized.Organization);
            var memberKey = PersonIdentity.Key(member.FullName, member.JobTitle, member.Organization);
            if (!string.Equals(proposedKey, memberKey, StringComparison.Ordinal))
                throw new BusinessRuleException(
                    "Thông tin thành viên được chọn không khớp với đầu mối hiện tại. Hãy kiểm tra lại thông tin hoặc chọn đúng người.",
                    OperationalContactErrorCodes.RelationProfileMismatch);
        }

        var relationChanged = effectiveRelationId != detail.OperationalContactGuestMemberId;
        AuditLog? relationAudit = null;
        if (relationChanged)
        {
            // Durable human-readable snapshot (plan CanhIter3FixBug, decision D) — resolved NOW, while the
            // old value is still readable, and stored as plain text so later copy-on-write replacement of
            // the member row can never make this history entry unresolvable.
            var oldName = ResolveMemberDisplayName(detail.OperationalContactGuestMemberId, members);
            var newName = ResolveMemberDisplayName(effectiveRelationId, members);
            relationAudit = new AuditLog
            {
                ActorUserId = actorId,
                Action = OperationalContactHistoryAudit.RelationUpdated,
                EntityType = "VisitRequestCampus",
                EntityId = instance.VisitInstanceId,
                CampusId = instance.CampusId,
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                CorrelationId = correlationId,
                SourceType = "IDENTITY",
                CreatedAt = now,
            };
            relationAudit.Changes.Add(new AuditLogChange
            {
                FieldName = "operational_contact_relation",
                OldValueText = oldName,
                NewValueText = newName,
                CreatedAt = now,
            });
        }

        if (!profileChanged && !relationChanged)
            return null;

        return new ContactMutationPlan(
            instance, detail, profileChanged, normalized, profileChanged ? profileAudit : null,
            relationChanged, effectiveRelationId, relationAudit);
    }

    /// <summary>
    /// APPLY pass for one staged <see cref="ContactMutationPlan"/> — the only place
    /// <c>detail.OperationalContact*</c> is actually written. Called after baselines are captured
    /// (decision T).
    /// </summary>
    private async Task ApplyContactPlanAsync(ContactMutationPlan plan, CancellationToken ct)
    {
        if (plan.ProfileChanged)
        {
            OperationalContactProfileMutation.Apply(plan.Detail, plan.NormalizedProfile);
            _db.AuditLogs.Add(plan.ProfileAudit!);
            await OperationalContactProfileMutation.RefreshPendingInvitationSnapshotAsync(
                _invitations, plan.Instance, plan.Detail, ct);
        }
        if (plan.RelationChanged)
        {
            plan.Detail.OperationalContactGuestMemberId = plan.NewRelationId;
            _db.AuditLogs.Add(plan.RelationAudit!);
        }
    }

    /// <summary>Human-readable relation-history text (decision D) — never a raw id.</summary>
    private static string ResolveMemberDisplayName(ulong? guestMemberId, IReadOnlyList<VisitGuestMember> members)
    {
        if (guestMemberId is null) return "Không nằm trong danh sách đoàn";
        return members.FirstOrDefault(m => m.GuestMemberId == guestMemberId)?.FullName
            ?? "Không nằm trong danh sách đoàn";
    }

    /// <summary>
    /// Authoritative per-instance concurrency check (plan CanhIter3FixBug, decision R) — a real
    /// SELECT ... FOR UPDATE re-read, replicating <c>VisitInstanceConcurrencyGuard</c>'s logic inline
    /// (row_version is a plain int with no EF concurrency token behind it: IsConcurrencyToken is used
    /// nowhere in this codebase) while keeping this endpoint's EXISTING VisitFormConcurrencyConflict
    /// error code — VisitSafeEditV2Tests already asserts that code, and swapping it for
    /// VisitInstanceConcurrencyGuard's InstanceVersionConflict would be an unrelated breaking change to
    /// an established contract.
    /// </summary>
    private async Task EnsureInstanceUnchangedAsync(
        VisitRequestCampus instance, int expectedRowVersion, CancellationToken ct)
    {
        var rows = await _db.VisitRequestCampuses
            .FromSqlRaw("SELECT * FROM visit_request_campuses WHERE visit_instance_id = {0} FOR UPDATE",
                instance.VisitInstanceId)
            .AsNoTracking()
            .ToListAsync(ct);
        var current = rows.Count == 1 ? rows[0].RowVersion : (int?)null;
        if (current is null)
            throw new NotFoundException("VisitRequestCampus", instance.VisitInstanceId);

        var stale = instance.RowVersion != current.Value || expectedRowVersion != current.Value;
        if (stale)
            throw new ConflictException(
                "Lịch thăm tại một cơ sở đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                VisitFormV2ErrorCodes.VisitFormConcurrencyConflict);
    }

    /// <summary>True when the safe edit contains a PRIVACY_URGENT media withdrawal (for HIGH-priority notify).</summary>
    public static bool HasPrivacyUrgent(VisitRequestSafeEditResponse response)
        => response.AppliedChanges.Any(c => c.ChangeClass == AmendmentChangeClasses.PrivacyUrgent);

    private static void Diff(
        List<Change> changes, string fieldPath, ulong? instanceId,
        string? oldValue, string? newValue, Action<string?> apply)
    {
        if (string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal))
            return;
        var cls = VisitFieldClassifier.ClassifyChange(fieldPath, oldValue, newValue)
            ?? throw new BusinessRuleException(
                $"Trường '{fieldPath}' không được hỗ trợ.", VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
        changes.Add(new Change(fieldPath, instanceId, oldValue, newValue, cls, () => apply(newValue)));
    }

    private static List<VisitGuestMember> MembersOf(VisitRequest request, VisitRequestCampus instance)
        => V2CanonicalRefresh.MembersOf(request, instance);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// Rebuilds a request's canonical scope/mixed/fingerprint/compatibility-projection from the CURRENT
/// persisted per-campus state (details + linked members). Shared by the safe-edit and amendment services —
/// unlike the pending-edit flow there is no payload covering all campuses, so the source is the entities.
/// </summary>
internal static class V2CanonicalRefresh
{
    public static async Task RecomputeAsync(IApplicationDbContext db, VisitRequest request, CancellationToken ct)
    {
        var campusIds = request.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        var codesById = await db.Campuses.AsNoTracking()
            .Where(c => campusIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.CampusCode, ct);

        var contents = request.CampusInstances
            .OrderBy(c => c.CampusId)
            .Select(c => ToFormDto(request, c, codesById.TryGetValue(c.CampusId, out var code) ? code : c.CampusId.ToString()))
            .ToList();

        var scope = VisitRequestV2Canonical.ScopeOf(contents.Count);
        var hasMixed = VisitRequestV2Canonical.ComputeHasMixed(contents);
        var fingerprint = VisitRequestV2Canonical.BuildFingerprint(
            VisitRequestFingerprintBuilder.NormalizeEmail(request.RegistrantEmail),
            scope, contents);

        // Pure V2: form content lives ONLY in each campus's visit_instance_form_details. The request row
        // keeps identity, scope and lifecycle — it no longer mirrors one campus's content, so a mixed
        // request can never leak the smallest campus's values as if they were request-wide.
        request.VisitScope = scope;
        request.HasMixedCampusDetails = hasMixed;
        request.BusinessFingerprint = fingerprint;
    }

    public static List<VisitGuestMember> MembersOf(VisitRequest request, VisitRequestCampus instance)
    {
        var ids = instance.GuestMemberLinks.Select(l => l.GuestMemberId).ToHashSet();
        return request.GuestMembers.Where(m => ids.Contains(m.GuestMemberId))
            .OrderBy(m => m.DisplayOrder).ToList();
    }

    public static CampusVisitFormDto ToFormDto(VisitRequest request, VisitRequestCampus instance, string campusCode)
    {
        var d = instance.FormDetail!;
        var members = MembersOf(request, instance);
        return new CampusVisitFormDto(
            campusCode, instance.PlannedStartAt, instance.PlannedEndAt,
            d.DelegationName, d.VisitType ?? string.Empty, d.VisitTypeOther, d.Purpose ?? string.Empty, d.WorkingContent,
            members.Where(m => m.MemberType == "GUEST")
                .Select(m => new VisitorDto(m.FullName, m.Nationality ?? string.Empty, m.JobTitle ?? string.Empty,
                    m.Organization ?? string.Empty, m.OrganizationPartnerId)).ToList(),
            members.Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .Select(m => new SupportTeamMemberDto(m.FullName, m.JobTitle ?? string.Empty, m.Organization ?? string.Empty,
                    m.Nationality ?? string.Empty, m.OrganizationPartnerId)).ToList(),
            new ContactPointDto(
                d.OperationalContactFullName, d.OperationalContactOrganization ?? string.Empty,
                d.OperationalContactJobTitle, d.OperationalContactPhone, d.OperationalContactEmail),
            d.WorkingLanguage ?? "EN", d.TransportationNote, d.MediaConsentStatus ?? "DECLINED", d.Notes,
            null);
    }
}
