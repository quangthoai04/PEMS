using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
using PEMS.Shared;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// See <see cref="IVisitAmendmentService"/> (plan §16.6). Submit stores an IMMUTABLE per-field proposal —
/// the active <c>visit_instance_form_details</c>/members/schedule stay authoritative until the campus
/// Staff Leader approves; approve applies the patch target-only in one transaction (revision snapshot
/// first), bumps form+approval revisions, recomputes the canonical projection and never resets any
/// approval status — sibling campuses are untouched by construction.
/// </summary>
public sealed class VisitAmendmentService : IVisitAmendmentService
{
    private const int MinDurationMinutes = 30;

    /// <summary>
    /// The window in which a proposal may be filed or decided at all, measured against the campus's
    /// CURRENT start — shared with every other self-service mutation (<see cref="VisitMutationPolicy"/>).
    ///
    /// <para>
    /// Not to be confused with the 72-hour registration floor. That floor governs a schedule being put
    /// to a Staff Leader for the FIRST time, so that nobody is asked to approve a visit they have no
    /// time to prepare. After approval the campus already has an owner and an agreed date: the current
    /// schedule stays in force until the Host approves the proposal, so a proposal is a conversation
    /// about a date that already exists rather than a fresh ask, and holding it to 72 hours would make
    /// "can we start an hour later tomorrow" impossible to even suggest.
    /// </para>
    /// </summary>
    private const int SelfServiceCutoffHours = VisitMutationPolicy.MutationCutoffHours;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IApplicationDbContext _db;
    private readonly ILogger<VisitAmendmentService> _logger;

