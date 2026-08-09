using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
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

        // ── 2. Apply the change rows target-only ──
        List<VisitGuestMember>? stagedMembers = null;
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
                case VisitFieldClassifier.PlannedStartAt: instance.PlannedStartAt = FromJson<DateTime>(change.NewValueJson); break;
                case VisitFieldClassifier.PlannedEndAt: instance.PlannedEndAt = FromJson<DateTime>(change.NewValueJson); break;
                case VisitFieldClassifier.Visitors:
                case VisitFieldClassifier.SupportMembers:
                    // Member replacement is applied ONCE from the pair of member rows (copy-on-write).
                    if (stagedMembers is null)
                    {
                        var visitors = FindMemberProposal<List<VisitorDto>>(amendment, VisitFieldClassifier.Visitors)
                            ?? V2CanonicalRefresh.ToFormDto(request, instance, "X").Visitors.ToList();
                        var support = FindMemberProposal<List<SupportTeamMemberDto>>(amendment, VisitFieldClassifier.SupportMembers)
                            ?? V2CanonicalRefresh.ToFormDto(request, instance, "X").ExternalSupportMembers.ToList();
                        stagedMembers = VisitRequestV2EditOps.StageReplaceMembers(
                            _db, request, instance, visitors, support, now, actorId);
                    }
                    break;
                default:
                    throw new BusinessRuleException(
                        $"Đề xuất chứa trường không được hỗ trợ: {change.FieldPath}.",
                        VisitFormV2ErrorCodes.AmendmentNotEditable);
            }
        }

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
            VisitRequestV2EditOps.LinkMembers(_db, request, instance, stagedMembers, now, actorId);

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
        Add(VisitFieldClassifier.OperationalContactFullName, detail.OperationalContactFullName, Clean(p.OperationalContact?.FullName));
        Add(VisitFieldClassifier.OperationalContactOrganization, detail.OperationalContactOrganization, Clean(p.OperationalContact?.Organization));
        Add(VisitFieldClassifier.OperationalContactJobTitle, detail.OperationalContactJobTitle, Clean(p.OperationalContact?.JobTitle));
        Add(VisitFieldClassifier.OperationalContactPhone,
            PhoneNumber.NormalizeOrOriginal(detail.OperationalContactPhone),
            p.OperationalContact?.Phone is { } ph ? PhoneNumber.NormalizeOrOriginal(ph) : null);
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
        Add(VisitFieldClassifier.Visitors, current.Visitors, p.Visitors);
        Add(VisitFieldClassifier.SupportMembers, current.ExternalSupportMembers, p.ExternalSupportMembers);

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

    private static string? ToJson(object? value)
        => value is null ? null : JsonSerializer.Serialize(value, Json);

    private static T? FromJson<T>(string? json)
        => json is null ? default : JsonSerializer.Deserialize<T>(json, Json);

    private static string? Truncate(string? s) => s is { Length: > 480 } ? s[..480] + "…" : s;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
