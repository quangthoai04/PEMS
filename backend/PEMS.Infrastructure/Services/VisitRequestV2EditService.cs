using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// Per-campus form pending-edit aggregate service (plan §6.4). Runs inside the caller's open transaction on
/// a TRACKED <see cref="VisitRequest"/> loaded with CampusInstances(+FormDetail,+GuestMemberLinks) and
/// GuestMembers; the caller re-checked authorization and lifecycle before calling and owns the commit.
/// Everything here re-validates the data-facing rules in-transaction: explicit optimistic row-version checks
/// (plain int columns — EF has no concurrency token on them), campus resolution/availability, schedule,
/// immutable account-binding fields, per-instance change detection, copy-on-write member replacement, campus
/// add/remove, canonical recompute (request scope, mixed-campus indicator and request fingerprint — facts
/// ABOUT the campus set; no request-level form projection is produced, form content stays on each campus
/// instance) and immutable revision history + audit.
/// </summary>
public sealed class VisitRequestV2EditService : IVisitRequestV2EditService
{
    private const int MinDurationMinutes = 30;

    /// <summary>
    /// Minimum lead time a NEW or CHANGED schedule must leave, measured from the moment of THIS action.
    ///
    /// <para>
    /// This is <see cref="VisitMutationPolicy.MinScheduleLeadHours"/> — the floor on a schedule being
    /// filed for approval — and NOT <see cref="VisitMutationPolicy.RequiredLeadHours"/>, which decides
    /// whether the action itself is still open. It used to be the latter, which quietly made "how late
    /// may I edit" and "how soon may the visit be" the same six hours: a request could be edited into a
    /// slot the campus had no time to prepare for. Create enforces the same floor, so the three write
    /// paths agree.
    /// </para>
    /// </summary>
    private const int EditWindowHours = VisitMutationPolicy.MinScheduleLeadHours;

    private readonly IApplicationDbContext _db;

    /// <summary>
    /// The canonical aggregate recompute. Held here so an instance resubmit can DERIVE the request
    /// status from its campuses instead of naming one — with a sibling already approved the answer is
    /// PARTIALLY_APPROVED, which no single hardcoded value in this class could have been.
    /// </summary>
    private readonly IVisitRequestAggregateStatusService _aggregateStatus;

    /// <summary>
    /// Only used to scope which partner profiles the editor was entitled to pick. Optional for the
    /// same reason as in <see cref="VisitRequestV2CreateService"/>: absent means "no session", which
    /// resolves to the PUBLIC option set — the narrowest one — so a missing wiring tightens the check
    /// rather than skipping it.
    /// </summary>
    private readonly ICurrentUserService? _currentUser;

    /// <summary>
    /// Nullable/optional like <see cref="_currentUser"/> below, for the same reason: dozens of existing
    /// tests construct this service directly with just (db, aggregateStatus) and never exercise the
    /// pending-invitation-snapshot refresh this drives (operational-contact consistency fix) — every
    /// campus they build either has no live invitation or isn't asserting on its freshness. A null here
    /// makes the refresh a no-op rather than forcing every call site to supply a fake.
    /// </summary>
    private readonly IOperationalContactInvitationService? _invitations;

    public VisitRequestV2EditService(
        IApplicationDbContext db,
        IVisitRequestAggregateStatusService aggregateStatus,
        ICurrentUserService? currentUser = null,
        IOperationalContactInvitationService? invitations = null)
    {
        _db = db;
        _aggregateStatus = aggregateStatus;
        _currentUser = currentUser;
        _invitations = invitations;
    }