    public VisitAmendmentService(IApplicationDbContext db, ILogger<VisitAmendmentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Submit ────────────────────────────────────────────────────────────────────

    public async Task<VisitInstanceAmendment> SubmitAsync(
        VisitRequest request, VisitRequestCampus instance, VisitAmendmentProposalDto proposal,
        ulong actorId, DateTime now, CancellationToken ct)
    {
        var detail = instance.FormDetail
            ?? throw new ConflictException("Thiếu dữ liệu biểu mẫu theo cơ sở.",
                VisitFormV2ErrorCodes.VisitFormDetailMissing);

        // ── Amendable lifecycle + window, from the shared policy: a DECIDED, not-yet-started instance
        //    at least RequiredLeadHours out. Scoped to THIS campus, so a sibling that is under way
        //    cannot block a proposal for a campus that is still a week away. ──
        VisitMutationGuard.EnsureAllowed(
            VisitMutationAction.SubmitAmendment, request.Status, instance, now,
            VisitViewerRelations.Requester, VisitFormV2ErrorCodes.AmendmentNotEditable);

        // ── Concurrency + base state the requester saw ──
        if (proposal.ExpectedInstanceRowVersion != instance.RowVersion)
            throw new ConflictException(
                "Lịch thăm tại cơ sở này đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                VisitFormV2ErrorCodes.VisitFormConcurrencyConflict);
        if (proposal.BaseFormRevision != detail.FormRevision
            || proposal.BaseApprovalRevision != detail.ApprovalRevision)
            throw new ConflictException(
                "Nội dung đang hiệu lực đã thay đổi so với bản bạn xem. Vui lòng tải lại trước khi đề xuất.",
                VisitFormV2ErrorCodes.AmendmentBaseRevisionConflict);

        // ── One PENDING amendment per instance (pre-check; the DB guard settles a true race below) ──
        var pendingExists = await _db.VisitInstanceAmendments.AsNoTracking()
            .AnyAsync(a => a.VisitInstanceId == instance.VisitInstanceId
                           && a.Status == AmendmentStatuses.PendingApproval, ct);
        if (pendingExists)
            throw new ConflictException(
                "Cơ sở này đang có một đề xuất thay đổi chờ duyệt. Vui lòng chờ quyết định hoặc rút đề xuất cũ.",
                VisitFormV2ErrorCodes.AmendmentAlreadyPending);

        // ── Per-MEMBER organization identity, validated as a SET, once, before the proposal is even
        //    diffed. An amendment re-sends the whole member list like every other v2 write path, so it
        //    can smuggle in a partner id the editor was never offered just as easily as create/edit
        //    could; both of those already run this same check before saving. ──
        await GuestOrganizationPartnerPolicy.EnsureRequestFormSelectableAsync(
            _db,
            proposal.Visitors.Select(v => v.OrganizationPartnerId)
                .Concat(proposal.ExternalSupportMembers.Select(m => m.OrganizationPartnerId))
                .Where(id => id.HasValue).Select(id => id!.Value),
            ct);

        // ── Durable contact-member reference (NP-03) — structural half of the rule only: the payload
        //    must NAME exactly one of its own rows, and no two rows may share a key. Whether that row
        //    may actually hold the role is decided on approve, inside the transaction, by
        //    OperationalContactLink — the ids do not exist yet at this point. ──
        if (!MemberKeysAreDistinct(proposal))
            throw new BusinessRuleException(
                "Danh sách thành viên có định danh trùng nhau. Vui lòng tải lại biểu mẫu.",
                VisitFormV2ErrorCodes.AmendmentNotEditable);
        if (!string.IsNullOrWhiteSpace(proposal.OperationalContactClientMemberKey)
            && !ContactKeyNamesAMember(proposal))
            throw new BusinessRuleException(
                OperationalContactMessages.MemberNotInDelegation,
                VisitFormV2ErrorCodes.AmendmentNotEditable);

        // ── Nationality on every GENUINELY new-or-changed member must resolve to a real country, or
        //    this submission is refused outright (Patch 4 hardening H4-4). Rejecting only at approval
        //    time would let a proposal with an invalid nationality sit PENDING_APPROVAL — the Requester
        //    notified it was filed, the Staff Leader notified there is something to decide — when it
        //    could never actually be approved. A member row whose full content already matches one that
        //    exists on this campus is exempt, same as at approval (StageReplaceMembers): an unrelated
        //    proposal must not be blocked by a legacy nationality nobody touched.
        var activeMembers = MemberContentIndex.Build(V2CanonicalRefresh.MembersOf(request, instance));
        foreach (var v in proposal.Visitors ?? new List<VisitorDto>())
            if (!activeMembers.TryTakeMatch("GUEST", v.FullName, v.Organization, v.JobTitle, v.Nationality, out _))
                NationalityResolution.ResolveOrThrow(v.Nationality, "Quốc tịch khách không hợp lệ:");
        foreach (var m in proposal.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
            if (!activeMembers.TryTakeMatch("EXTERNAL_SUPPORT", m.FullName, m.Organization, m.JobTitle, m.Nationality, out _))
                NationalityResolution.ResolveOrThrow(m.Nationality, "Quốc tịch nhân sự hỗ trợ không hợp lệ:");

        // ── Relation continuity — fail closed BEFORE the proposal is diffed/persisted (operational-
        //    contact consistency fix). An amendment may never change WHO the Operational Contact is or
        //    WHETHER one exists; it may only preserve the SAME persisted member across a member-list
        //    rewrite. Two cases, matching the two continuity mechanisms the DTOs carry — never mixed:
        //    a member-list-changing proposal proves continuity via OperationalContactClientMemberKey +
        //    the incoming rows' own GuestMemberId (copy-on-write mints fresh ids on approve, so only the
        //    ephemeral key can survive that); a member-list-unchanged proposal has every row's
        //    persistent id already real and checkable directly. Uses the SAME fingerprint comparison
        //    BuildChangeRows itself uses below, so the two can never disagree about whether the member
        //    list moved. ──
        var currentForm = V2CanonicalRefresh.ToFormDto(request, instance, "X");
        var memberListChanged =
            VisitorsFingerprint(currentForm.Visitors) != VisitorsFingerprint(proposal.Visitors)
            || SupportFingerprint(currentForm.ExternalSupportMembers) != SupportFingerprint(proposal.ExternalSupportMembers);
        if (memberListChanged)
        {
            var incomingRows = (proposal.Visitors ?? new List<VisitorDto>())
                .Select(v => (v.GuestMemberId, v.ClientMemberKey))
                .Concat((proposal.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
                    .Select(m => (m.GuestMemberId, m.ClientMemberKey)));
            var continuity = OperationalContactLink.CheckPreservesExistingMemberRelation(
                detail.OperationalContactGuestMemberId, incomingRows, proposal.OperationalContactClientMemberKey);
            if (continuity != OperationalContactLink.ContactMemberContinuityResult.Preserved)
                throw continuity == OperationalContactLink.ContactMemberContinuityResult.MissingIdentityEvidence
                    ? new BusinessRuleException(
                        "Phiên chỉnh sửa của bạn đã cũ. Vui lòng tải lại trang và thử lại.",
                        OperationalContactErrorCodes.StaleSessionRequiresReload)
                    : new BusinessRuleException(
                        OperationalContactMessages.MemberNotInDelegation,
                        VisitFormV2ErrorCodes.AmendmentNotEditable);
        }
        else if (proposal.OperationalContactGuestMemberId != detail.OperationalContactGuestMemberId)
        {
            // No COW rewrite is happening, so continuity is simply "did the proposal echo back the
            // active id" — any difference (null↔id, or id↔a-different-id) is an attempted relation
            // change and refuses the whole submission before it ever becomes a change row.
            throw new BusinessRuleException(
                "Đề xuất không được thay đổi liên kết đầu mối. Hãy dùng Sửa nhanh để cập nhật liên kết hoặc Chuyển đầu mối nếu đổi người phụ trách.",
                VisitFormV2ErrorCodes.AmendmentNotEditable);
        }

        // ── Diff the proposal vs the ACTIVE state → immutable change rows (fail closed via classifier) ──
        var changes = BuildChangeRows(request, instance, detail, proposal, now);
        if (changes.Count == 0)
            throw new BusinessRuleException(
                "Đề xuất không có thay đổi nào so với nội dung đang hiệu lực.",
                VisitFormV2ErrorCodes.AmendmentNoChanges);

        // Schedule proposals must be a real, future slot — and nothing more. The 72-hour registration
        // floor does NOT apply here (§39/§40): the campus's current schedule remains the official one
        // until the Host approves, so a proposal is a request to move an agreed date rather than a new
        // date filed for approval. Refusing anything under 72 hours would have made the commonest
        // post-approval conversation — "could we shift it to tomorrow morning" — unsubmittable.
        EnsureProposedSlotValid(proposal.PlannedStartAt, proposal.PlannedEndAt, now);

        var amendmentNo = (await _db.VisitInstanceAmendments
            .Where(a => a.VisitInstanceId == instance.VisitInstanceId)
            .MaxAsync(a => (uint?)a.AmendmentNo, ct) ?? 0) + 1;

        var amendment = new VisitInstanceAmendment
        {
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            AmendmentNo = amendmentNo,
            Status = AmendmentStatuses.PendingApproval,
            BaseFormRevision = detail.FormRevision,
            BaseApprovalRevision = detail.ApprovalRevision,
            RequestedBy = actorId,
            RequestedAt = now,
            Reason = Clean(proposal.Reason),
            // The self-service window closes 24h before the (current) start — the expiry job enforces it.
            ExpiresAt = instance.PlannedStartAt.AddHours(-SelfServiceCutoffHours),
            ExpectedInstanceRowVersion = (uint)instance.RowVersion,
            CreatedAt = now,
        };
        foreach (var c in changes) amendment.Changes.Add(c);
        _db.VisitInstanceAmendments.Add(amendment);

        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "VISIT_AMENDMENT_SUBMITTED",
            EntityType = "VisitInstanceAmendment",
            EntityId = 0, // resolved id noted in Reason below (audit rows precede the flush)
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = "AMENDMENT",
            Reason = $"fields={string.Join(',', changes.Select(c => c.FieldPath))};base={detail.FormRevision}/{detail.ApprovalRevision}",
            CreatedAt = now,
        };
        _db.AuditLogs.Add(audit);

        try
        {
            await _db.SaveChangesAsync(ct); // fires the DB one-pending guard on a true race
        }
        catch (DbUpdateException)
        {
            // Patch 7 (P7.3): this save also writes the AuditLog row and the amendment's own Changes
            // collection, so a blanket "any DbUpdateException here means the one-pending race" would
            // mislabel an unrelated failure (a truncated field, an FK violation, a dropped connection)
            // with the wrong business reason. Re-query the SPECIFIC condition the DB guard exists to
            // catch — the same predicate the soft pre-check above already used — before reporting it;
            // anything else re-throws and reaches the middleware as the real, unclassified 500 it is.
            var reallyPending = await _db.VisitInstanceAmendments.AsNoTracking()
                .AnyAsync(a => a.VisitInstanceId == instance.VisitInstanceId
                               && a.Status == AmendmentStatuses.PendingApproval, ct);
            if (!reallyPending) throw;

            throw new ConflictException(
                "Cơ sở này đang có một đề xuất thay đổi chờ duyệt. Vui lòng chờ quyết định hoặc rút đề xuất cũ.",
                VisitFormV2ErrorCodes.AmendmentAlreadyPending);
        }

        audit.EntityId = amendment.AmendmentId;
        await _db.SaveChangesAsync(ct);
        return amendment;
    }

    // ── Locking ──────────────────────────────────────────────────────────────────

    public async Task<VisitInstanceAmendment?> LockAmendmentAsync(ulong amendmentId, CancellationToken ct)
    {
        var rows = await _db.VisitInstanceAmendments
            .FromSqlRaw("SELECT * FROM visit_instance_amendments WHERE amendment_id = {0} FOR UPDATE", amendmentId)
            .ToListAsync(ct);
        var amendment = rows.SingleOrDefault();
        if (amendment is not null)
            await _db.VisitInstanceAmendmentChanges
                .Where(c => c.AmendmentId == amendmentId)
                .LoadAsync(ct); // populate the tracked collection
        return amendment;
    }

    // ── Approve ──────────────────────────────────────────────────────────────────

    public async Task<VisitAmendmentDecisionResponse> ApproveAsync(
        VisitInstanceAmendment amendment, ulong actorId, string? note, DateTime now, CancellationToken ct,
        bool selfApproval = false)
    {
        EnsurePending(amendment, now);

        var request = await _db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .Include(v => v.CampusInstances).ThenInclude(c => c.GuestMemberLinks)
            .Include(v => v.GuestMembers)
            .FirstAsync(v => v.VisitRequestId == amendment.VisitRequestId, ct);
        var instance = request.CampusInstances.Single(c => c.VisitInstanceId == amendment.VisitInstanceId);
        var detail = instance.FormDetail!;

        // Base state must STILL match — any applied change since submit invalidates the proposal.
        if (amendment.BaseFormRevision != detail.FormRevision
            || amendment.BaseApprovalRevision != detail.ApprovalRevision)
            throw new ConflictException(
                "Nội dung đang hiệu lực đã thay đổi sau khi đề xuất được gửi. Đề xuất cần được gửi lại.",
                VisitFormV2ErrorCodes.AmendmentBaseRevisionConflict);
        // Approving is itself a mutation of a live campus, so it answers to the same window: a proposal
        // that is still pending when the campus is hours from starting must NOT be written in behind
        // everyone's back — by then the campus has printed its list and briefed its Host.
        //
        // The relation asserted here is HOST. The handler has already proved the actor IS the campus's
        // current host (or is the requester side self-approving on a campus they host); passing the
        // relation the policy expects keeps the two halves reading the same rule rather than each
        // carrying its own copy of "who decides".
        VisitMutationGuard.EnsureAllowed(
            VisitMutationAction.ApproveAmendment, request.Status, instance, now,
            VisitViewerRelations.Host, VisitFormV2ErrorCodes.AmendmentNotEditable);

        // A schedule proposal that has aged past its own start time cannot become the campus's schedule
        // — approving it would file a visit in the past. Checked against the PROPOSED values, so a
        // content-only amendment is unaffected.
        var proposedStart = FindProposedDate(amendment, VisitFieldClassifier.PlannedStartAt);
        if (proposedStart is not null)
            EnsureProposedSlotValid(
                proposedStart.Value,
                FindProposedDate(amendment, VisitFieldClassifier.PlannedEndAt) ?? instance.PlannedEndAt,
                now);

        // ── 1. The CURRENT active state is USUALLY already snapshotted: revision history holds exactly
        //       one row per form_revision (unique key) written by whichever edit produced it
        //       (CREATE/SAFE_EDIT/RESUBMIT/AMENDMENT), so approve normally only appends the POST-apply
        //       row for the new revision.
        //
        //       "Usually" is the part that used to be assumed. A campus whose chain never got its first
        //       link — legacy data, canonical seed, a restored database — reaches this line at revision
        //       N with no row for N, and appending N+1 leaves the drawer with nothing to diff against:
        //       the amendment says what it proposed, but the revision entry beside it reports no
        //       recorded changes. Captured here, before the change rows below rewrite the detail, the
        //       schedule and the members. ──
        var membersBefore = V2CanonicalRefresh.MembersOf(request, instance);
        await VisitRevisionBaselineGuard.EnsureInstanceBaselineAsync(
            _db, request, instance, detail, actorId, now, ct);

        // ── 1.5. Relation continuity, re-checked against the CURRENT active relation, inside this
        //         transaction, before anything is staged (operational-contact consistency fix). Submit
        //         already proved this once, but the active relation can move between submit and approve
        //         (a concurrent Safe Edit unlink/relink) — never trust submit-time validation alone. Only
        //         relevant when this proposal actually rewrites the member list; a content-only amendment
        //         never touches the relation and has nothing to re-check. Any non-Preserved result here —
        //         including MissingIdentityEvidence, which is exactly how a proposal STORED BEFORE the
        //         GuestMemberId field existed shows up once deserialized under the new DTO shape — means
        //         this proposal cannot be proven safe under the current rules and must be resubmitted;
        //         there is no live browser session left to ask to reload, so it is never the
        //         stale-session code, always the legacy/resubmit one. ──
        var proposesMemberChange = amendment.Changes.Any(c =>
            c.FieldPath == VisitFieldClassifier.Visitors || c.FieldPath == VisitFieldClassifier.SupportMembers);
        if (proposesMemberChange)
        {
            var proposedVisitorsForCheck = FindMemberProposal<List<VisitorDto>>(amendment, VisitFieldClassifier.Visitors)
                ?? V2CanonicalRefresh.ToFormDto(request, instance, "X").Visitors.ToList();
            var proposedSupportForCheck = FindMemberProposal<List<SupportTeamMemberDto>>(amendment, VisitFieldClassifier.SupportMembers)
                ?? V2CanonicalRefresh.ToFormDto(request, instance, "X").ExternalSupportMembers.ToList();
            var incomingRows = proposedVisitorsForCheck.Select(v => (v.GuestMemberId, v.ClientMemberKey))
                .Concat(proposedSupportForCheck.Select(m => (m.GuestMemberId, m.ClientMemberKey)));
            var continuity = OperationalContactLink.CheckPreservesExistingMemberRelation(
                detail.OperationalContactGuestMemberId, incomingRows, FindProposedContactMemberKey(amendment));
            if (continuity != OperationalContactLink.ContactMemberContinuityResult.Preserved)
                throw new BusinessRuleException(
                    "Đề xuất này được tạo theo quy tắc liên kết đầu mối phiên bản cũ và cần được gửi lại.",
                    VisitFormV2ErrorCodes.AmendmentLegacyContactRelationRequiresResubmission);
        }

        // Constructed here (rather than after the apply block, where this used to live) so the
        // post-relink Operational Contact sync below can append its own change entries into the SAME
        // audit row as the amendment's own field changes — one audit entry per approve action, not two.
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = selfApproval ? "VISIT_AMENDMENT_SELF_APPROVED" : "VISIT_AMENDMENT_APPROVED",
            EntityType = "VisitInstanceAmendment",
            EntityId = amendment.AmendmentId,
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = "AMENDMENT",
            Reason = Clean(note),
            CreatedAt = now,
        };

        // ── 2. Apply the change rows target-only ──
        List<VisitGuestMember>? stagedMembers = null;
        // Hoisted (rather than local to the Visitors/SupportMembers case) so the durable contact-member
        // reference below can zip them against `stagedMembers` in the same order they were staged in.
        List<VisitorDto>? proposedVisitors = null;
        List<SupportTeamMemberDto>? proposedSupport = null;
        // Set when this approval actually moves PlannedStartAt, so any PENDING reminder configured
        // against the OLD start can be recomputed against the new one after the loop (see below).
        var plannedStartChanged = false;
        foreach (var change in amendment.Changes.OrderBy(c => c.DisplayOrder))
        {
            switch (change.FieldPath)
            {
                case VisitFieldClassifier.DelegationName: detail.DelegationName = FromJson<string>(change.NewValueJson)!; break;
                case VisitFieldClassifier.VisitType: detail.VisitType = FromJson<string>(change.NewValueJson); break;
                case VisitFieldClassifier.VisitTypeOther: detail.VisitTypeOther = FromJson<string>(change.NewValueJson); break;
                case VisitFieldClassifier.Purpose: detail.Purpose = FromJson<string>(change.NewValueJson); break;
                case VisitFieldClassifier.WorkingContent: detail.WorkingContent = FromJson<string>(change.NewValueJson); break;
                case VisitFieldClassifier.WorkingLanguage: detail.WorkingLanguage = FromJson<string>(change.NewValueJson); break;
                case VisitFieldClassifier.OperationalContactFullName: detail.OperationalContactFullName = FromJson<string>(change.NewValueJson); break;
                case VisitFieldClassifier.OperationalContactOrganization: detail.OperationalContactOrganization = FromJson<string>(change.NewValueJson); break;
                case VisitFieldClassifier.OperationalContactJobTitle: detail.OperationalContactJobTitle = FromJson<string>(change.NewValueJson)!; break;
                case VisitFieldClassifier.OperationalContactPhone: detail.OperationalContactPhone = FromJson<string>(change.NewValueJson)!; break;
                // Kept for amendments FILED BEFORE the address became identity-managed: a proposal
                // already sitting in PENDING_APPROVAL still has to be applicable or withdrawable.
                // Nothing writes new rows with this path any more (see BuildChangeRows).
                case VisitFieldClassifier.OperationalContactEmail: detail.OperationalContactEmail = FromJson<string>(change.NewValueJson); break;
                case VisitFieldClassifier.PlannedStartAt:
                    instance.PlannedStartAt = FromJson<DateTime>(change.NewValueJson);
                    plannedStartChanged = true;
                    break;
                case VisitFieldClassifier.PlannedEndAt: instance.PlannedEndAt = FromJson<DateTime>(change.NewValueJson); break;
                case VisitFieldClassifier.Visitors:
                case VisitFieldClassifier.SupportMembers:
                    // Member replacement is applied ONCE from the pair of member rows (copy-on-write).
                    if (stagedMembers is null)
                    {
                        proposedVisitors = FindMemberProposal<List<VisitorDto>>(amendment, VisitFieldClassifier.Visitors)
                            ?? V2CanonicalRefresh.ToFormDto(request, instance, "X").Visitors.ToList();
                        proposedSupport = FindMemberProposal<List<SupportTeamMemberDto>>(amendment, VisitFieldClassifier.SupportMembers)
                            ?? V2CanonicalRefresh.ToFormDto(request, instance, "X").ExternalSupportMembers.ToList();
                        stagedMembers = VisitRequestV2EditOps.StageReplaceMembers(
                            _db, request, instance, proposedVisitors, proposedSupport, now, actorId);
                    }
                    break;
                // Metadata only — applied below, alongside `stagedMembers`, not through a direct
                // assignment onto `detail` like the cases above.
                case VisitFieldClassifier.OperationalContactMemberKey:
                    break;
                // LEGACY ONLY (operational-contact consistency fix): BuildChangeRows has not emitted this
                // field path since this fix shipped — a relationship-only relation change is no longer an
                // amendable fact at all, full stop (relation existence/identity moves only through Safe
                // Edit or Replace/Transfer). This case exists solely so a proposal that reached
                // PENDING_APPROVAL BEFORE this fix (when the field was still writable) fails closed with
                // a clear, dedicated explanation instead of silently applying a relation change or falling
                // through to the generic "field not supported" refusal. Never mutates anything.
                case VisitFieldClassifier.OperationalContactGuestMemberId:
                    throw new BusinessRuleException(
                        "Đề xuất này được tạo theo quy tắc liên kết đầu mối phiên bản cũ và cần được gửi lại.",
                        VisitFormV2ErrorCodes.AmendmentLegacyContactRelationRequiresResubmission);
                default:
                    throw new BusinessRuleException(
                        $"Đề xuất chứa trường không được hỗ trợ: {change.FieldPath}.",
                        VisitFormV2ErrorCodes.AmendmentNotEditable);
            }
        }

        // A PENDING reminder was scheduled as "offset before the OLD start"; leaving it there once the
        // approved proposal moves the start would fire it at a moment that no longer means "N before
        // this visit". Safe to call even when the instance is only ASSIGNED (not yet BEFORE_VISIT): no
        // reminder can exist there, so this is a no-op query in that case.
        if (plannedStartChanged)
            await PEMS.Application.Delegations.Reminders.VisitReminderLifecycleSync
                .RescheduleForPlannedStartChangeAsync(_db, instance.VisitInstanceId, instance.PlannedStartAt, now, ct);

        detail.FormRevision += 1;
        detail.ApprovalRevision += 1;
        detail.RowVersion += 1;
        detail.UpdatedAt = now;
        detail.UpdatedBy = actorId;
        instance.RowVersion += 1;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;

        amendment.Status = AmendmentStatuses.Approved;
        amendment.DecidedBy = actorId;
        amendment.DecidedAt = now;
        amendment.DecisionNote = Clean(note);
        amendment.UpdatedAt = now;

        // ── 3. Flush #1 resolves any staged member ids; then link + post-apply revision snapshot ──
        await _db.SaveChangesAsync(ct);
        if (stagedMembers is not null)
        {
            // Durable contact-member reference (NP-03) — the SAME mechanism create/edit use
            // (VisitRequestV2EditOps.LinkMembers + OperationalContactLink), reused here rather than
            // reinvented: the keys are index-aligned with `stagedMembers` in the exact order
            // StageReplaceMembers built it (visitors, then support — see VisitRequestV2EditOps.MemberKeys).
            var clientMemberKeys = (proposedVisitors ?? new List<VisitorDto>()).Select(v => v.ClientMemberKey)
                .Concat((proposedSupport ?? new List<SupportTeamMemberDto>()).Select(m => m.ClientMemberKey))
                .ToList();
            var pickedKey = FindProposedContactMemberKey(amendment);
            VisitRequestV2EditOps.LinkMembers(
                _db, request, instance, stagedMembers, now, actorId, clientMemberKeys, pickedKey);

            // Sync the linked member's shared identity fields onto the contact snapshot — real old→new
            // audit values, appended into THIS approve action's own audit row, never a second FormRevision
            // (operational-contact consistency fix). A no-op when the campus ends up unlinked.
            if (detail.OperationalContactGuestMemberId is { } linkedId)
            {
                var relinkedMember = V2CanonicalRefresh.MembersOf(request, instance)
                    .FirstOrDefault(m => m.GuestMemberId == linkedId);
                if (relinkedMember is not null)
                    OperationalContactLink.SyncSnapshotFromLinkedMember(audit, detail, relinkedMember, now);
            }
        }

        var membersAfter = stagedMembers ?? membersBefore;
        _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
        {
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            FormRevision = detail.FormRevision,
            ApprovalRevision = detail.ApprovalRevision,
            SourceType = FormRevisionSourceTypes.AmendmentApplied,
            SnapshotJson = VisitFormRevisionSnapshotBuilder.Instance(instance, detail, membersAfter),
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = $"amendment #{amendment.AmendmentNo}",
        });

        // ── 4. Canonical recompute + request version bump + audit ──
        await V2CanonicalRefresh.RecomputeAsync(_db, request, ct);
        request.RowVersion += 1;
        request.UpdatedAt = now;
        request.UpdatedBy = actorId;

        // A self-approved amendment is a real amendment with a real decision — it just had no waiting to
        // do, because the person proposing it is the person who decides it. It keeps its own audit
        // action so the timeline can say so plainly instead of showing a proposal that was approved
        // within the same second by its own author and leaving the reader to guess why.
        foreach (var change in amendment.Changes.OrderBy(c => c.DisplayOrder))
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = change.FieldPath,
                OldValueText = Truncate(change.OldValueJson),
                NewValueText = Truncate(change.NewValueJson),
                CreatedAt = now,
            });
        _db.AuditLogs.Add(audit);
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "VISIT_INSTANCE_FORM_REVISION_APPLIED",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = "AMENDMENT",
            Reason = $"form_revision={detail.FormRevision};approval_revision={detail.ApprovalRevision};amendment={amendment.AmendmentId}",
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(ct);
        return new VisitAmendmentDecisionResponse(
            amendment.AmendmentId, instance.VisitInstanceId, amendment.Status,
            detail.FormRevision, detail.ApprovalRevision,
            selfApproval
                ? "Đã cập nhật thông tin cho cơ sở này."
                : "Đề xuất thay đổi đã được duyệt và áp dụng cho cơ sở này.");
    }

    // ── Reject / withdraw / expire ───────────────────────────────────────────────

    public async Task<VisitAmendmentDecisionResponse> RejectAsync(
        VisitInstanceAmendment amendment, ulong actorId, string note, DateTime now, CancellationToken ct)
    {
        EnsurePending(amendment, now);
        if (string.IsNullOrWhiteSpace(note))
            throw new BusinessRuleException("Từ chối đề xuất phải kèm lý do.",
                VisitFormV2ErrorCodes.AmendmentNotEditable);

        amendment.Status = AmendmentStatuses.Rejected;
        amendment.DecidedBy = actorId;
        amendment.DecidedAt = now;
        amendment.DecisionNote = note.Trim();
        amendment.UpdatedAt = now;
        _db.AuditLogs.Add(DecisionAudit(amendment, actorId, "VISIT_AMENDMENT_REJECTED", note.Trim(), now));

        await _db.SaveChangesAsync(ct);
        return new VisitAmendmentDecisionResponse(
            amendment.AmendmentId, amendment.VisitInstanceId, amendment.Status, null, null,
            "Đề xuất đã bị từ chối; nội dung đang hiệu lực giữ nguyên.");
    }

    public async Task<VisitAmendmentDecisionResponse> WithdrawAsync(
        VisitInstanceAmendment amendment, ulong actorId, DateTime now, CancellationToken ct)
    {
        EnsurePending(amendment, now);

        amendment.Status = AmendmentStatuses.Withdrawn;
        amendment.WithdrawnAt = now;
        amendment.UpdatedAt = now;
        _db.AuditLogs.Add(DecisionAudit(amendment, actorId, "VISIT_AMENDMENT_WITHDRAWN", null, now));

        await _db.SaveChangesAsync(ct);
        return new VisitAmendmentDecisionResponse(
            amendment.AmendmentId, amendment.VisitInstanceId, amendment.Status, null, null,
            "Đề xuất đã được rút; nội dung đang hiệu lực giữ nguyên.");
    }

    public async Task<int> ExpireDueAsync(DateTime now, int batchSize, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(ct);
        var due = await _db.VisitInstanceAmendments
            .FromSqlRaw(
                "SELECT a.* FROM visit_instance_amendments a " +
                "JOIN visit_request_campuses c ON c.visit_instance_id = a.visit_instance_id " +
                "WHERE a.status = 'PENDING_APPROVAL' " +
                "  AND (a.expires_at <= {0} OR c.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED','CANCELLED')) " +
                "ORDER BY a.amendment_id LIMIT {1} FOR UPDATE", now, batchSize)
            .ToListAsync(ct);
        if (due.Count == 0)
        {
            await tx.CommitAsync(ct);
            return 0;
        }

        foreach (var amendment in due)
        {
            amendment.Status = AmendmentStatuses.Expired;
            amendment.UpdatedAt = now;
            _db.AuditLogs.Add(DecisionAudit(amendment, null, "VISIT_AMENDMENT_EXPIRED", "EXPIRY_JOB", now));
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _logger.LogInformation("visit-amendment maintenance: {Expired} expired", due.Count);
        return due.Count;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A proposed slot must be usable: end after start, at least half an hour, and still ahead of us.
    /// Checked both when the proposal is filed and again when it is approved — a proposal that sat in
    /// the queue until its own start time had passed must not be written in as the campus's schedule.
    /// </summary>
    private static void EnsureProposedSlotValid(DateTime start, DateTime end, DateTime now)
    {
        if (end <= start || (end - start).TotalMinutes < MinDurationMinutes)
            throw new BusinessRuleException(
                $"Lịch đề xuất không hợp lệ (thời gian kết thúc phải sau thời gian bắt đầu, tối thiểu {MinDurationMinutes} phút).",
                VisitRequestErrorCodes.InvalidVisitTime);
        if (start <= now)
            throw new BusinessRuleException(
                "Lịch đề xuất phải ở thời điểm trong tương lai.",
                VisitRequestErrorCodes.InvalidVisitTime);
    }

    private static void EnsurePending(VisitInstanceAmendment amendment, DateTime now)
    {
        if (amendment.Status != AmendmentStatuses.PendingApproval)
            throw new ConflictException(
                "Đề xuất này đã được xử lý (duyệt/từ chối/rút/hết hạn).",
                VisitFormV2ErrorCodes.AmendmentNotEditable);
        if (amendment.ExpiresAt is not null && amendment.ExpiresAt <= now)
            throw new ConflictException(
                "Đề xuất đã quá thời hạn xử lý.", VisitFormV2ErrorCodes.AmendmentWindowExpired);
    }

    private static AuditLog DecisionAudit(
        VisitInstanceAmendment amendment, ulong? actorId, string action, string? reason, DateTime now)
        => new()
        {
            ActorUserId = actorId,
            Action = action,
            EntityType = "VisitInstanceAmendment",
            EntityId = amendment.AmendmentId,
            VisitRequestId = amendment.VisitRequestId,
            VisitInstanceId = amendment.VisitInstanceId,
            SourceType = "AMENDMENT",
            Reason = reason,
            CreatedAt = now,
        };

    /// <summary>Diffs the FULL proposal snapshot against the active state → ordered immutable change rows.</summary>
    private List<VisitInstanceAmendmentChange> BuildChangeRows(
        VisitRequest request, VisitRequestCampus instance, VisitInstanceFormDetail detail,
        VisitAmendmentProposalDto p, DateTime now)
    {
        var rows = new List<VisitInstanceAmendmentChange>();
        uint order = 0;

        void Add(string path, object? oldValue, object? newValue)
        {
            var oldJson = ToJson(oldValue);
            var newJson = ToJson(newValue);
            if (string.Equals(oldJson, newJson, StringComparison.Ordinal)) return;
            if (!VisitFieldClassifier.IsAmendable(path))
                throw new BusinessRuleException(
                    $"Trường '{path}' không thể thay đổi qua đề xuất. Hãy dùng sửa nhanh hoặc quy trình phù hợp.",
                    VisitFormV2ErrorCodes.AmendmentNotEditable);
            rows.Add(new VisitInstanceAmendmentChange
            {
                FieldPath = path,
                ChangeClass = VisitFieldClassifier.ClassifyChange(path, oldJson, newJson)!,
                OldValueJson = oldJson,
                NewValueJson = newJson,
                IsSensitive = true,
                DisplayOrder = order++,
                CreatedAt = now,
            });
        }

        Add(VisitFieldClassifier.DelegationName, detail.DelegationName, p.DelegationName?.Trim());
        Add(VisitFieldClassifier.VisitType, detail.VisitType, p.VisitType?.Trim());
        Add(VisitFieldClassifier.VisitTypeOther, detail.VisitTypeOther,
            p.VisitType == "OTHER" ? Clean(p.VisitTypeOther) : null);
        Add(VisitFieldClassifier.Purpose, detail.Purpose, p.Purpose?.Trim());
        Add(VisitFieldClassifier.WorkingContent, detail.WorkingContent, Clean(p.WorkingContent));
        Add(VisitFieldClassifier.WorkingLanguage, detail.WorkingLanguage, p.WorkingLanguage?.Trim());
        // The contact PROFILE (name/organization/job title/phone/email) is not amendable at all any
        // more (plan PEMS_CONTACT_ONE_DOOR) — it has exactly one door, "Manage the contact role". The
        // modal sends the whole contact block back UNCHANGED (it is read-only there now), so the normal
        // case never trips this; a handcrafted request that tries to redescribe the contact is refused
        // here rather than silently applied on approval or silently dropped.
        //
        // WHO the contact IS remains amendable — see OperationalContactClientMemberKey below — because
        // naming a different existing delegation member is not the same act as redescribing this one.
        if (p.OperationalContact is { } proposedContact)
        {
            // Phone is compared with the SAME normalization on both sides (PhoneNumber.NormalizeOrNull,
            // which maps blank/null to null on either end symmetrically) — the two sides used to run
            // through different rules (NormalizeOrOriginal, which turns a null DB value into "", against
            // a null-preserving ternary on the proposal), so a campus with no phone on file always read
            // "" != null and threw ContactProfileNotAmendable on ANY amendment, even one that never
            // touched the contact at all (the modal always sends the profile back read-only/unchanged).
            var changed =
                !string.Equals(Clean(detail.OperationalContactFullName), Clean(proposedContact.FullName), StringComparison.Ordinal)
                || !string.Equals(Clean(detail.OperationalContactOrganization), Clean(proposedContact.Organization), StringComparison.Ordinal)
                || !string.Equals(Clean(detail.OperationalContactJobTitle), Clean(proposedContact.JobTitle), StringComparison.Ordinal)
                || !string.Equals(PhoneNumber.NormalizeOrNull(detail.OperationalContactPhone),
                    PhoneNumber.NormalizeOrNull(proposedContact.Phone), StringComparison.Ordinal);
            if (changed)
                throw new BusinessRuleException(
                    "Không thể sửa thông tin đầu mối (họ tên/tổ chức/chức danh/điện thoại) qua đề xuất thay đổi. " +
                    "Hãy dùng chức năng \"Chỉnh sửa đầu mối\" của cơ sở.",
                    VisitFormV2ErrorCodes.ContactProfileNotAmendable);
        }
        // The ADDRESS is not amendable — it decides WHO holds the campus, and that only moves through
        // the contact workflow, where the new person has to accept and the old one keeps their rights
        // until they do. A proposal carrying the unchanged address is fine and common (the modal sends
        // the whole contact back); one carrying a different address is refused here rather than
        // silently applied on approval.
        var proposedEmail = Clean(p.OperationalContact?.Email);
        if (proposedEmail is not null
            && !string.Equals(proposedEmail, Clean(detail.OperationalContactEmail), StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException(
                "Không thể đổi email đầu mối qua đề xuất thay đổi. Hãy dùng chức năng \"Chỉnh sửa đầu mối\" của cơ sở.",
                VisitFormV2ErrorCodes.ContactEmailNotAmendable);

        Add(VisitFieldClassifier.PlannedStartAt, instance.PlannedStartAt, p.PlannedStartAt);
        Add(VisitFieldClassifier.PlannedEndAt, instance.PlannedEndAt, p.PlannedEndAt);

        var current = V2CanonicalRefresh.ToFormDto(request, instance, "X");

        // Member lists compare on BUSINESS content only — ClientMemberKey is a per-submission session
        // tag (NP-03), freshly minted every time the modal opens, never persisted. Comparing it like an
        // ordinary field would make every amendment "change" the member lists even when nothing the
        // user can see was touched, since the active snapshot never carries a key to match against.
        //
        // Nationality specifically compares on COUNTRY, not spelling (Patch 4 hardening H4-1): two
        // strings that resolve to the same country are the same business value, so reopening the modal
        // and resubmitting an alias-only respelling of an unchanged member's nationality — or of a
        // legacy value that does not resolve at all — must not manufacture a "Visitors"/"SupportMembers"
        // change (a full member-list replace, a FormRevision bump, a revision-history row) out of zero
        // real difference. A value that does not resolve to any real country falls back to its own text,
        // so it still compares stable against itself and only itself.
        void AddMembers(string path, object? oldValue, object? newValue, bool changed)
        {
            if (!changed) return;
            if (!VisitFieldClassifier.IsAmendable(path))
                throw new BusinessRuleException(
                    $"Trường '{path}' không thể thay đổi qua đề xuất. Hãy dùng sửa nhanh hoặc quy trình phù hợp.",
                    VisitFormV2ErrorCodes.AmendmentNotEditable);
            var oldJson = ToJson(oldValue);
            var newJson = ToJson(newValue);
            rows.Add(new VisitInstanceAmendmentChange
            {
                FieldPath = path,
                ChangeClass = VisitFieldClassifier.ClassifyChange(path, oldJson, newJson)!,
                OldValueJson = oldJson,
                NewValueJson = newJson,
                IsSensitive = true,
                DisplayOrder = order++,
                CreatedAt = now,
            });
        }

        var visitorsChanged = VisitorsFingerprint(current.Visitors) != VisitorsFingerprint(p.Visitors);
        var supportChanged = SupportFingerprint(current.ExternalSupportMembers) != SupportFingerprint(p.ExternalSupportMembers);
        AddMembers(VisitFieldClassifier.Visitors, current.Visitors, p.Visitors, visitorsChanged);
        AddMembers(VisitFieldClassifier.SupportMembers, current.ExternalSupportMembers, p.ExternalSupportMembers, supportChanged);

        // MEMBER-LIST REPLACEMENT ONLY (operational-contact consistency fix — `OperationalContactGuestMemberId`
        // is no longer a proposable amendable field at all; `SubmitAsync` already refused this proposal
        // above if it tried to change relation existence/identity, so by the time control reaches here
        // the relation-unchanged case has nothing left to record). Rows are copy-on-write on approve, so
        // every row gets a brand new GuestMemberId — the only thing that can name the new contact row is
        // the EPHEMERAL key the proposal minted for it (NP-03). Unconditional per the existing
        // convention: even an unchanged pick is recorded here, because "the contact" is about to be
        // re-resolved onto a fresh row regardless.
        if (visitorsChanged || supportChanged)
        {
            AddMembers(
                VisitFieldClassifier.OperationalContactMemberKey, null,
                string.IsNullOrWhiteSpace(p.OperationalContactClientMemberKey) ? null : p.OperationalContactClientMemberKey,
                true);
        }

        return rows;
    }

    /// <summary>The proposed value of a date field, or null when this amendment does not touch it.</summary>
    private static DateTime? FindProposedDate(VisitInstanceAmendment amendment, string path)
    {
        var row = amendment.Changes.FirstOrDefault(c => c.FieldPath == path);
        return row?.NewValueJson is null ? null : JsonSerializer.Deserialize<DateTime>(row.NewValueJson, Json);
    }

    private static T? FindMemberProposal<T>(VisitInstanceAmendment amendment, string path) where T : class
    {
        var row = amendment.Changes.FirstOrDefault(c => c.FieldPath == path);
        return row?.NewValueJson is null ? null : JsonSerializer.Deserialize<T>(row.NewValueJson, Json);
    }

    /// <summary>The proposed contact-member key, or null when this amendment does not touch it.</summary>
    private static string? FindProposedContactMemberKey(VisitInstanceAmendment amendment)
    {
        var row = amendment.Changes.FirstOrDefault(c => c.FieldPath == VisitFieldClassifier.OperationalContactMemberKey);
        return row?.NewValueJson is null ? null : JsonSerializer.Deserialize<string>(row.NewValueJson, Json);
    }

    /// <summary>Every non-empty member key in ONE proposal, visitors and support together.</summary>
    private static IEnumerable<string> MemberKeysOf(VisitAmendmentProposalDto p) =>
        p.Visitors.Select(v => v.ClientMemberKey)
            .Concat(p.ExternalSupportMembers.Select(m => m.ClientMemberKey))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!);

    /// <summary>Mirrors CreateVisitRequestV2CommandValidator.MemberKeysAreDistinct — a key appearing
    /// twice is not an identity, and the contact would resolve to whichever row was enumerated first.</summary>
    private static bool MemberKeysAreDistinct(VisitAmendmentProposalDto p)
    {
        var keys = MemberKeysOf(p).ToList();
        return keys.Count == keys.Distinct(StringComparer.Ordinal).Count();
    }

    /// <summary>Mirrors CreateVisitRequestV2CommandValidator.ContactKeyNamesAMember.</summary>
    private static bool ContactKeyNamesAMember(VisitAmendmentProposalDto p) =>
        MemberKeysOf(p).Count(k => string.Equals(k, p.OperationalContactClientMemberKey, StringComparison.Ordinal)) == 1;

    /// <summary>
    /// Same country-normalized comparison <see cref="BuildChangeRows"/> uses to decide whether the
    /// member lists moved — promoted to class level (operational-contact consistency fix) so
    /// <see cref="SubmitAsync"/> can ask the SAME question before deciding which relation-continuity
    /// branch applies, and the two can never disagree about whether the member list changed.
    /// </summary>
    private static string EffectiveNationality(string? s) =>
        CountryName.TryResolve(s, out var canonical) ? canonical! : (s ?? string.Empty);

    private static string VisitorsFingerprint(IEnumerable<VisitorDto> vs) => JsonSerializer.Serialize(
        vs.Select(v => new { v.FullName, Nationality = EffectiveNationality(v.Nationality), v.JobTitle, v.Organization, v.OrganizationPartnerId }), Json);

    private static string SupportFingerprint(IEnumerable<SupportTeamMemberDto> ms) => JsonSerializer.Serialize(
        ms.Select(m => new { m.FullName, m.JobTitle, m.Organization, Nationality = EffectiveNationality(m.Nationality), m.OrganizationPartnerId }), Json);

    private static string? ToJson(object? value)
        => value is null ? null : JsonSerializer.Serialize(value, Json);

    private static T? FromJson<T>(string? json)
        => json is null ? default : JsonSerializer.Deserialize<T>(json, Json);

    private static string? Truncate(string? s) => s is { Length: > 480 } ? s[..480] + "…" : s;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
