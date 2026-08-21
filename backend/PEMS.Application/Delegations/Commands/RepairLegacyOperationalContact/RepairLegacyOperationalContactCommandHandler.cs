using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.RepairLegacyOperationalContact;

/// <summary>
/// Detects and (only on explicit request) repairs campuses the pre-fix destructive REPLACE bug
/// corrupted. See <see cref="RepairLegacyOperationalContactCommand"/> for the governing principles.
///
/// <para>
/// Recovers ONLY what committed, immutable evidence already proves: the identity-change row that
/// originally confirmed A (its <c>PendingSnapshotJson</c>, never redacted for an Applied row) as the
/// baseline, replayed forward through every legitimate <c>OPERATIONAL_CONTACT_PROFILE_UPDATED</c>
/// correction between then and the corrupting REPLACE, cross-checked against
/// <c>visit_instance_form_revision_history</c> snapshots in the same window. Never reconstructs a field
/// from A's CURRENT user profile — that can have changed for reasons unrelated to this campus. Any step
/// that cannot be resolved to exactly one deterministic answer routes the candidate to MANUAL_REVIEW.
/// </para>
/// </summary>
public sealed class RepairLegacyOperationalContactCommandHandler
    : IRequestHandler<RepairLegacyOperationalContactCommand, RepairLegacyOperationalContactResponse>
{
    /// <summary>audit_logs.action for the repair's OWN write — distinct from every business action, and
    /// deliberately unmapped by GetVisitRequestHistoryQueryHandler, so it can never render as if the
    /// registrant or the contact did something. It is a maintenance correction, not a business event.</summary>
    public const string RepairAction = "LEGACY_CONTACT_REPAIR";

    private const string ClassificationSafe = "SAFE_AUTO_REPAIR";
    private const string ClassificationManualReview = "MANUAL_REVIEW";
    private const string ClassificationNotCorrupted = "NOT_CORRUPTED";
    private const string ClassificationError = "ERROR";

    /// <summary>The exact — and only — literal that authorizes a write. Every other value (including
    /// null, empty, "dryRun", "true", a typo) is a dry run. Never inferred from a bare bool.</summary>
    private const string ApplyMode = "APPLY";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestAggregateStatusService _aggregate;
    private readonly IUserMutationLockService _locks;
    private readonly ILogger<RepairLegacyOperationalContactCommandHandler> _logger;

    public RepairLegacyOperationalContactCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestAggregateStatusService aggregate, IUserMutationLockService locks,
        ILogger<RepairLegacyOperationalContactCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _aggregate = aggregate;
        _locks = locks;
        _logger = logger;
    }

    public async Task<RepairLegacyOperationalContactResponse> Handle(
        RepairLegacyOperationalContactCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        if (!string.Equals(_currentUser.RoleCode, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Chỉ ADMIN mới được chạy khôi phục đầu mối vận hành cũ.");

        var apply = string.Equals(request.Mode, ApplyMode, StringComparison.Ordinal);

        var (scanned, evaluations) = await DetectAsync(ct);

        var safe = evaluations.Where(e => e.Classification == ClassificationSafe).ToList();
        var manual = evaluations.Where(e => e.Classification == ClassificationManualReview).ToList();
        var notCorrupted = evaluations.Count(e => e.Classification == ClassificationNotCorrupted);
        var errors = evaluations.Count(e => e.Classification == ClassificationError);

        var repairedIds = new HashSet<ulong>();
        if (apply)
        {
            var actorId = _currentUser.UserId.Value;
            var now = _clock.VietnamNow;
            foreach (var candidate in safe)
            {
                var ok = await RepairOneAsync(candidate, actorId, now, ct);
                if (ok) repairedIds.Add(candidate.CorruptingAuditLogId);
            }

            _logger.LogInformation(
                "Legacy operational-contact repair APPLY: scanned={Scanned} candidates={Candidates} " +
                "safe={Safe} repaired={Repaired} manualReview={Manual} notCorrupted={NotCorrupted} errors={Errors}",
                scanned, evaluations.Count, safe.Count, repairedIds.Count, manual.Count, notCorrupted, errors);
        }

        return new RepairLegacyOperationalContactResponse
        {
            Applied = apply,
            Scanned = scanned,
            Candidates = evaluations.Count,
            SafeAutoRepair = safe.Count,
            ManualReview = manual.Count,
            NotCorrupted = notCorrupted,
            Errors = errors,
            Repaired = repairedIds.Count,
            SafeAutoRepairCandidates = safe.Select(e => ToDto(e, repairedIds.Contains(e.CorruptingAuditLogId))).ToList(),
            ManualReviewCandidates = manual.Select(e => ToDto(e, Repaired: false)).ToList(),
            Message = !apply
                ? $"Dry-run: {safe.Count} cơ sở đủ điều kiện khôi phục tự động, {manual.Count} cần rà soát " +
                  $"thủ công, {notCorrupted} không (còn) bị lỗi. Chưa ghi gì vào database — gọi lại với " +
                  $"mode=APPLY để áp dụng."
                : $"Đã khôi phục {repairedIds.Count}/{safe.Count} cơ sở đủ điều kiện. {manual.Count} cần rà " +
                  $"soát thủ công.",
        };
    }

    private static LegacyContactRepairCandidateDto ToDto(CandidateEvaluation e, bool Repaired) => new()
    {
        VisitRequestId = e.VisitRequestId,
        VisitInstanceId = e.VisitInstanceId,
        CampusId = e.CampusId,
        CorruptingAuditLogId = e.CorruptingAuditLogId,
        OldContactUserId = e.OldContactUserId,
        Classification = e.Classification,
        Reason = e.Reason,
        Repaired = Repaired,
    };

    // ── Detection (read-only) ───────────────────────────────────────────────────────────────

    private sealed record ReplaceAuditRow(
        ulong AuditLogId, ulong? VisitRequestId, ulong? VisitInstanceId, ulong? EntityId, ulong? CampusId,
        DateTime CreatedAt, string? CorrelationId, string? OldContactUserIdText, string? NewContactUserIdText);

    private async Task<(int Scanned, List<CandidateEvaluation> Evaluations)> DetectAsync(CancellationToken ct)
    {
        var rows = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == OperationalContactHistoryAudit.Replaced)
            .Select(a => new ReplaceAuditRow(
                a.AuditLogId, a.VisitRequestId, a.VisitInstanceId, a.EntityId, a.CampusId, a.CreatedAt,
                a.CorrelationId,
                a.Changes.Where(c => c.FieldName == "operational_contact_user_id")
                    .Select(c => c.OldValueText).FirstOrDefault(),
                a.Changes.Where(c => c.FieldName == "operational_contact_user_id")
                    .Select(c => c.NewValueText).FirstOrDefault()))
            .ToListAsync(ct);

        var scanned = rows.Count;

        // The exact corruption fingerprint: a confirmed holder cleared to null. A no-holder replace
        // (old already null) or a self-match outcome (new = the registrant, non-null) are both ordinary,
        // uncorrupted REPLACE outcomes and are not even candidates.
        var candidates = rows.Where(r =>
            !string.IsNullOrEmpty(r.OldContactUserIdText) && string.IsNullOrEmpty(r.NewContactUserIdText)).ToList();

        var evaluations = new List<CandidateEvaluation>(candidates.Count);
        foreach (var row in candidates)
            evaluations.Add(await EvaluateAsync(row, ct));

        return (scanned, evaluations);
    }

    private async Task<CandidateEvaluation> EvaluateAsync(ReplaceAuditRow row, CancellationToken ct)
    {
        var instanceId = row.VisitInstanceId ?? row.EntityId;
        if (instanceId is null || row.VisitRequestId is null)
            return CandidateEvaluation.Error(row, "Audit thiếu VisitInstanceId/VisitRequestId — không đủ dữ liệu để đánh giá.");
        if (!ulong.TryParse(row.OldContactUserIdText, out var oldContactUserId))
            return CandidateEvaluation.Error(row, "Không đọc được operational_contact_user_id cũ từ audit.");

        var campus = await _db.VisitRequestCampuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VisitInstanceId == instanceId.Value, ct);
        if (campus is null)
            return CandidateEvaluation.Error(row, "Không tìm thấy cơ sở tương ứng với audit này.");

        var campusId = row.CampusId ?? campus.CampusId;

        // ── Disqualifiers: the campus's CURRENT state must still be exactly what the corruption left
        //    behind. Anything else means either it already self-healed (nothing to repair) or its
        //    lifecycle has moved on in a way a blind restore could contradict (rejected, cancelled,
        //    fixed by hand) — never auto-restore over that. ──
        if (campus.OperationalContactUserId is not null)
            return CandidateEvaluation.NotCorrupted(
                row, instanceId.Value, campusId, oldContactUserId,
                "Cơ sở hiện đã có đầu mối vận hành khác — không (còn) cần khôi phục.");
        if (campus.Status != VisitInstanceStatuses.WaitingContactConfirmation)
            return CandidateEvaluation.ManualReview(
                row, instanceId.Value, campusId, oldContactUserId,
                $"Trạng thái hiện tại ('{campus.Status}') không còn đúng như lúc lỗi để lại — cần rà soát thủ công.");

        // ── Step 4: the B invitation THIS specific replace spawned, joined by the shared CorrelationId
        //    the handler writes on both the audit and the invitation-created event. ──
        if (string.IsNullOrEmpty(row.CorrelationId))
            return CandidateEvaluation.ManualReview(
                row, instanceId.Value, campusId, oldContactUserId,
                "Audit không có CorrelationId — không xác định được lời mời B đã phát sinh, cần rà soát thủ công.");

        var spawnedEvent = await _db.VisitRequestIdentityChangeEvents.AsNoTracking()
            .Where(e => e.CorrelationId == row.CorrelationId
                        && e.EventType == "OPERATIONAL_CONTACT_INVITATION_CREATED")
            .FirstOrDefaultAsync(ct);
        if (spawnedEvent is null)
            return CandidateEvaluation.ManualReview(
                row, instanceId.Value, campusId, oldContactUserId,
                "Không tìm thấy lời mời B do lượt REPLACE này tạo ra — cần rà soát thủ công.");

        var bChange = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdentityChangeId == spawnedEvent.IdentityChangeId, ct);
        if (bChange is null)
            return CandidateEvaluation.ManualReview(
                row, instanceId.Value, campusId, oldContactUserId,
                "Không tải được lời mời B — cần rà soát thủ công.");

        var bOutcome = bChange.Status switch
        {
            IdentityChangeStatuses.Applied =>
                "Người B từng chính thức trở thành đầu mối — không tự động khôi phục đè lên một chuyển đổi hợp lệ sau đó.",
            IdentityChangeStatuses.Pending =>
                "Lời mời B vẫn còn hiệu lực — không khôi phục khi còn một lời mời có thể được chấp nhận.",
            IdentityChangeStatuses.Superseded =>
                "Lời mời B đã bị thay thế bởi một thay đổi khác — chuỗi nhiều bước cần rà soát thủ công.",
            IdentityChangeStatuses.Cancelled or IdentityChangeStatuses.Declined or IdentityChangeStatuses.Expired => null,
            _ => $"Trạng thái lời mời B không xác định ('{bChange.Status}') — cần rà soát thủ công.",
        };
        if (bOutcome is not null)
            return CandidateEvaluation.ManualReview(row, instanceId.Value, campusId, oldContactUserId, bOutcome);

        // ── Step 5: A's own confirming identity-change — the ONLY immutable source of A's exact
        //    pre-corruption snapshot. Missing entirely means A was linked by registrant self-match,
        //    which leaves no invitation snapshot to reconstruct from at all. ──
        var confirmingChange = await _db.VisitRequestIdentityChanges.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId.Value && c.NewUserId == oldContactUserId
                        && c.Status == IdentityChangeStatuses.Applied && c.AppliedAt != null
                        && c.AppliedAt <= row.CreatedAt)
            .OrderByDescending(c => c.AppliedAt)
            .FirstOrDefaultAsync(ct);
        if (confirmingChange is null)
            return CandidateEvaluation.ManualReview(
                row, instanceId.Value, campusId, oldContactUserId,
                "Không tìm thấy lời mời xác nhận ban đầu của đầu mối cũ (có thể do tự khớp đăng ký) — " +
                "không có bằng chứng bất biến để khôi phục chính xác, cần rà soát thủ công.");

        var baseline = PendingContactSnapshot.Read(confirmingChange.PendingSnapshotJson);
        if (baseline is null || string.IsNullOrWhiteSpace(baseline.ResolvedFullName)
            || string.IsNullOrWhiteSpace(baseline.ResolvedOrganization)
            || string.IsNullOrWhiteSpace(baseline.ResolvedJobTitle))
            return CandidateEvaluation.ManualReview(
                row, instanceId.Value, campusId, oldContactUserId,
                "Không đọc được đầy đủ thông tin đầu mối gốc từ lời mời xác nhận (đã ẩn danh hoặc thiếu " +
                "dữ liệu) — cần rà soát thủ công.");
        if (string.IsNullOrWhiteSpace(confirmingChange.NewEmailNormalized))
            return CandidateEvaluation.ManualReview(
                row, instanceId.Value, campusId, oldContactUserId,
                "Không đọc được email gốc từ lời mời xác nhận — cần rà soát thủ công.");

        var confirmedAt = confirmingChange.AppliedAt!.Value;

        // ── Step 6: replay every legitimate profile correction between confirmation and corruption. ──
        var corrections = await _db.AuditLogs.AsNoTracking()
            .Where(x => x.Action == OperationalContactHistoryAudit.ProfileUpdated
                        && (x.VisitInstanceId == instanceId.Value || x.EntityId == instanceId.Value)
                        && x.CreatedAt > confirmedAt && x.CreatedAt <= row.CreatedAt)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.CreatedAt,
                Fields = x.Changes.Select(c => new { c.FieldName, c.NewValueText }).ToList(),
            })
            .ToListAsync(ct);

        string ValueAt(string field, string baselineValue, DateTime asOf)
        {
            var value = baselineValue;
            foreach (var correction in corrections)
            {
                if (correction.CreatedAt > asOf) break;
                var hit = correction.Fields.FirstOrDefault(f => f.FieldName == field);
                if (hit is not null) value = hit.NewValueText ?? string.Empty;
            }
            return value;
        }

        var finalFullName = ValueAt("operational_contact_full_name", baseline.ResolvedFullName!, row.CreatedAt);
        var finalOrganization = ValueAt("operational_contact_organization", baseline.ResolvedOrganization!, row.CreatedAt);
        var finalJobTitle = ValueAt("operational_contact_job_title", baseline.ResolvedJobTitle!, row.CreatedAt);
        var finalPhone = ValueAt("operational_contact_phone", baseline.ResolvedPhone ?? string.Empty, row.CreatedAt);
        var finalEmail = confirmingChange.NewEmailNormalized!; // profile correction never touches the address

        // ── Step 6b: cross-check against form-revision snapshots in the same window — an unexplained
        //    mismatch means something touched the contact outside the trail just replayed; never guess
        //    which source is right. ──
        var revisions = await _db.VisitInstanceFormRevisionHistories.AsNoTracking()
            .Where(r => r.VisitInstanceId == instanceId.Value && r.AppliedAt > confirmedAt && r.AppliedAt <= row.CreatedAt)
            .OrderBy(r => r.AppliedAt)
            .ToListAsync(ct);
        foreach (var revision in revisions)
        {
            var revisionSnapshot = PendingContactSnapshot.Read(revision.SnapshotJson);
            if (revisionSnapshot is null) continue; // unreadable — no evidence either way, not a conflict

            var expectedName = ValueAt("operational_contact_full_name", baseline.ResolvedFullName!, revision.AppliedAt);
            var expectedOrg = ValueAt("operational_contact_organization", baseline.ResolvedOrganization!, revision.AppliedAt);

            if (!string.IsNullOrWhiteSpace(revisionSnapshot.ResolvedFullName) && revisionSnapshot.ResolvedFullName != expectedName)
                return CandidateEvaluation.ManualReview(
                    row, instanceId.Value, campusId, oldContactUserId,
                    $"Bản ghi lịch sử nội dung #{revision.RevisionHistoryId} cho thấy tên đầu mối khác với " +
                    "những gì tái tạo được từ audit — có thay đổi chưa giải thích được, cần rà soát thủ công.");
            if (!string.IsNullOrWhiteSpace(revisionSnapshot.ResolvedOrganization) && revisionSnapshot.ResolvedOrganization != expectedOrg)
                return CandidateEvaluation.ManualReview(
                    row, instanceId.Value, campusId, oldContactUserId,
                    $"Bản ghi lịch sử nội dung #{revision.RevisionHistoryId} cho thấy đơn vị công tác khác " +
                    "với những gì tái tạo được từ audit — có thay đổi chưa giải thích được, cần rà soát thủ công.");
        }

        return CandidateEvaluation.Safe(
            row, instanceId.Value, campusId, oldContactUserId,
            finalFullName, finalOrganization, finalJobTitle,
            string.IsNullOrWhiteSpace(finalPhone) ? null : finalPhone, finalEmail,
            bChange.ChangeKind == IdentityChangeKinds.Transfer
                ? OperationalContactSources.Transfer : OperationalContactSources.EmailConfirmation,
            confirmedAt, confirmingChange.IdentityChangeId, bChange.IdentityChangeId);
    }

    // ── Repair (write, one candidate per transaction) ──────────────────────────────────────

    private async Task<bool> RepairOneAsync(
        CandidateEvaluation candidate, ulong actorId, DateTime now, CancellationToken ct)
    {
        await using var tx = await _db.BeginTransactionAsync(ct);

        // Same lock tier CampusApprovalExecutor takes for a campus mutation: VisitRequest then
        // VisitRequestCampus, ascending order, so this can never deadlock against a live business
        // handler racing the same rows.
        await _locks.LockVisitRequestsAsync(new[] { candidate.VisitRequestId }, ct);
        await _locks.LockVisitRequestCampusesAsync(new[] { candidate.VisitInstanceId }, ct);

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances).ThenInclude(c => c.FormDetail)
            .FirstOrDefaultAsync(v => v.VisitRequestId == candidate.VisitRequestId, ct);
        var instance = visit?.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == candidate.VisitInstanceId);

        // Re-check EVERY safe-repair precondition against the row as it is RIGHT NOW, not as it was at
        // scan time. Anything that no longer matches is skipped, never clobbered.
        if (visit is null || instance is null || instance.FormDetail is null
            || instance.OperationalContactUserId is not null
            || instance.Status != VisitInstanceStatuses.WaitingContactConfirmation)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        var detail = instance.FormDetail;
        var correlationId = Guid.NewGuid().ToString("N");

        instance.OperationalContactUserId = candidate.OldContactUserId;
        instance.OperationalContactConfirmedAt = candidate.ConfirmedAt;
        instance.OperationalContactConfirmationSource = candidate.ConfirmationSource;
        instance.Status = VisitInstanceStatuses.WaitingRequestApproval;
        instance.RowVersion += 1;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;

        detail.OperationalContactFullName = candidate.FullName!;
        detail.OperationalContactOrganization = candidate.Organization!;
        detail.OperationalContactJobTitle = candidate.JobTitle!;
        detail.OperationalContactPhone = candidate.Phone;
        detail.OperationalContactEmail = candidate.Email!;
        // GuestMemberId is NOT restored: no immutable evidence source proves what it was before the
        // corruption (the destructive replace cleared it, exactly as a legitimate replace/transfer
        // would). Left null rather than guessed.
        detail.RowVersion += 1;
        detail.UpdatedAt = now;
        detail.UpdatedBy = actorId;

        // Canonical recompute — never hand-set visit_requests.status/ContactGateRevision.
        _aggregate.Apply(visit);
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;

        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = RepairAction,
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = "MAINTENANCE",
            CorrelationId = correlationId,
            Reason = $"confirmingChangeId={candidate.ConfirmingChangeId};" +
                     $"corruptingAuditId={candidate.CorruptingAuditLogId};" +
                     $"bInvitationChangeId={candidate.BInvitationChangeId}",
            CreatedAt = now,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "operational_contact_user_id",
            OldValueText = null,
            NewValueText = candidate.OldContactUserId.ToString(),
            CreatedAt = now,
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "visit_request_campuses.status",
            OldValueText = VisitInstanceStatuses.WaitingContactConfirmation,
            NewValueText = VisitInstanceStatuses.WaitingRequestApproval,
            CreatedAt = now,
        });
        _db.AuditLogs.Add(audit);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    // ── Evaluation result ───────────────────────────────────────────────────────────────────

    private sealed record CandidateEvaluation(
        ulong VisitRequestId, ulong VisitInstanceId, ulong CampusId, ulong CorruptingAuditLogId,
        ulong OldContactUserId, string Classification, string Reason,
        string? FullName, string? Organization, string? JobTitle, string? Phone, string? Email,
        string? ConfirmationSource, DateTime? ConfirmedAt, ulong? ConfirmingChangeId, ulong? BInvitationChangeId)
    {
        public static CandidateEvaluation Error(ReplaceAuditRow row, string reason) => new(
            row.VisitRequestId ?? 0, row.VisitInstanceId ?? row.EntityId ?? 0, row.CampusId ?? 0, row.AuditLogId,
            0, ClassificationError, reason, null, null, null, null, null, null, null, null, null);

        public static CandidateEvaluation NotCorrupted(
            ReplaceAuditRow row, ulong instanceId, ulong campusId, ulong oldContactUserId, string reason) => new(
            row.VisitRequestId!.Value, instanceId, campusId, row.AuditLogId, oldContactUserId,
            ClassificationNotCorrupted, reason, null, null, null, null, null, null, null, null, null);

        public static CandidateEvaluation ManualReview(
            ReplaceAuditRow row, ulong instanceId, ulong campusId, ulong oldContactUserId, string reason) => new(
            row.VisitRequestId!.Value, instanceId, campusId, row.AuditLogId, oldContactUserId,
            ClassificationManualReview, reason, null, null, null, null, null, null, null, null, null);

        public static CandidateEvaluation Safe(
            ReplaceAuditRow row, ulong instanceId, ulong campusId, ulong oldContactUserId,
            string fullName, string organization, string jobTitle, string? phone, string email,
            string confirmationSource, DateTime confirmedAt, ulong confirmingChangeId, ulong bInvitationChangeId) => new(
            row.VisitRequestId!.Value, instanceId, campusId, row.AuditLogId, oldContactUserId,
            ClassificationSafe,
            "Đủ bằng chứng bất biến để khôi phục chính xác đầu mối cũ.",
            fullName, organization, jobTitle, phone, email, confirmationSource, confirmedAt,
            confirmingChangeId, bInvitationChangeId);
    }
}