    /// <summary>
    /// Every edit path re-sends the whole member list, so every edit path can smuggle in a partner id
    /// the editor was never offered. Validated here, once, against the REQUEST FORM's rule rather than
    /// the editor's session (PART-09): an edit is the same form as a create, so a profile a create may
    /// not cite is not one an edit may quietly introduce either.
    /// </summary>
    private Task EnsureMemberOrganizationsSelectableAsync(
        IEnumerable<CampusVisitEditV2Dto> contents, CancellationToken ct) =>
        GuestOrganizationPartnerPolicy.EnsureFormSelectableAsync(
            _db,
            contents.SelectMany(c =>
                (c.Visitors ?? new List<VisitorDto>()).Select(v => v.OrganizationPartnerId)
                    .Concat((c.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
                        .Select(m => m.OrganizationPartnerId))),
            _currentUser,
            ct);

    public async Task<V2EditResult> ApplyPendingEditAsync(
        VisitRequest request, VisitRequestEditV2Dto edit, ulong actorId, DateTime now, CancellationToken ct,
        bool allowShortNotice = false)
    {
        await EnsureMemberOrganizationsSelectableAsync(edit.CampusVisits, ct);

        // ── 0. Optimistic concurrency — request level (stable 409, never last-write-wins).
        //       row_version is a plain int (no EF concurrency token), so the guard is an explicit
        //       SELECT … FOR UPDATE against the CURRENT committed row: concurrent editors serialize on the
        //       lock and the loser sees the winner's bumped version → 409. ──
        await AssertCurrentRequestVersionAsync(request, edit.ExpectedRequestRowVersion, ct);

        if (edit.CampusVisits is null || edit.CampusVisits.Count == 0)
            throw new BusinessRuleException("Phải chọn ít nhất 1 cơ sở.", VisitRequestErrorCodes.InvalidVisitScope);

        // ── 1. Immutable account-binding + registrant snapshot (v1 parity, plan §5.1/§16) ──
        ValidateImmutableFields(request, edit);

        // ── 2. The campus set is IMMUTABLE (§18/§19). Every payload slot must name an instance this
        //       request already has, and every instance must appear exactly once — so an edit cannot add
        //       a campus (no id), drop one (missing from the payload) or swap one (id of another
        //       request). Enforced HERE, in the write path, rather than by hiding buttons: the rule is
        //       about what the request IS, and a client that keeps an old form open, or calls the API
        //       directly, must meet the same answer.
        //
        //       It used to be negotiable while every campus was still waiting, which meant the scope,
        //       the fingerprint and the set of people already invited could all change under a request
        //       that other people were holding links to. ──
        var instancesById = request.CampusInstances.ToDictionary(c => c.VisitInstanceId);
        var kept = new List<(CampusVisitEditV2Dto Content, VisitRequestCampus Instance)>();
        foreach (var cv in edit.CampusVisits)
        {
            if (cv.VisitInstanceId is not { } id || !instancesById.TryGetValue(id, out var instance))
                throw new BusinessRuleException(CampusSetImmutableMessage, VisitRequestErrorCodes.CampusSetImmutable);
            kept.Add((cv, instance));
        }
        if (kept.Select(k => k.Instance.VisitInstanceId).Distinct().Count() != request.CampusInstances.Count)
            throw new BusinessRuleException(CampusSetImmutableMessage, VisitRequestErrorCodes.CampusSetImmutable);

        // ── 3. Campus-code resolution + no-dup over the FINAL set ──
        var codes = edit.CampusVisits.Select(c => (c.CampusId ?? string.Empty).Trim().ToUpperInvariant()).ToList();
        if (codes.Any(string.IsNullOrEmpty))
            throw new BusinessRuleException("Thiếu mã cơ sở.", VisitRequestErrorCodes.CampusNotFound);
        if (codes.Distinct().Count() != codes.Count)
            throw new BusinessRuleException("Không được chọn trùng cơ sở.", VisitRequestErrorCodes.CampusNotFound);

        var campusIdsByCode = await _db.Campuses
            .Where(c => codes.Contains(c.CampusCode))
            .Select(c => new { c.CampusCode, c.CampusId })
            .ToDictionaryAsync(c => c.CampusCode, c => c.CampusId, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var code in codes)
            if (!campusIdsByCode.ContainsKey(code))
                throw new BusinessRuleException($"Cơ sở '{code}' không tồn tại.", VisitRequestErrorCodes.CampusNotFound);

        // Kept instances: the campus code must still resolve to the instance's campus — moving an
        // instance to another campus is remove + add, never an in-place mutation.
        foreach (var (content, instance) in kept)
            if (campusIdsByCode[content.CampusId.Trim().ToUpperInvariant()] != instance.CampusId)
                throw new BusinessRuleException(
                    "Không thể đổi cơ sở của một lịch thăm hiện có. Hãy bỏ cơ sở cũ và thêm cơ sở mới.",
                    VisitRequestErrorCodes.InstanceEditInvalid);

        // ── 4. Per-instance optimistic concurrency + defensive lifecycle re-check (in-transaction) ──
        foreach (var (content, instance) in kept)
        {
            if (content.ExpectedRowVersion is null || content.ExpectedRowVersion != instance.RowVersion)
                throw new ConflictException(
                    "Lịch thăm tại một cơ sở đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                    VisitRequestErrorCodes.InstanceVersionConflict);
            if (!IsPreDecision(instance.Status))
                throw new BusinessRuleException(
                    "Đơn đã có cơ sở được xử lý (duyệt/từ chối/hủy) nên không thể sửa.",
                    VisitRequestErrorCodes.VisitRequestNotEditable);
            // Refused during VALIDATION, before anything is written: an attempt to edit contact data
            // through this path is not a partial edit to be undone, it is a payload that does not
            // belong here at all.
            if (instance.FormDetail is not null)
                EnsureContactSnapshotUnchanged(instance.FormDetail, content.OperationalContact);
        }

        // ── 5. Schedule validation. The 72-hour registration floor applies ONLY to a campus whose
        //       schedule this edit actually MOVES (§28/§29): a guest correcting the purpose of a visit
        //       two days out is not proposing a new date, and refusing them because the existing date is
        //       now inside the floor would freeze the request with the mistake in it. The floor is not
        //       overridable here — this path belongs to the registrant, and only the campus's own Staff
        //       Leader may file a schedule inside it (per-campus pending edit). ──
        foreach (var (content, instance) in kept)
        {
            var scheduleMoves = instance.PlannedStartAt != content.PlannedStartAt
                                || instance.PlannedEndAt != content.PlannedEndAt;
            ValidateSchedule(
                content.CampusId, content.PlannedStartAt, content.PlannedEndAt, now,
                enforceLeadTime: scheduleMoves, allowShortNotice: allowShortNotice);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // All validation passed — apply. Correlated audit for the whole edit.
        // ─────────────────────────────────────────────────────────────────────────────
        var correlationId = Guid.NewGuid().ToString("N");
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "UPDATE_PENDING_VISIT_REQUEST_V2",
            EntityType = "VisitRequest",
            EntityId = request.VisitRequestId,
            VisitRequestId = request.VisitRequestId,
            CorrelationId = correlationId,
            // A full edit of a pending request is NOT a safe edit — it rewrites content across every
            // campus. Writing SAFE_EDIT here is what made the timeline report it as "sửa nhanh".
            SourceType = FormRevisionSourceTypes.PendingEdit,
            CreatedAt = now,
        };
        _db.AuditLogs.Add(audit);

        // ── 6. Per instance: change detection → apply only what changed ──
        // ContentChanged travels with each entry so the post-flush block below knows whether the
        // member links must be replaced (content changed) or the existing ones left alone
        // (schedule-only) — see VisitFieldClassifier's sibling rule in FormRevision semantics: a
        // saved campus always advances FormRevision by exactly one, regardless of how many of its
        // fields moved in that one save.
        var changedInstances =
            new List<(VisitRequestCampus Instance, bool ContentChanged, List<VisitGuestMember> NewMembers, CampusVisitEditV2Dto Content)>();
        foreach (var (content, instance) in kept)
        {
            var detail = instance.FormDetail
                ?? throw new ConflictException(
                    "Đơn thiếu dữ liệu chi tiết theo cơ sở (v2).", "VISIT_FORM_DETAIL_MISSING");

            var currentDto = CurrentContentOf(request, instance, detail);
            var contentChanged = VisitRequestV2Canonical.CanonicalContent(currentDto)
                                 != VisitRequestV2Canonical.CanonicalContent(content.ToFormDto());
            var scheduleChanged = instance.PlannedStartAt != content.PlannedStartAt
                                  || instance.PlannedEndAt != content.PlannedEndAt;

            // Relation validation — fail closed BEFORE any mutation to this campus (operational-contact
            // consistency fix). Pending Edit may never change WHO the contact is or WHETHER one exists —
            // that is Safe Edit's (link/unlink) or Replace/Transfer's job — it may only PRESERVE the same
            // persisted member across a content-changing rewrite. See the single-instance path's
            // identical block for the full reasoning; kept in sync deliberately rather than shared via a
            // helper across the two very different call shapes.
            if (!contentChanged)
            {
                if (content.OperationalContactGuestMemberId != detail.OperationalContactGuestMemberId)
                    throw PendingEditRelationError(
                        OperationalContactLink.ContactMemberContinuityResult.RelationKeyPointsElsewhere);
            }
            else
            {
                var incomingRows = (content.Visitors ?? new List<VisitorDto>())
                    .Select(v => (v.GuestMemberId, v.ClientMemberKey))
                    .Concat((content.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
                        .Select(m => (m.GuestMemberId, m.ClientMemberKey)));
                var continuity = OperationalContactLink.CheckPreservesExistingMemberRelation(
                    detail.OperationalContactGuestMemberId, incomingRows, content.OperationalContactClientMemberKey);
                if (continuity != OperationalContactLink.ContactMemberContinuityResult.Preserved)
                    throw PendingEditRelationError(continuity);
            }

            if (!contentChanged && !scheduleChanged)
                continue; // untouched sibling: no member churn, no revision bump, no row-version bump

            // This campus IS about to move to revision N+1. Capture revision N first if the chain is
            // missing it (legacy/seed data), while the schedule, the detail and the member links all
            // still hold their pre-edit values — three lines below, the schedule is already gone.
            await VisitRevisionBaselineGuard.EnsureInstanceBaselineAsync(
                _db, request, instance, detail, actorId, now, ct);

            if (scheduleChanged)
            {
                audit.Changes.Add(new AuditLogChange
                {
                    FieldName = $"instance[{instance.VisitInstanceId}].schedule",
                    OldValueText = $"{instance.PlannedStartAt:yyyy-MM-dd HH:mm}..{instance.PlannedEndAt:yyyy-MM-dd HH:mm}",
                    NewValueText = $"{content.PlannedStartAt:yyyy-MM-dd HH:mm}..{content.PlannedEndAt:yyyy-MM-dd HH:mm}",
                    CreatedAt = now,
                });
                instance.PlannedStartAt = content.PlannedStartAt;
                instance.PlannedEndAt = content.PlannedEndAt;
            }

            List<VisitGuestMember> newMembers = new();
            if (contentChanged)
            {
                audit.Changes.Add(new AuditLogChange
                {
                    FieldName = $"instance[{instance.VisitInstanceId}].form_content",
                    OldValueText = $"form_revision={detail.FormRevision}",
                    NewValueText = $"form_revision={detail.FormRevision + 1}",
                    CreatedAt = now,
                });
                VisitRequestV2EditOps.ApplyFormDetail(detail, content, now, actorId);
                // Full-replace THIS instance's members only; legacy shared rows survive via copy-on-write.
                newMembers = VisitRequestV2EditOps.StageReplaceMembers(
                    _db, request, instance, content.Visitors, content.ExternalSupportMembers, now, actorId);
            }
            else
            {
                // Schedule-only and/or relationship-only: still a save on this campus, so it still
                // advances FormRevision by exactly one regardless of how many of these two moved
                // together — the member list is untouched, so ApplyFormDetail (which would rewrite
                // content fields) is deliberately not called here.
                audit.Changes.Add(new AuditLogChange
                {
                    FieldName = $"instance[{instance.VisitInstanceId}].form_revision",
                    OldValueText = detail.FormRevision.ToString(),
                    NewValueText = (detail.FormRevision + 1).ToString(),
                    CreatedAt = now,
                });
                detail.FormRevision += 1;
                detail.RowVersion += 1;
                detail.UpdatedAt = now;
                detail.UpdatedBy = actorId;
            }

            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            changedInstances.Add((instance, contentChanged, newMembers, content));
        }

        // ── 7. Request-level common fields (mutable subset only) + canonical recompute ──
        // Captured BEFORE the write, used only if the write turns out to change something (see the
        // revision block further down). ApplyCommonFields rewrites the registrant columns in place, so
        // this string is the last moment the "before" exists anywhere.
        var requestBaselineJson = VisitRevisionBaselineGuard.CaptureRequestSnapshot(request);
        var commonChanged = ApplyCommonFields(request, edit, audit, now);

        var finalContents = edit.CampusVisits.Select(c => c.ToFormDto()).ToList();
        var scope = VisitRequestV2Canonical.ScopeOf(finalContents.Count);
        var hasMixed = VisitRequestV2Canonical.ComputeHasMixed(finalContents);
        var registrantEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(request.RegistrantEmail);
        var fingerprint = VisitRequestV2Canonical.BuildFingerprint(
            registrantEmailNorm, scope, finalContents);

        // Pure V2: each campus's content was already written to its own visit_instance_form_details.
        // The request row keeps identity, scope and lifecycle only — no compatibility projection.
        request.VisitScope = scope;
        request.HasMixedCampusDetails = hasMixed;
        request.BusinessFingerprint = fingerprint;
        request.RowVersion += 1;
        request.UpdatedAt = now;
        request.UpdatedBy = actorId;

        // ── FLUSH #1 — resolves form-detail PKs and new member ids. ──
        await _db.SaveChangesAsync(ct);

        // ── 8. Post-flush: composite links + immutable revision snapshots ──
        foreach (var (instance, contentChanged, newMembers, content) in changedInstances)
        {
            List<VisitGuestMember> snapshotMembers;
            if (contentChanged)
            {
                VisitRequestV2EditOps.LinkMembers(
                    _db, request, instance, newMembers, now, actorId,
                    VisitRequestV2EditOps.MemberKeys(content), content.OperationalContactClientMemberKey);
                snapshotMembers = newMembers;
                await SyncLinkedContactAfterRelinkAsync(request, instance, audit, now, ct);
            }
            else
            {
                // Schedule-only: members were never replaced, so there is nothing to (re)link —
                // the snapshot must reflect the CURRENT, still-linked members, never an empty list.
                snapshotMembers = V2CanonicalRefresh.MembersOf(request, instance);
            }
            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = instance.FormDetail!.FormRevision,
                ApprovalRevision = instance.FormDetail.ApprovalRevision,
                SourceType = FormRevisionSourceTypes.PendingEdit,
                SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, instance.FormDetail, snapshotMembers),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        if (commonChanged)
        {
            // The chain needs a first link before this becomes its second. Uses the snapshot captured
            // above, because the registrant columns now hold the AFTER values.
            await VisitRevisionBaselineGuard.EnsureRequestBaselineAsync(
                _db, request, requestBaselineJson, actorId, now, ct);

            // Counts the baseline just staged above as well as what is already persisted — see
            // NextRequestRevisionAsync. Reading the database alone numbered this row 1 on a request
            // whose chain was empty, colliding with the baseline's own 1.
            var nextRevision = await VisitRevisionBaselineGuard.NextRequestRevisionAsync(
                _db, request.VisitRequestId, ct);
            _db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                RequestRevision = nextRevision,
                SourceType = FormRevisionSourceTypes.PendingEdit,
                SnapshotJson = VisitFormRevisionSnapshotBuilder.Request(request),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        // ── FLUSH #2 — links + revisions. Caller commits. ──
        await _db.SaveChangesAsync(ct);
        await ResolvePartnerLinksAsync(request.VisitRequestId, now, actorId, ct);

        return new V2EditResult(scope, hasMixed, request.RowVersion);
    }

    public async Task<V2EditResult> ApplyResubmitAsync(
        VisitRequest request, VisitRequestEditV2Dto edit, ulong actorId, DateTime now, CancellationToken ct,
        bool allowShortNotice = false)
    {
        await EnsureMemberOrganizationsSelectableAsync(edit.CampusVisits, ct);

        // ── 0. Concurrency guard (FOR UPDATE — concurrent resubmits serialize; exactly one winner,
        //       the loser gets a stable 409 after seeing the winner's bumped version/status). ──
        await AssertCurrentRequestVersionAsync(request, edit.ExpectedRequestRowVersion, ct);

        // ── 1. Resubmittable gate re-checked IN-transaction: request REJECTED + EVERY campus REJECTED ──
        if (request.Status != VisitRequestStatuses.Rejected
            || request.CampusInstances.Count == 0
            || request.CampusInstances.Any(c => c.Status != VisitInstanceStatuses.Rejected))
            throw new BusinessRuleException(
                "Chỉ có thể gửi lại đơn khi toàn bộ yêu cầu đã bị từ chối ở tất cả các cơ sở.",
                VisitRequestErrorCodes.VisitRequestNotResubmittable);

        if (edit.CampusVisits is null || edit.CampusVisits.Count == 0)
            throw new BusinessRuleException("Phải chọn ít nhất 1 cơ sở.", VisitRequestErrorCodes.InvalidVisitScope);

        ValidateImmutableFields(request, edit);

        // ── 2. Campus set is FIXED and instance ids are KEPT (đổi campus ⇒ tạo đơn mới). Every slot must
        //       reference an existing instance; every existing instance must be present exactly once. ──
        var instancesById = request.CampusInstances.ToDictionary(c => c.VisitInstanceId);
        var pairs = new List<(CampusVisitEditV2Dto Content, VisitRequestCampus Instance)>();
        foreach (var cv in edit.CampusVisits)
        {
            if (cv.VisitInstanceId is not { } id || !instancesById.TryGetValue(id, out var instance))
                throw new BusinessRuleException(
                    "Không thể đổi danh sách cơ sở khi gửi lại đơn. Nếu muốn thăm cơ sở khác, vui lòng tạo đơn đăng ký mới.",
                    VisitRequestErrorCodes.ResubmitCampusListChanged);
            pairs.Add((cv, instance));
        }
        if (pairs.Select(p => p.Instance.VisitInstanceId).Distinct().Count() != request.CampusInstances.Count)
            throw new BusinessRuleException(
                "Không thể đổi danh sách cơ sở khi gửi lại đơn. Nếu muốn thăm cơ sở khác, vui lòng tạo đơn đăng ký mới.",
                VisitRequestErrorCodes.ResubmitCampusListChanged);

        var campusIdsByCode = await _db.Campuses
            .Where(c => request.CampusInstances.Select(i => i.CampusId).Contains(c.CampusId))
            .Select(c => new { c.CampusCode, c.CampusId })
            .ToDictionaryAsync(c => c.CampusCode, c => c.CampusId, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var (content, instance) in pairs)
        {
            var code = (content.CampusId ?? string.Empty).Trim().ToUpperInvariant();
            if (!campusIdsByCode.TryGetValue(code, out var campusId) || campusId != instance.CampusId)
                throw new BusinessRuleException(
                    "Không thể đổi danh sách cơ sở khi gửi lại đơn. Nếu muốn thăm cơ sở khác, vui lòng tạo đơn đăng ký mới.",
                    VisitRequestErrorCodes.ResubmitCampusListChanged);
        }

        // ── 3. Per-instance optimistic concurrency + contact-snapshot immutability ──
        foreach (var (content, instance) in pairs)
        {
            if (content.ExpectedRowVersion is null || content.ExpectedRowVersion != instance.RowVersion)
                throw new ConflictException(
                    "Lịch thăm tại một cơ sở đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                    VisitRequestErrorCodes.InstanceVersionConflict);
            // Resubmit rewrites content wholesale, which makes it the other path a contact edit could
            // sneak in through. Same guard, same codes, same place in the sequence: before any write.
            if (instance.FormDetail is not null)
                EnsureContactSnapshotUnchanged(instance.FormDetail, content.OperationalContact);
        }

        // ── 4. New schedule: end > start, ≥ 30 min, and every start ≥ NOW + MinScheduleLeadHours.
        //       Measured from this moment, never from when the request was originally filed: a request
        //       created on the 1st for the 10th was valid then, and resubmitting it on the 9th is a
        //       fresh ask that the campus has one day to answer. ──
        ValidateSchedules(edit, now, allowShortNotice);

        // ── 5. Every campus must STILL be operationally available (re-entry uses the same bar as create) ──
        var campusIds = request.CampusInstances.Select(c => c.CampusId).ToList();
        var availability = await CampusAvailabilityEvaluator.EvaluateAsync(_db, campusIds, ct);
        var leadersByCampus = new Dictionary<ulong, ulong>();
        foreach (var (content, instance) in pairs)
        {
            var s = availability.TryGetValue(instance.CampusId, out var snap)
                ? snap
                : throw new BusinessRuleException("Cơ sở không tồn tại.", VisitRequestErrorCodes.CampusNotFound);
            if (!string.Equals(s.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException($"Cơ sở '{s.CampusCode}' hiện không hoạt động.", VisitRequestErrorCodes.CampusInactive);
            if (s.ValidStaffLeaderCount == 0)
                throw new BusinessRuleException($"Cơ sở {s.Name} chưa có Staff Leader đang hoạt động nên chưa thể tiếp nhận lại yêu cầu.", VisitRequestErrorCodes.CampusHasNoActiveStaffLeader);
            if (!s.IsAvailableForVisitRegistration)
                throw new BusinessRuleException($"Cấu hình tiếp nhận của cơ sở {s.Name} không hợp lệ.", VisitRequestErrorCodes.CampusStaffLeaderConfigurationInvalid);
            leadersByCampus[instance.CampusId] = s.ValidStaffLeaderUserId!.Value;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Apply. Old rejection history is NEVER deleted — the decisions are snapshotted to
        // audit_log_changes (v1 parity §7.3) and the old revision rows stay untouched.
        // ─────────────────────────────────────────────────────────────────────────────
        var correlationId = Guid.NewGuid().ToString("N");
        var oldCount = request.ResubmissionCount;

        var deciderSnapshot = request.CampusInstances
            .OrderBy(c => c.CampusId)
            .Select(c => new
            {
                visitInstanceId = c.VisitInstanceId,
                campusId = c.CampusId,
                oldStatus = c.Status,
                decidedBy = c.DecidedBy,
                decidedAt = c.DecidedAt,
                decisionActorRole = c.DecisionActorRole,
                decisionNote = c.DecisionNote,
            })
            .ToList();

        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "RESUBMIT_REJECTED_VISIT_REQUEST_V2",
            EntityType = "VisitRequest",
            EntityId = request.VisitRequestId,
            VisitRequestId = request.VisitRequestId,
            CorrelationId = correlationId,
            SourceType = "RESUBMIT",
            CreatedAt = now,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "request.status",
            OldValueText = VisitRequestStatuses.Rejected,
            NewValueText = VisitRequestStatuses.PendingApproval,
            CreatedAt = now,
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "resubmission_count",
            OldValueText = oldCount.ToString(),
            NewValueText = (oldCount + 1).ToString(),
            CreatedAt = now,
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "campus_decisions_before_resubmit_json",
            OldValueText = System.Text.Json.JsonSerializer.Serialize(deciderSnapshot),
            NewValueText = "cleared_for_resubmission",
            CreatedAt = now,
        });
        _db.AuditLogs.Add(audit);

        // ── Phase 1: request-level fields + status back to PENDING_APPROVAL + canonical recompute. The
        //    parent MUST be flushed before any instance status flips (campus trigger only allows
        //    REJECTED → WAITING_REQUEST_APPROVAL under a pending parent). ──
        var finalContents = edit.CampusVisits.Select(c => c.ToFormDto()).ToList();
        var scope = VisitRequestV2Canonical.ScopeOf(finalContents.Count);
        var hasMixed = VisitRequestV2Canonical.ComputeHasMixed(finalContents);
        var fingerprint = VisitRequestV2Canonical.BuildFingerprint(
            VisitRequestFingerprintBuilder.NormalizeEmail(request.RegistrantEmail),
            scope, finalContents);

        // Baselines BEFORE anything is overwritten. The campuses first, while their details, schedules
        // and member links still hold the rejected content — phase 2 below replaces all three, and
        // after that the "before" for this resubmit's revision would be unrecoverable.
        foreach (var (_, instance) in pairs)
            if (instance.FormDetail is { } rejectedDetail)
                await VisitRevisionBaselineGuard.EnsureInstanceBaselineAsync(
                    _db, request, instance, rejectedDetail, actorId, now, ct);

        var requestBaselineJson = VisitRevisionBaselineGuard.CaptureRequestSnapshot(request);
        var commonChanged = ApplyCommonFields(request, edit, audit, now);
        request.VisitScope = scope;
        request.HasMixedCampusDetails = hasMixed;
        request.BusinessFingerprint = fingerprint;
        // Pure V2: resubmitted content was written per campus; the request row carries no form content.
        request.Status = VisitRequestStatuses.PendingApproval;
        request.ResubmissionCount = oldCount + 1;
        request.LastResubmittedAt = now;
        request.LastResubmittedBy = actorId;
        request.CancelledBy = null;
        request.CancelledAt = null;
        request.CancellationReason = null;
        request.RowVersion += 1;
        request.UpdatedAt = now;
        request.UpdatedBy = actorId;

        await _db.SaveChangesAsync(ct); // FLUSH #1 — parent is PENDING_APPROVAL

        // ── Phase 2: per instance — KEEP the id; replace content + members (copy-on-write), clear the old
        //    decision (already snapshotted), reset to WAITING and re-route to the CURRENT Staff Leader. ──
        var staging = new List<(VisitRequestCampus Instance, List<VisitGuestMember> Members, CampusVisitEditV2Dto Content)>();
        foreach (var (content, instance) in pairs)
        {
            // Relation continuity — fail closed BEFORE any mutation to this campus (operational-contact
            // consistency fix). Resubmit always full-rewrites every campus's members, so it always goes
            // through the same COW continuity proof Pending Edit's content-changed branch uses — there
            // is no "relationship-only" concept here at all (verified: this method never reads an
            // OperationalContactGuestMemberId off the payload as a direct pick, only the ephemeral
            // OperationalContactClientMemberKey via LinkMembers below), so the only failure modes are
            // the disguised-repoint case and the stale-client case, both live-request scenarios since
            // this is a real browser session resubmitting.
            var incomingRows = (content.Visitors ?? new List<VisitorDto>())
                .Select(v => (v.GuestMemberId, v.ClientMemberKey))
                .Concat((content.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
                    .Select(m => (m.GuestMemberId, m.ClientMemberKey)));
            var continuity = OperationalContactLink.CheckPreservesExistingMemberRelation(
                instance.FormDetail!.OperationalContactGuestMemberId, incomingRows,
                content.OperationalContactClientMemberKey);
            if (continuity != OperationalContactLink.ContactMemberContinuityResult.Preserved)
                throw LiveRequestRelationError(continuity);

            VisitRequestV2EditOps.ApplyFormDetail(instance.FormDetail!, content, now, actorId);
            var newMembers = VisitRequestV2EditOps.StageReplaceMembers(
                _db, request, instance, content.Visitors, content.ExternalSupportMembers, now, actorId);

            instance.PlannedStartAt = content.PlannedStartAt;
            instance.PlannedEndAt = content.PlannedEndAt;
            instance.Status = VisitInstanceStatuses.WaitingRequestApproval;
            instance.DecisionActorRole = null;
            instance.DecisionSource = null;
            instance.DecidedBy = null;
            instance.DecidedAt = null;
            instance.DecisionNote = null;
            instance.CurrentHostUserId = null;
            instance.HostAssignedBy = null;
            instance.HostAssignedAt = null;
            instance.CancelledBy = null;
            instance.CancelledAt = null;
            instance.CancellationActorType = null;
            instance.CancellationSource = null;
            instance.CancellationReason = null;
            instance.CoordinatorUserId = leadersByCampus[instance.CampusId];
            instance.CoordinatorAssignedBy = actorId;
            instance.CoordinatorAssignedAt = now;
            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            staging.Add((instance, newMembers, content));
        }

        await _db.SaveChangesAsync(ct); // FLUSH #2 — instance resets + new member ids

        // ── Phase 3: member links + RESUBMIT revision snapshots (history keeps every prior revision). ──
        foreach (var (instance, members, content) in staging)
        {
            VisitRequestV2EditOps.LinkMembers(
                _db, request, instance, members, now, actorId,
                VisitRequestV2EditOps.MemberKeys(content), content.OperationalContactClientMemberKey);
            await SyncLinkedContactAfterRelinkAsync(request, instance, audit, now, ct);
            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = instance.FormDetail!.FormRevision,
                ApprovalRevision = instance.FormDetail.ApprovalRevision,
                SourceType = "RESUBMIT",
                SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, instance.FormDetail, members),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        // A resubmit ALWAYS writes a request revision, so the chain always needs its first link —
        // unconditionally here, unlike the pending edit which only writes one when something changed.
        await VisitRevisionBaselineGuard.EnsureRequestBaselineAsync(
            _db, request, requestBaselineJson, actorId, now, ct);

        // Same as the pending-edit path: the baseline staged immediately above is invisible to a
        // database MAX, and a resubmit ALWAYS writes a revision, so this collided every time the
        // chain started empty.
        var nextRevision = await VisitRevisionBaselineGuard.NextRequestRevisionAsync(
            _db, request.VisitRequestId, ct);
        _db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
        {
            VisitRequestId = request.VisitRequestId,
            RequestRevision = nextRevision,
            SourceType = "RESUBMIT",
            SnapshotJson = VisitFormRevisionSnapshotBuilder.Request(request),
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = correlationId,
        });
        _ = commonChanged; // resubmit always records a request revision; the audit already carries field diffs

        await _db.SaveChangesAsync(ct); // FLUSH #3 — links + revisions. Caller commits.
        await ResolvePartnerLinksAsync(request.VisitRequestId, now, actorId, ct);

        return new V2EditResult(scope, hasMixed, request.RowVersion);
    }

    /// <inheritdoc />
    public async Task<V2EditResult> ApplyInstancePendingEditAsync(
        VisitRequest request, VisitRequestCampus instance, CampusVisitEditV2Dto content,
        ulong actorId, DateTime now, bool actorIsCampusLeader, bool overrideLeadTimeConfirmed,
        bool approveAfterSaveRequested, CancellationToken ct,
        bool allowShortNotice = false)
    {
        await EnsureMemberOrganizationsSelectableAsync(new[] { content }, ct);

        // ── 1. THIS campus must still be waiting for its own decision. Nothing is asked about the
        //       request aggregate on purpose: with a sibling already approved the request reads
        //       PARTIALLY_APPROVED, and letting that decide would re-create the dead end this exists to
        //       remove — a campus nobody has answered yet, unfixable because a different campus was
        //       answered. ──
        if (request.Status == VisitRequestStatuses.Cancelled)
            throw new BusinessRuleException(
                "Đơn đã bị hủy nên không thể sửa cơ sở này.",
                VisitRequestErrorCodes.PendingCampusNotEditable);
        // Either PRE-DECISION stage. A campus still waiting for its operational contact to confirm has
        // been decided by nobody, exactly like one waiting for approval — same door, same rule as
        // VisitMutationPolicy.IsPreDecision, which is what granted the capability the UI rendered.
        if (instance.Status is not (VisitInstanceStatuses.WaitingContactConfirmation
                                 or VisitInstanceStatuses.WaitingRequestApproval))
            throw new BusinessRuleException(
                "Chỉ có thể sửa cơ sở khi cơ sở đó chưa được duyệt.",
                VisitRequestErrorCodes.PendingCampusNotEditable);

        // ── 2. The campus may not be swapped — that is an add and a remove wearing one payload. ──
        var campusCode = (content.CampusId ?? string.Empty).Trim().ToUpperInvariant();
        var namedCampusId = await _db.Campuses
            .Where(c => c.CampusCode == campusCode)
            .Select(c => (ulong?)c.CampusId)
            .FirstOrDefaultAsync(ct);
        if (namedCampusId is null || namedCampusId != instance.CampusId)
            throw new BusinessRuleException(CampusSetImmutableMessage, VisitRequestErrorCodes.CampusSetImmutable);

        // ── 3. Optimistic concurrency on the INSTANCE alone. The request row version is deliberately
        //       not the guard: a sibling being approved bumps it, and that must not brick an edit of a
        //       campus nobody has touched.
        //
        //       The instance row is LOCKED first, for the same reason the whole-request path locks the
        //       request: row_version is a plain int with no EF concurrency token, so two editors who
        //       both read version 4 would both pass a bare comparison and the second would silently win.
        //       With the lock they serialize, and the loser wakes up seeing the winner's bump. ──
        await AssertCurrentInstanceVersionAsync(instance, content.ExpectedRowVersion, ct);

        if (instance.FormDetail is not null)
            EnsureContactSnapshotUnchanged(instance.FormDetail, content.OperationalContact);

        // ── 4. Schedule. The 72-hour floor applies only if this edit MOVES the dates (§28/§29), and the
        //       campus's own Staff Leader may file inside it once they confirm they mean to (§30/§31). ──
        var scheduleChanged = instance.PlannedStartAt != content.PlannedStartAt
                              || instance.PlannedEndAt != content.PlannedEndAt;
        var oldStart = instance.PlannedStartAt;
        var oldEnd = instance.PlannedEndAt;
        ValidateSchedule(
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt, now,
            enforceLeadTime: scheduleChanged,
            leaderMayOverride: actorIsCampusLeader,
            overrideConfirmed: overrideLeadTimeConfirmed,
            allowShortNotice: allowShortNotice);
        // Either door that can land a schedule inside the floor: the campus leader's confirmed override,
        // or the (broader) internal-registrant short-notice capability — an actor who satisfies the
        // former always satisfies the latter too (see IsShortNoticeEligible's remarks), so this is a
        // superset of the old condition, not a replacement of it.
        var usedLeadTimeOverride = scheduleChanged
            && (actorIsCampusLeader || allowShortNotice)
            && content.PlannedStartAt < now.AddHours(VisitMutationPolicy.MinScheduleLeadHours);

        var detail = instance.FormDetail
            ?? throw new ConflictException(
                "Đơn thiếu dữ liệu chi tiết theo cơ sở (v2).", VisitFormV2ErrorCodes.VisitFormDetailMissing);

        var contentChanged = VisitRequestV2Canonical.CanonicalContent(CurrentContentOf(request, instance, detail))
                             != VisitRequestV2Canonical.CanonicalContent(content.ToFormDto());

        // Relation validation — fail closed BEFORE anything is mutated (operational-contact consistency
        // fix). Pending Edit may never change WHO the contact is or WHETHER one exists — only Safe Edit
        // (link/unlink) or Replace/Transfer may — it may only PRESERVE the same persisted member across
        // a content-changing rewrite. See the whole-request path's identical block for the full
        // reasoning; kept in sync deliberately rather than shared via a helper, since the two methods'
        // surrounding control flow (single target vs. multi-campus loop) differ enough that a shared
        // helper would need to take half its own state as parameters anyway.
        if (!contentChanged)
        {
            if (content.OperationalContactGuestMemberId != detail.OperationalContactGuestMemberId)
                throw PendingEditRelationError(
                    OperationalContactLink.ContactMemberContinuityResult.RelationKeyPointsElsewhere);
        }
        else
        {
            var incomingRows = (content.Visitors ?? new List<VisitorDto>())
                .Select(v => (v.GuestMemberId, v.ClientMemberKey))
                .Concat((content.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
                    .Select(m => (m.GuestMemberId, m.ClientMemberKey)));
            var continuity = OperationalContactLink.CheckPreservesExistingMemberRelation(
                detail.OperationalContactGuestMemberId, incomingRows, content.OperationalContactClientMemberKey);
            if (continuity != OperationalContactLink.ContactMemberContinuityResult.Preserved)
                throw PendingEditRelationError(continuity);
        }

        if (!contentChanged && !scheduleChanged)
        {
            // "Lưu và duyệt" carries two intents — an edit and a decision — and having nothing to
            // EDIT does not mean there is nothing to DECIDE. The caller still has an approval to run
            // in the same transaction, so this is a no-op rather than a refusal: no revision, no audit
            // row, no row-version bump, and the request/instance come back exactly as they are now.
            if (approveAfterSaveRequested)
                return new V2EditResult(request.VisitScope, request.HasMixedCampusDetails, request.RowVersion);

            throw new BusinessRuleException(
                "Không có thay đổi nào để lưu cho cơ sở này.",
                VisitFormV2ErrorCodes.PendingCampusNoContentChanges);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Apply — target-only. There is no loop over campuses here, which is the point: a sibling's
        // status, host, decision, revision and row version cannot move because nothing writes them.
        // ─────────────────────────────────────────────────────────────────────────────
        var correlationId = Guid.NewGuid().ToString("N");
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "UPDATE_PENDING_VISIT_INSTANCE_V2",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            CorrelationId = correlationId,
            SourceType = FormRevisionSourceTypes.PendingEdit,
            CreatedAt = now,
        };
        _db.AuditLogs.Add(audit);

        // Revision N+1 is about to be written for this campus. Capture N first if the chain is
        // missing it, while the detail, the schedule and the member links are all still pre-edit.
        await VisitRevisionBaselineGuard.EnsureInstanceBaselineAsync(
            _db, request, instance, detail, actorId, now, ct);

        List<VisitGuestMember> newMembers = new();
        if (contentChanged)
        {
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = $"instance[{instance.VisitInstanceId}].form_content",
                OldValueText = $"form_revision={detail.FormRevision}",
                NewValueText = $"form_revision={detail.FormRevision + 1}",
                CreatedAt = now,
            });
            VisitRequestV2EditOps.ApplyFormDetail(detail, content, now, actorId);
            newMembers = VisitRequestV2EditOps.StageReplaceMembers(
                _db, request, instance, content.Visitors, content.ExternalSupportMembers, now, actorId);
        }
        else
        {
            // Schedule-only and/or relationship-only (the only other ways this method reaches here —
            // see the throw above): this campus is still being saved, so FormRevision still advances
            // by exactly one regardless of how many of these two moved together. Members are
            // untouched, so ApplyFormDetail — which would rewrite content fields — is not called.
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = $"instance[{instance.VisitInstanceId}].form_revision",
                OldValueText = detail.FormRevision.ToString(),
                NewValueText = (detail.FormRevision + 1).ToString(),
                CreatedAt = now,
            });
            detail.FormRevision += 1;
            detail.RowVersion += 1;
            detail.UpdatedAt = now;
            detail.UpdatedBy = actorId;
        }

        if (scheduleChanged)
        {
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = $"instance[{instance.VisitInstanceId}].schedule",
                OldValueText = $"{oldStart:yyyy-MM-dd HH:mm}..{oldEnd:yyyy-MM-dd HH:mm}",
                NewValueText = $"{content.PlannedStartAt:yyyy-MM-dd HH:mm}..{content.PlannedEndAt:yyyy-MM-dd HH:mm}",
                CreatedAt = now,
            });
            instance.PlannedStartAt = content.PlannedStartAt;
            instance.PlannedEndAt = content.PlannedEndAt;
        }

        // An override is a decision somebody took, not a validation that happened to pass. It gets its
        // own audit row — actor, campus, old and new start — so "why is this visit in two days when the
        // rule says three" has an answer that does not depend on reading the code.
        if (usedLeadTimeOverride)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                Action = VisitAuditActions.LeadTimeOverride,
                EntityType = "VisitRequestCampus",
                EntityId = instance.VisitInstanceId,
                CampusId = instance.CampusId,
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                CorrelationId = correlationId,
                SourceType = VisitAuditActions.LeadTimeOverrideSourceType,
                Reason = $"required_lead_hours={VisitMutationPolicy.MinScheduleLeadHours};" +
                         $"old_start={oldStart:yyyy-MM-dd HH:mm};new_start={content.PlannedStartAt:yyyy-MM-dd HH:mm}",
                CreatedAt = now,
            });
        }

        instance.RowVersion += 1;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;

        // The campus stays WAITING_REQUEST_APPROVAL — editing is not deciding (§30.1). The aggregate is
        // recomputed rather than assumed, so a request with one campus approved keeps reading
        // PARTIALLY_APPROVED instead of being dragged back to PENDING by an edit of its other campus.
        _aggregateStatus.Apply(request);
        request.RowVersion += 1;
        request.UpdatedAt = now;
        request.UpdatedBy = actorId;

        await _db.SaveChangesAsync(ct);

        // contentChanged || scheduleChanged is guaranteed true here (the method throws earlier if
        // neither moved), so this campus always gets a new revision row — content changes relink
        // members via LinkMembers; a schedule-only save reads back the CURRENT, untouched members
        // instead of relinking (there is nothing to relink) so the snapshot is never empty.
        var snapshotMembers = contentChanged
            ? newMembers
            : V2CanonicalRefresh.MembersOf(request, instance);
        if (contentChanged)
        {
            VisitRequestV2EditOps.LinkMembers(
                _db, request, instance, newMembers, now, actorId,
                VisitRequestV2EditOps.MemberKeys(content), content.OperationalContactClientMemberKey);
            await SyncLinkedContactAfterRelinkAsync(request, instance, audit, now, ct);
        }
        _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
        {
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            FormRevision = detail.FormRevision,
            ApprovalRevision = detail.ApprovalRevision,
            SourceType = FormRevisionSourceTypes.PendingEdit,
            SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, detail, snapshotMembers),
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = correlationId,
        });
        await _db.SaveChangesAsync(ct);

        // Scope / mixed / fingerprint are facts about the campus SET and its content, so they are
        // rebuilt from the persisted campuses — this path has no payload covering the siblings and must
        // never infer theirs from the one campus it was given.
        await V2CanonicalRefresh.RecomputeAsync(_db, request, ct);
        await _db.SaveChangesAsync(ct);
        await ResolvePartnerLinksAsync(request.VisitRequestId, now, actorId, ct);

        return new V2EditResult(request.VisitScope, request.HasMixedCampusDetails, request.RowVersion);
    }

    /// <inheritdoc />
    /// <remarks>
    /// SCHEDULE-ONLY (plan CanhIter3FixBug FIX-G/H). The old version of this method took a full
    /// <see cref="CampusVisitEditV2Dto"/> and called <c>ApplyFormDetail</c> + <c>StageReplaceMembers</c>
    /// + <c>LinkMembers</c> + <c>ResolvePartnerLinksAsync</c> — a full content/member copy-on-write
    /// replace — even though the UI (<c>InstanceResubmitPanel.tsx</c>) only ever offered the two dates
    /// for editing and echoed the rest back. That echo silently dropped every guest's
    /// <c>OrganizationPartnerId</c> (the panel's payload never carried it), which then wrote NULL onto
    /// the freshly copy-on-written member rows, and forced the operational-contact link to re-resolve by
    /// name/org/title fingerprint on every resubmit instead of staying pinned to the same member. This
    /// version does not call any of those four — member rows, their partner ids and the
    /// operational-contact link are never staged, linked or resolved, so there is nothing for them to
    /// lose.
    /// </remarks>
    public async Task<V2EditResult> ApplyInstanceResubmitAsync(
        VisitRequest request, VisitRequestCampus instance, InstanceResubmitScheduleDto content,
        ulong actorId, DateTime now, CancellationToken ct,
        bool allowShortNotice = false)
    {
        // ── 1. Only THIS campus need be rejected. Deliberately not the whole-request gate: a campus
        //       refused beside one that was approved is exactly the case this exists for. ──
        if (request.Status == VisitRequestStatuses.Cancelled)
            throw new BusinessRuleException(
                "Đơn đã bị hủy nên không thể gửi lại cơ sở này.",
                VisitRequestErrorCodes.VisitRequestNotResubmittable);

        if (instance.Status != VisitInstanceStatuses.Rejected)
            throw new BusinessRuleException(
                "Chỉ có thể gửi lại cơ sở đang ở trạng thái bị từ chối.",
                VisitRequestErrorCodes.VisitRequestNotResubmittable);

        // ── 2. The campus may not be swapped: the payload must name the campus this instance already is.
        //       Wanting a different campus is a new request, exactly as for the whole-request resubmit. ──
        var campusCode = (content.CampusId ?? string.Empty).Trim().ToUpperInvariant();
        var namedCampusId = await _db.Campuses
            .Where(c => c.CampusCode == campusCode)
            .Select(c => (ulong?)c.CampusId)
            .FirstOrDefaultAsync(ct);
        if (namedCampusId is null || namedCampusId != instance.CampusId)
            throw new BusinessRuleException(
                "Không thể đổi cơ sở khi gửi lại. Nếu muốn thăm cơ sở khác, vui lòng tạo đơn đăng ký mới.",
                VisitRequestErrorCodes.ResubmitCampusListChanged);

        // ── 3. Optimistic concurrency on the INSTANCE. The request row version is deliberately not the
        //       guard here: a sibling campus being decided bumps it, and that must not brick a resubmit
        //       of this one.
        //
        //       LOCKED first, same as every other instance-scoped mutation in this class
        //       (AssertCurrentInstanceVersionAsync's own doc: row_version is a plain int with no EF
        //       concurrency token, so two callers who both read the same version would both pass a bare
        //       comparison and the second would silently overwrite the first). A bare compare against the
        //       already-loaded `instance` used to stand here instead — it could not actually catch two
        //       genuinely concurrent resubmits, only a caller replaying a version it saw before this
        //       method ran at all. ──
        await AssertCurrentInstanceVersionAsync(instance, content.ExpectedRowVersion, ct);

        // No EnsureContactSnapshotUnchanged / EnsureMemberOrganizationsSelectableAsync here any more —
        // the payload carries no contact and no member data at all for either of those guards to check.

        // ── 4. Registration lead time. This IS a resubmit, so the 72h floor applies to the new start
        //       (plan §17) — measured from now, never from when the request was first filed. An internal
        //       registrant resubmitting their OWN campus is exempt from the floor itself, never from the
        //       future-time invariant below it (see ValidateSchedule). ──
        ValidateSchedule(
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt, now,
            allowShortNotice: allowShortNotice);

        // ── 5. The campus must still be able to take a visit at all — same bar as create. ──
        var availability = await CampusAvailabilityEvaluator.EvaluateAsync(_db, new[] { instance.CampusId }, ct);
        var snapshot = availability.TryGetValue(instance.CampusId, out var snap)
            ? snap
            : throw new BusinessRuleException("Cơ sở không tồn tại.", VisitRequestErrorCodes.CampusNotFound);
        if (!string.Equals(snapshot.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException($"Cơ sở '{snapshot.CampusCode}' hiện không hoạt động.", VisitRequestErrorCodes.CampusInactive);
        if (snapshot.ValidStaffLeaderCount == 0)
            throw new BusinessRuleException($"Cơ sở {snapshot.Name} chưa có Staff Leader đang hoạt động nên chưa thể tiếp nhận lại yêu cầu.", VisitRequestErrorCodes.CampusHasNoActiveStaffLeader);
        if (!snapshot.IsAvailableForVisitRegistration)
            throw new BusinessRuleException($"Cấu hình tiếp nhận của cơ sở {snapshot.Name} không hợp lệ.", VisitRequestErrorCodes.CampusStaffLeaderConfigurationInvalid);

        var detail = instance.FormDetail
            ?? throw new ConflictException(
                "Đơn thiếu dữ liệu chi tiết theo cơ sở (v2).", VisitFormV2ErrorCodes.VisitFormDetailMissing);

        // ─────────────────────────────────────────────────────────────────────────────
        // Apply. The rejection is snapshotted to audit before it is cleared — the DB refuses to hold
        // decision metadata on a campus that is back in review, so clearing it is not optional.
        // ─────────────────────────────────────────────────────────────────────────────
        var correlationId = Guid.NewGuid().ToString("N");
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "RESUBMIT_REJECTED_VISIT_INSTANCE_V2",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            CorrelationId = correlationId,
            SourceType = "RESUBMIT",
            CreatedAt = now,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "visit_request_campuses.status",
            OldValueText = VisitInstanceStatuses.Rejected,
            NewValueText = VisitInstanceStatuses.WaitingRequestApproval,
            CreatedAt = now,
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "campus_decision_before_resubmit_json",
            OldValueText = System.Text.Json.JsonSerializer.Serialize(new
            {
                visitInstanceId = instance.VisitInstanceId,
                campusId = instance.CampusId,
                oldStatus = instance.Status,
                decidedBy = instance.DecidedBy,
                decidedAt = instance.DecidedAt,
                decisionActorRole = instance.DecisionActorRole,
                decisionNote = instance.DecisionNote,
            }),
            NewValueText = "cleared_for_resubmission",
            CreatedAt = now,
        });
        _db.AuditLogs.Add(audit);

        // Revision N+1 is about to be written for this campus. Capture N first if the chain is missing
        // it, exactly like every other schedule-only save site in this class (ApplyPendingEditAsync's
        // and ApplyInstancePendingEditAsync's schedule-only branches) — while the detail, the schedule
        // and the member links are all still pre-resubmit.
        await VisitRevisionBaselineGuard.EnsureInstanceBaselineAsync(
            _db, request, instance, detail, actorId, now, ct);

        // ── Phase 1: THIS campus only, schedule + lifecycle. Content, members and the
        //    operational-contact link are never touched — see the class remarks above. Every sibling's
        //    status, decision, host and schedule are untouched too — the loop that would have reset them
        //    does not exist here. ──
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = $"instance[{instance.VisitInstanceId}].schedule",
            OldValueText = $"{instance.PlannedStartAt:yyyy-MM-dd HH:mm}..{instance.PlannedEndAt:yyyy-MM-dd HH:mm}",
            NewValueText = $"{content.PlannedStartAt:yyyy-MM-dd HH:mm}..{content.PlannedEndAt:yyyy-MM-dd HH:mm}",
            CreatedAt = now,
        });
        instance.PlannedStartAt = content.PlannedStartAt;
        instance.PlannedEndAt = content.PlannedEndAt;
        instance.Status = VisitInstanceStatuses.WaitingRequestApproval;
        instance.DecisionActorRole = null;
        instance.DecisionSource = null;
        instance.DecidedBy = null;
        instance.DecidedAt = null;
        instance.DecisionNote = null;
        instance.CurrentHostUserId = null;
        instance.HostAssignedBy = null;
        instance.HostAssignedAt = null;
        instance.CancelledBy = null;
        instance.CancelledAt = null;
        instance.CancellationActorType = null;
        instance.CancellationSource = null;
        instance.CancellationReason = null;
        instance.CoordinatorUserId = snapshot.ValidStaffLeaderUserId!.Value;
        instance.CoordinatorAssignedBy = actorId;
        instance.CoordinatorAssignedAt = now;
        instance.RowVersion += 1;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;

        // The campus is still being saved (its schedule moved), so FormRevision still advances by one —
        // the same convention every other schedule-only branch in this class follows. Content and
        // members are untouched, so ApplyFormDetail (which would rewrite content fields) is never called.
        detail.FormRevision += 1;
        detail.RowVersion += 1;
        detail.UpdatedAt = now;
        detail.UpdatedBy = actorId;

        // ── Phase 2: recompute the aggregate FROM the campuses, never assumed. With a sibling already
        //    approved this lands on PARTIALLY_APPROVED; with the rest still rejected, PENDING_APPROVAL.
        //    The same answer the AFTER UPDATE trigger computes, so EF's write and the trigger's agree. ──
        _aggregateStatus.Apply(request);

        // Request-level bookkeeping. resubmission_count is display/audit only — nothing validates or
        // limits on it (audited v9 §5) — and the request genuinely was resubmitted, so it counts.
        request.ResubmissionCount += 1;
        request.LastResubmittedAt = now;
        request.LastResubmittedBy = actorId;
        request.RowVersion += 1;
        request.UpdatedAt = now;
        request.UpdatedBy = actorId;

        await _db.SaveChangesAsync(ct);

        // ── Phase 3: a RESUBMIT revision snapshot for THIS instance, built from the CURRENT, still-
        //    linked members — never StageReplaceMembers/LinkMembers/ResolvePartnerLinksAsync. Nothing
        //    about the member list or the partner links moved, so nothing here may write them; the
        //    snapshot must still reflect who is actually linked, never an empty list. ──
        var currentMembers = V2CanonicalRefresh.MembersOf(request, instance);
        _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
        {
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            FormRevision = detail.FormRevision,
            ApprovalRevision = detail.ApprovalRevision,
            SourceType = "RESUBMIT",
            SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, detail, currentMembers),
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = correlationId,
        });

        await _db.SaveChangesAsync(ct);

        return new V2EditResult(request.VisitScope, request.HasMixedCampusDetails, request.RowVersion);
    }

    /// <summary>
    /// Re-runs the partner-link resolver after an edit. An edit REPLACES member rows, so the ids the
    /// old links pointed at are gone; the resolver re-seeds from the new rows and clears the links
    /// that were left pointing at nothing. Idempotent — an already-confirmed relationship survives
    /// untouched (PART-06).
    /// </summary>
    private async Task ResolvePartnerLinksAsync(
        ulong visitRequestId, DateTime now, ulong actorId, CancellationToken ct)
    {
        var changed = await GuestPartnerLinkResolver.ResolveForRequestAsync(
            _db, visitRequestId, now, actorId, ct);
        if (changed > 0) await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The schedule rules for ONE campus: end after start, at least 30 minutes, and — when the schedule
    /// is genuinely being FILED — a start no sooner than <see cref="EditWindowHours"/> from now.
    ///
    /// <para>
    /// <paramref name="enforceLeadTime"/> is false when the caller is not moving the schedule at all. An
    /// edit that leaves the dates exactly as they were is not a new ask, and holding it to the
    /// registration floor would mean a request became uneditable simply by getting closer to its own
    /// date — the guest could no longer fix a name, and the campus would receive the visit with the
    /// mistake still in it.
    /// </para>
    /// <para>
    /// <paramref name="leaderMayOverride"/> is the Staff Leader of THIS campus, and it is resolved by the
    /// handler from the actor's relation — never from the payload. A caller who sets
    /// <paramref name="overrideConfirmed"/> without being that leader is refused exactly like anyone
    /// else, because the flag alone grants nothing.
    /// </para>
    /// </summary>
    /// <summary>
    /// Takes the three schedule primitives rather than a <see cref="CampusVisitEditV2Dto"/> so it can be
    /// shared by every caller that validates a schedule — including
    /// <see cref="InstanceResubmitScheduleDto"/>, which has no content fields to build a whole edit DTO
    /// from.
    /// </summary>
    private static void ValidateSchedule(
        string campusId, DateTime plannedStartAt, DateTime plannedEndAt, DateTime now,
        bool enforceLeadTime = true, bool leaderMayOverride = false, bool overrideConfirmed = false,
        bool allowShortNotice = false)
    {
        var campus = (campusId ?? string.Empty).Trim().ToUpperInvariant();
        if (plannedEndAt <= plannedStartAt)
            throw new BusinessRuleException(
                $"Cơ sở {campus}: thời gian kết thúc phải sau thời gian bắt đầu.",
                VisitRequestErrorCodes.InvalidVisitTime);
        if ((plannedEndAt - plannedStartAt).TotalMinutes < MinDurationMinutes)
            throw new BusinessRuleException(
                $"Cơ sở {campus}: thời lượng tối thiểu là {MinDurationMinutes} phút.",
                VisitRequestErrorCodes.InvalidVisitTime);
        if (!enforceLeadTime)
            return;

        // Absolute invariant, independent of every lead-time exemption below it — the campus leader's
        // confirmed override and the internal-registrant short-notice capability alike. Split out on
        // purpose (mirrors VisitRequestV2CreateService's identical split for Create): exempting a
        // schedule from the 72-hour floor must never, as a side effect, exempt it from "not in the
        // past" too. Strictly greater than now, not "not yet past" — the same boundary Create uses.
        if (plannedStartAt <= now)
            throw new BusinessRuleException(
                $"Cơ sở {campus}: thời gian bắt đầu phải ở trong tương lai.",
                VisitRequestErrorCodes.InvalidVisitTime);

        // Internal Staff/Staff Leader registrant (VisitMutationPolicy.IsShortNoticeEligible) — exempt
        // from the 72-hour floor automatically, with no confirmation dialog: they are the person the
        // floor protects, on their own request, so filing it short is the rule being satisfied.
        if (allowShortNotice)
            return;

        var lead = VisitMutationPolicy.EvaluateScheduleLeadTime(
            plannedStartAt, now, leaderMayOverride, overrideConfirmed);
        if (lead.Allowed)
            return;
        if (lead.ConfirmationRequired)
            throw new ConflictException(
                $"Cơ sở {campus}: {VisitScheduleMessages.LeadTimeOverrideRequired(lead.EarliestAllowedStart)}",
                VisitMutationErrorCodes.LeadTimeOverrideConfirmationRequired);
        throw new BusinessRuleException(
            $"Cơ sở {campus}: {VisitScheduleMessages.LeadTimeNotMet(lead.EarliestAllowedStart)}",
            VisitRequestErrorCodes.InvalidVisitTime);
    }

    /// <summary>The one sentence for "you cannot change which campuses this request is for".</summary>
    private const string CampusSetImmutableMessage =
        "Danh sách cơ sở không thể thay đổi sau khi đơn đã được tạo. " +
        "Vui lòng tạo đơn mới nếu muốn đăng ký thêm cơ sở.";

    /// <summary>
    /// Locks the request row (<c>SELECT … FOR UPDATE</c>) and compares the payload's expected version AND the
    /// tracked entity's loaded version against the CURRENT committed row_version. Concurrent writers serialize
    /// on the lock; the loser wakes up, sees the winner's bump and gets a stable 409 — exactly one winner.
    /// </summary>
    private async Task AssertCurrentRequestVersionAsync(VisitRequest request, int expectedVersion, CancellationToken ct)
    {
        // No LINQ composition on purpose: composing (Select/First) would wrap the SQL in a derived table and
        // MySQL would not lock through it. Uncomposed FromSqlRaw executes the statement verbatim.
        var rows = await _db.VisitRequests
            .FromSqlRaw("SELECT * FROM visit_requests WHERE visit_request_id = {0} FOR UPDATE", request.VisitRequestId)
            .AsNoTracking()
            .ToListAsync(ct);
        var current = rows.Count == 1 ? rows[0].RowVersion : (int?)null;
        if (current is null || expectedVersion != current.Value || request.RowVersion != current.Value)
            throw new ConflictException(
                "Đơn đã được thay đổi bởi một thao tác khác. Vui lòng tải lại và thử lại.",
                VisitRequestErrorCodes.RequestVersionConflict);
    }

    /// <summary>
    /// Locks ONE campus row (<c>SELECT … FOR UPDATE</c>) and compares the payload's expected version AND
    /// the tracked entity's loaded version against the CURRENT committed row_version.
    ///
    /// <para>
    /// The instance rather than the request, on purpose: a sibling campus being approved bumps the
    /// request row, and locking that would make an edit of a campus nobody has touched wait on — and
    /// then lose to — a decision about a different campus.
    /// </para>
    /// </summary>
    private async Task AssertCurrentInstanceVersionAsync(
        VisitRequestCampus instance, int? expectedVersion, CancellationToken ct)
    {
        // Uncomposed FromSqlRaw: composing (Select/First) would wrap the SQL in a derived table and
        // MySQL would not lock through it.
        var rows = await _db.VisitRequestCampuses
            .FromSqlRaw("SELECT * FROM visit_request_campuses WHERE visit_instance_id = {0} FOR UPDATE",
                instance.VisitInstanceId)
            .AsNoTracking()
            .ToListAsync(ct);
        var current = rows.Count == 1 ? rows[0].RowVersion : (int?)null;
        if (current is null || expectedVersion != current.Value || instance.RowVersion != current.Value)
            throw new ConflictException(
                "Lịch thăm tại cơ sở này đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                VisitRequestErrorCodes.InstanceVersionConflict);
    }

    /// <summary>Registrant snapshot, partner and BOTH account-binding emails are immutable in a form edit.
    /// Changing the primary-contact identity is the Phase D identity workflow, never a pending edit.</summary>
    /// <summary>
    /// The schedule rules both write paths share: end after start, at least 30 minutes, and a start no
    /// sooner than <see cref="EditWindowHours"/> from <paramref name="now"/>.
    ///
    /// <para>
    /// Every campus in the payload is checked and the FIRST failure refuses the whole action — an edit
    /// or a resubmit is one atomic ask, and a half-applied one would leave the request describing a
    /// visit nobody agreed to. The message names the campus so a multi-campus payload does not send the
    /// user hunting through the cards for which one is too soon.
    /// </para>
    /// </summary>
    private static void ValidateSchedules(VisitRequestEditV2Dto edit, DateTime now, bool allowShortNotice = false)
    {
        var earliestAllowedStart = now.AddHours(EditWindowHours);
        foreach (var cv in edit.CampusVisits)
        {
            var campus = (cv.CampusId ?? string.Empty).Trim().ToUpperInvariant();
            if (cv.PlannedEndAt <= cv.PlannedStartAt)
                throw new BusinessRuleException(
                    $"Cơ sở {campus}: thời gian kết thúc phải sau thời gian bắt đầu.",
                    VisitRequestErrorCodes.InvalidVisitTime);
            if ((cv.PlannedEndAt - cv.PlannedStartAt).TotalMinutes < MinDurationMinutes)
                throw new BusinessRuleException(
                    $"Cơ sở {campus}: mỗi buổi thăm phải kéo dài tối thiểu {MinDurationMinutes} phút.",
                    VisitRequestErrorCodes.InvalidVisitTime);
            // Absolute invariant for every actor, short-notice or not — see the singular ValidateSchedule
            // above for the full reasoning. A resubmit always proposes a schedule (never echoes an old
            // one silently), so this runs unconditionally, same as the 72h check below it used to.
            if (cv.PlannedStartAt <= now)
                throw new BusinessRuleException(
                    $"Cơ sở {campus}: thời gian bắt đầu phải ở trong tương lai.",
                    VisitRequestErrorCodes.InvalidVisitTime);
            // Internal Staff/Staff Leader registrant — exempt from the 72-hour floor only (plan
            // §PEMS_SHORT_NOTICE_72H_ALL_REGISTRANT_MUTATIONS). Every other actor keeps it exactly as
            // before.
            if (!allowShortNotice && cv.PlannedStartAt < earliestAllowedStart)
                throw new BusinessRuleException(
                    $"Cơ sở {campus}: {VisitScheduleMessages.LeadTimeNotMet(earliestAllowedStart)}",
                    VisitRequestErrorCodes.InvalidVisitTime);
        }
    }

    /// <summary>
    /// What an edit may NOT touch about the registrant: the address, and the partner the request is
    /// filed under.
    ///
    /// <para>
    /// The five descriptive fields beside them — name, organization, job title, nationality, phone —
    /// ARE editable, and used not to be. That refusal was a contract drift, not a rule: the edit form
    /// has always rendered them as inputs, so a registrant correcting a misspelt name or a changed
    /// phone number got "Thông tin người đăng ký không được phép thay đổi" for doing exactly what the
    /// screen invited. They are a SNAPSHOT of who filed this request, stored on the request row; they
    /// are not the account, and <see cref="ApplyCommonFields"/> writes them without touching the
    /// <c>users</c> profile behind <see cref="VisitRequest.RegistrantUserId"/>.
    /// </para>
    /// <para>
    /// The address stays immutable because it is IDENTITY, not description: it is what the account
    /// binding was resolved from, what an OTP was verified against, and what every notification about
    /// this request is addressed to. Changing who the registrant is means a new request. The partner
    /// stays immutable for the same reason — it decides whose organisation the visit is booked under.
    /// </para>
    /// </summary>
    private static void ValidateImmutableFields(VisitRequest request, VisitRequestEditV2Dto edit)
    {
        // Patch 5: compared in the SAME normalized space (trim + lowercase) every other identity/
        // snapshot check in this file already uses (EnsureContactSnapshotUnchanged, ApplyCommonFields'
        // own registrant fields) — a client echoing back exactly what it was served, with only
        // incidental whitespace or casing difference, must never trip this. The bare OrdinalIgnoreCase
        // compare this replaced tolerated case but not whitespace, which could spuriously fail on a
        // registrant email persisted with (or an echo carrying) untrimmed padding.
        var r = edit.Registrant;
        if (VisitRequestFingerprintBuilder.NormalizeEmail(request.RegistrantEmail)
            != VisitRequestFingerprintBuilder.NormalizeEmail(r.Email))
        {
            throw new BusinessRuleException(
                "Không được phép đổi email người đăng ký trong biểu mẫu. " +
                "Email là danh tính đã xác thực của đơn; muốn đổi người đăng ký thì phải tạo đơn mới.",
                "IMMUTABLE_REGISTRANT_EMAIL");
        }
        if (request.PartnerId != edit.PartnerId)
        {
            throw new BusinessRuleException(
                "Không được phép đổi đối tác của đơn đăng ký.", "IMMUTABLE_REGISTRANT_PARTNER");
        }
        // The contact snapshot is checked per campus by EnsureContactSnapshotUnchanged, called from the
        // validation phase of both apply paths — there is no request-level address left to compare here.
    }

    /// <summary>
    /// A campus's WHOLE operational-contact snapshot is immutable in a form edit — the address and the
    /// four details beside it.
    ///
    /// <para>
    /// The address first, and under its own code: it is the only thing this campus's confirmation
    /// invitation is bound to, and the person behind it has either already accepted or is being asked
    /// to. Letting a form edit swap it would hand the campus to a different address with nobody
    /// confirming anything — the exact hole the per-campus confirmation exists to close. Changing it is
    /// a replace (before the decision) or a transfer (after it), both of which re-open the confirmation.
    /// </para>
    /// <para>
    /// Name, organization, job title and phone are refused too, and that is the change. They used to be
    /// editable here on the reasoning that "correcting a typo in a contact's name is not a change of who
    /// runs the campus" — true, but it made the request-edit form a second, silent writer of contact
    /// data. Editing a visit request and managing its operational contact are two workflows now: this
    /// path carries a contact snapshot only so an unchanged payload still round-trips, and anything else
    /// belongs to the contact-management screen, whose metadata path writes exactly these four fields
    /// with its own concurrency check and its own audit entry.
    /// </para>
    /// <para>
    /// Both sides are normalised the way <c>ApplyFormDetail</c> would have STORED them before comparing.
    /// The point is to refuse a real mutation, not to refuse the same phone number written nationally
    /// rather than in E.164, or a value that differs only by whitespace — a client echoing back what it
    /// was served must never trip this.
    /// </para>
    /// </summary>
    /// <summary>
    /// Maps a continuity check's fact to the business error Pending Edit raises for it
    /// (operational-contact consistency fix) — shared by the whole-request and single-instance paths,
    /// which both call <see cref="OperationalContactLink.CheckPreservesExistingMemberRelation"/> against
    /// the same DTOs but cannot share the surrounding loop shape. Relation-STATE violations
    /// (<c>RelationIntroduced</c>, a currently-unlinked campus being handed a relation; and
    /// <c>RelationKeyPointsElsewhere</c>, the disguised-repoint case, including the schedule-only path's
    /// own direct comparison) get Pending Edit's own "you tried to touch the relation here" code rather
    /// than the generic not-found code — those two describe an attempted EDIT of the relation, not a
    /// vanished member.
    /// </summary>
    private static BusinessRuleException PendingEditRelationError(
        OperationalContactLink.ContactMemberContinuityResult result) => result switch
    {
        OperationalContactLink.ContactMemberContinuityResult.MissingIdentityEvidence =>
            new BusinessRuleException(
                "Phiên chỉnh sửa của bạn đã cũ. Vui lòng tải lại trang và thử lại.",
                OperationalContactErrorCodes.StaleSessionRequiresReload),
        OperationalContactLink.ContactMemberContinuityResult.RelationIntroduced or
        OperationalContactLink.ContactMemberContinuityResult.RelationKeyPointsElsewhere =>
            new BusinessRuleException(
                "Liên kết đầu mối không được thay đổi trong Sửa đơn. Hãy dùng Sửa nhanh để cập nhật liên kết hoặc Chuyển đầu mối nếu đổi người phụ trách.",
                OperationalContactErrorCodes.RelationNotEditableInPendingEdit),
        _ => new BusinessRuleException(
            OperationalContactMessages.MemberNotInDelegation,
            OperationalContactErrorCodes.MemberNotFound),
    };

    /// <summary>
    /// Same mapping, for Resubmit and any other LIVE-request caller that has no Pending-Edit-specific
    /// code of its own: relation-state violations fall back to the generic member-not-found code rather
    /// than <see cref="OperationalContactErrorCodes.RelationNotEditableInPendingEdit"/>, which is
    /// Pending-Edit-only vocabulary.
    /// </summary>
    private static BusinessRuleException LiveRequestRelationError(
        OperationalContactLink.ContactMemberContinuityResult result) =>
        result == OperationalContactLink.ContactMemberContinuityResult.MissingIdentityEvidence
            ? new BusinessRuleException(
                "Phiên chỉnh sửa của bạn đã cũ. Vui lòng tải lại trang và thử lại.",
                OperationalContactErrorCodes.StaleSessionRequiresReload)
            : new BusinessRuleException(
                OperationalContactMessages.MemberNotInDelegation,
                OperationalContactErrorCodes.MemberNotFound);

    /// <summary>
    /// After a content-changing save re-links the campus's Operational Contact onto the fresh
    /// copy-on-write member row (<see cref="VisitRequestV2EditOps.LinkMembers"/> already ran), sync the
    /// three shared identity fields from that member onto the contact snapshot and refresh any live
    /// pending invitation so it does not go stale the moment the invited person accepts
    /// (operational-contact consistency fix). A no-op when the campus ends up unlinked.
    /// </summary>
    private async Task SyncLinkedContactAfterRelinkAsync(
        VisitRequest request, VisitRequestCampus instance, AuditLog audit, DateTime now, CancellationToken ct)
    {
        var detail = instance.FormDetail;
        if (detail?.OperationalContactGuestMemberId is not { } linkedId) return;

        var member = V2CanonicalRefresh.MembersOf(request, instance)
            .FirstOrDefault(m => m.GuestMemberId == linkedId);
        if (member is null) return;

        OperationalContactLink.SyncSnapshotFromLinkedMember(audit, detail, member, now);
        if (_invitations is not null)
            await OperationalContactProfileMutation.RefreshPendingInvitationSnapshotAsync(
                _invitations, instance, detail, ct);
    }

    private static void EnsureContactSnapshotUnchanged(
        VisitInstanceFormDetail detail, ContactPointDto incomingContact)
    {
        var current = VisitRequestFingerprintBuilder.NormalizeEmail(detail.OperationalContactEmail);
        var incoming = VisitRequestFingerprintBuilder.NormalizeEmail(incomingContact.Email);
        if (!string.Equals(current, incoming, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "Không được phép thay đổi email đầu mối vận hành của cơ sở. " +
                "Hãy dùng chức năng đổi/chuyển giao đầu mối để người mới xác nhận.",
                "IMMUTABLE_CONTACT_IDENTITY");
        }

        var changed =
            !SameText(detail.OperationalContactFullName, incomingContact.FullName)
            || !SameText(detail.OperationalContactOrganization, incomingContact.Organization)
            || !SameText(detail.OperationalContactJobTitle, incomingContact.JobTitle)
            || !SameText(PhoneNumber.NormalizeOrOriginal(detail.OperationalContactPhone),
                         PhoneNumber.NormalizeOrOriginal(incomingContact.Phone));

        if (changed)
        {
            throw new BusinessRuleException(
                "Không được phép sửa thông tin đầu mối vận hành của cơ sở trong biểu mẫu đăng ký. " +
                "Hãy dùng chức năng chỉnh sửa đầu mối ở màn hình chi tiết đơn.",
                "IMMUTABLE_CONTACT_PROFILE");
        }
    }

    /// <summary>Trimmed comparison in which null, empty and whitespace are all "no value".</summary>
    private static bool SameText(string? stored, string? incoming)
        => string.Equals(
            string.IsNullOrWhiteSpace(stored) ? string.Empty : stored.Trim(),
            string.IsNullOrWhiteSpace(incoming) ? string.Empty : incoming.Trim(),
            StringComparison.Ordinal);

    /// <summary>
    /// Request-level mutable fields: the registrant's DESCRIPTIVE snapshot. Returns true when
    /// something actually moved, which is what makes the caller append a
    /// <c>visit_request_revision_history</c> row — an unchanged payload must not manufacture a
    /// revision, or every save would add a history entry with an empty diff.
    ///
    /// <para>
    /// Each campus's operational contact is NOT here: that snapshot moved onto the campus and is
    /// written by <c>VisitRequestV2EditOps.ApplyFormDetail</c>, so a contact correction shows up as a
    /// change to THAT campus rather than to the request.
    /// </para>
    /// <para>
    /// This writes the request's own columns and nothing else. It never touches the <c>users</c> row
    /// behind <c>RegistrantUserId</c>: the account's profile is the person's, maintained where they
    /// maintain it, while these five columns record who filed THIS request — including the common case
    /// where a Visitor files on behalf of somebody whose details are not their own account's.
    /// </para>
    /// <para>
    /// Comparison is normalised the way the value is STORED (trimmed text; E.164 phone), so a client
    /// echoing back what it was served never registers as a change, and "0912345678" against a stored
    /// "+84912345678" is the same number rather than an edit.
    /// </para>
    /// </summary>
    private static bool ApplyCommonFields(VisitRequest request, VisitRequestEditV2Dto edit, AuditLog audit, DateTime now)
    {
        var r = edit.Registrant;
        var changed = false;

        void Track(string field, string? oldValue, string? newValue)
        {
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = field,
                OldValueText = oldValue,
                NewValueText = newValue,
                CreatedAt = now,
            });
            changed = true;
        }

        // Full name, nationality, organisation and job title are NOT NULL columns and required at
        // create. A blank incoming value is therefore "the client did not send this", never "clear
        // it" — clearing a required column through an optional-looking omission is how a request
        // ends up with no registrant name at all.
        void ApplyRequired(string? incoming, string current, Action<string> assign, string field)
        {
            if (string.IsNullOrWhiteSpace(incoming)) return;
            var value = incoming.Trim();
            if (string.Equals(current, value, StringComparison.Ordinal)) return;
            Track(field, current, value);
            assign(value);
        }

        // Patch 4: resolves the incoming value to a real country's canonical Vietnamese short name
        // before comparing — see the call site below for why raw-string comparison is wrong here.
        // Rejects only a GENUINE change to something that does not resolve; an unresolvable value
        // that is not actually changing (the client's echo of legacy data) passes through untouched.
        void ApplyNationality(string? incoming, string current, Action<string> assign, string field)
        {
            if (string.IsNullOrWhiteSpace(incoming)) return;
            var trimmedIncoming = incoming.Trim();

            var incomingResolved = CountryName.TryResolve(trimmedIncoming, out var incomingCanonical);
            var currentResolved = CountryName.TryResolve(current, out var currentCanonical);
            var effectiveCurrent = currentResolved ? currentCanonical! : current;
            var effectiveIncoming = incomingResolved ? incomingCanonical! : trimmedIncoming;

            if (string.Equals(effectiveCurrent, effectiveIncoming, StringComparison.Ordinal))
                return; // same country (or the same unresolved text) either way — no real change

            if (!incomingResolved)
                throw new BusinessRuleException(
                    $"Quốc tịch người đăng ký không hợp lệ: '{trimmedIncoming}'. {CountryName.FormatHint}",
                    VisitRequestErrorCodes.InvalidNationality);

            Track(field, current, incomingCanonical);
            assign(incomingCanonical!);
        }

        // Nationality (Patch 4): compared in CANONICAL space, not raw text. The client always echoes
        // back whatever the read model returned — including on an edit that never touched this field
        // at all — so comparing raw strings would treat "Hàn Quốc" vs a re-picked "South Korea" (or a
        // legacy row's own casing/spelling drift) as a change on every unrelated save, writing a
        // representation-only entry into history for a field the user never opened. Resolving BOTH
        // sides first means a genuinely unresolvable legacy value that comes back unchanged is left
        // exactly as it was — only an ACTUAL change is required to name a real country.
        ApplyNationality(r.Nationality, request.RegistrantNationality,
            v => request.RegistrantNationality = v, "registrantNationality");

        ApplyRequired(r.FullName, request.RegistrantFullName,
            v => request.RegistrantFullName = v, "registrantFullName");
        ApplyRequired(r.JobTitle, request.RegistrantJobTitle,
            v => request.RegistrantJobTitle = v, "registrantJobTitle");

        // With a partner the organisation is DERIVED from the partner row, so the payload's echo of it
        // is not a value the registrant owns and must not overwrite anything.
        if (!request.PartnerId.HasValue)
            ApplyRequired(r.Organization, request.RegistrantOrganization,
                v => request.RegistrantOrganization = v, "registrantOrganization");

        // Phone IS nullable and optional, so here a blank really does mean "remove it". Stored the way
        // create stores it (E.164) so the two paths cannot disagree about the same number.
        var phone = string.IsNullOrWhiteSpace(r.Phone)
            ? null
            : PhoneNumber.NormalizeOrOriginal(r.Phone.Trim());
        if (!string.Equals(request.RegistrantPhone, phone, StringComparison.Ordinal))
        {
            Track("registrantPhone", request.RegistrantPhone, phone);
            request.RegistrantPhone = phone;
        }

        return changed;
    }

    /// <summary>Rebuilds the CURRENT canonical content of one instance (detail + linked members) for change detection.</summary>
    private static CampusVisitFormDto CurrentContentOf(
        VisitRequest request, VisitRequestCampus instance, VisitInstanceFormDetail d)
    {
        // Called per instance, BEFORE flush #1 — an earlier instance in this same edit may already
        // have staged its copy-on-write replacement members (StageReplaceMembers), which still carry
        // the unassigned GuestMemberId 0 until that flush. Two or more such placeholders collide on
        // ToDictionary. They are harmless to collapse here: no instance's GuestMemberLinks can
        // reference id 0 (a real GuestMemberId is never 0, and LinkMembers only wires up the new rows
        // to their owning instance AFTER flush #1), so this instance's own lookups below only ever
        // resolve real, already-distinct ids regardless of which placeholder wins the collision.
        var membersById = new Dictionary<ulong, VisitGuestMember>();
        foreach (var m in request.GuestMembers) membersById[m.GuestMemberId] = m;
        var linked = instance.GuestMemberLinks
            .OrderBy(l => l.DisplayOrder)
            .Select(l => membersById.TryGetValue(l.GuestMemberId, out var m) ? m : null)
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();
        return new CampusVisitFormDto(
            string.Empty, instance.PlannedStartAt, instance.PlannedEndAt,
            d.DelegationName, d.VisitType, d.VisitTypeOther, d.Purpose, d.WorkingContent,
            linked.Where(m => m.MemberType == "GUEST")
                .Select(m => new VisitorDto(m.FullName, m.Nationality ?? string.Empty, m.JobTitle ?? string.Empty,
                    m.Organization ?? string.Empty, m.OrganizationPartnerId)).ToList(),
            linked.Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .Select(m => new SupportTeamMemberDto(m.FullName, m.JobTitle ?? string.Empty,
                    m.Organization ?? string.Empty, m.Nationality ?? string.Empty, m.OrganizationPartnerId)).ToList(),
            new ContactPointDto(d.OperationalContactFullName, d.OperationalContactOrganization,
                d.OperationalContactJobTitle, d.OperationalContactPhone, d.OperationalContactEmail),
            d.WorkingLanguage, d.TransportationNote, d.MediaConsentStatus, d.Notes,
            HostSelection: null);
    }

    /// <summary>
    /// The two states a campus can be in before any Staff Leader has decided it: still waiting for its own
    /// operational contact to confirm, and waiting for the campus decision once they have.
    ///
    /// <para>
    /// Both are "the registrant may still edit this". The checks here used to name
    /// <c>WAITING_REQUEST_APPROVAL</c> alone, which was complete before the confirmation gate existed and
    /// silently stopped being so afterwards: a request whose contact has not confirmed yet — the ordinary
    /// state for the first 72 hours of most requests — could not be edited or have a campus removed at
    /// all, and the refusal claimed the campus had been "processed (approved/rejected/cancelled)" when in
    /// fact it had not yet reached the point where anyone could process it.
    /// </para>
    /// </summary>
    private static bool IsPreDecision(string status)
        => status is VisitInstanceStatuses.WaitingContactConfirmation
                  or VisitInstanceStatuses.WaitingRequestApproval;

}
