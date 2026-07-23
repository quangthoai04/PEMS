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
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Per-campus form v2 pending-edit aggregate service (plan §6.4). Runs inside the caller's open transaction on
/// a TRACKED <see cref="VisitRequest"/> loaded with CampusInstances(+FormDetail,+GuestMemberLinks) and
/// GuestMembers; the caller re-checked authorization and lifecycle before calling and owns the commit.
/// Everything here re-validates the data-facing rules in-transaction: explicit optimistic row-version checks
/// (plain int columns — EF has no concurrency token on them), campus resolution/availability, schedule,
/// immutable account-binding fields, per-instance change detection, copy-on-write member replacement, campus
/// add/remove, canonical recompute (scope/mixed/fingerprint/projection) and immutable revision history + audit.
/// </summary>
public sealed class VisitRequestV2EditService : IVisitRequestV2EditService
{
    private const int MinDurationMinutes = 30;
    private const int EditWindowHours = 24; // Visitor edit window (v1 parity: sửa/hủy trước 24h)

    private readonly IApplicationDbContext _db;

    public VisitRequestV2EditService(IApplicationDbContext db) => _db = db;

    public async Task<V2EditResult> ApplyPendingEditAsync(
        VisitRequest request, VisitRequestEditV2Dto edit, ulong actorId, DateTime now, CancellationToken ct)
    {
        // ── 0. Optimistic concurrency — request level (stable 409, never last-write-wins).
        //       row_version is a plain int (no EF concurrency token), so the guard is an explicit
        //       SELECT … FOR UPDATE against the CURRENT committed row: concurrent editors serialize on the
        //       lock and the loser sees the winner's bumped version → 409. ──
        await AssertCurrentRequestVersionAsync(request, edit.ExpectedRequestRowVersion, ct);

        if (edit.CampusVisits is null || edit.CampusVisits.Count == 0)
            throw new BusinessRuleException("Phải chọn ít nhất 1 cơ sở.", VisitRequestErrorCodes.InvalidVisitScope);

        // ── 1. Immutable account-binding + registrant snapshot (v1 parity, plan §5.1/§16) ──
        ValidateImmutableFields(request, edit);

        // ── 2. Classify payload campuses: kept (stable visitInstanceId) vs added (null id) ──
        var instancesById = request.CampusInstances.ToDictionary(c => c.VisitInstanceId);
        var kept = new List<(CampusVisitEditV2Dto Content, VisitRequestCampus Instance)>();
        var added = new List<CampusVisitEditV2Dto>();
        foreach (var cv in edit.CampusVisits)
        {
            if (cv.VisitInstanceId is { } id)
            {
                if (!instancesById.TryGetValue(id, out var instance))
                    throw new BusinessRuleException(
                        "Cơ sở được sửa không thuộc đơn này.", VisitRequestErrorCodes.InstanceEditInvalid);
                kept.Add((cv, instance));
            }
            else
            {
                added.Add(cv);
            }
        }

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
            if (instance.Status != VisitInstanceStatuses.WaitingRequestApproval)
                throw new BusinessRuleException(
                    "Đơn đã có cơ sở được xử lý (duyệt/từ chối/hủy) nên không thể sửa.",
                    VisitRequestErrorCodes.VisitRequestNotEditable);
        }

        // ── 5. Removed instances = present on the request but absent from the payload ──
        var keptIds = kept.Select(k => k.Instance.VisitInstanceId).ToHashSet();
        var removed = request.CampusInstances.Where(c => !keptIds.Contains(c.VisitInstanceId)).ToList();
        foreach (var gone in removed)
            await EnsureInstanceRemovableAsync(gone, ct);

        // ── 6. Schedule validation (pending-edit window: every start ≥ now + 24h) ──
        foreach (var cv in edit.CampusVisits)
        {
            if (cv.PlannedEndAt <= cv.PlannedStartAt)
                throw new BusinessRuleException("Thời gian kết thúc phải sau thời gian bắt đầu.", VisitRequestErrorCodes.InvalidVisitTime);
            if ((cv.PlannedEndAt - cv.PlannedStartAt).TotalMinutes < MinDurationMinutes)
                throw new BusinessRuleException("Mỗi buổi thăm phải kéo dài tối thiểu 30 phút.", VisitRequestErrorCodes.InvalidVisitTime);
            if (cv.PlannedStartAt < now.AddHours(EditWindowHours))
                throw new BusinessRuleException(
                    "Thời gian bắt đầu mới phải cách hiện tại ít nhất 24 giờ.",
                    VisitRequestErrorCodes.InvalidVisitTime);
        }

        // ── 7. Added campuses: full operational-availability recheck (ACTIVE + IC dept + one Staff Leader) ──
        var addedSnapshots = new Dictionary<string, CampusAvailabilitySnapshot>(StringComparer.OrdinalIgnoreCase);
        if (added.Count > 0)
        {
            var addedIds = added.Select(a => campusIdsByCode[a.CampusId.Trim().ToUpperInvariant()]).ToList();
            var snapshots = await CampusAvailabilityEvaluator.EvaluateAsync(_db, addedIds, ct);
            foreach (var a in added)
            {
                var code = a.CampusId.Trim().ToUpperInvariant();
                var s = snapshots.TryGetValue(campusIdsByCode[code], out var snap)
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
                addedSnapshots[code] = s;
            }
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
            SourceType = "SAFE_EDIT",
            CreatedAt = now,
        };
        _db.AuditLogs.Add(audit);

        // ── 8. Kept instances: change detection → apply only what changed ──
        var changedInstances = new List<(VisitRequestCampus Instance, List<VisitGuestMember> NewMembers)>();
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
            if (!contentChanged && !scheduleChanged)
                continue; // untouched sibling: no member churn, no revision bump, no row-version bump

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

            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            if (contentChanged)
                changedInstances.Add((instance, newMembers));
        }

        // ── 9. Removed campuses: delete the instance (details/links/revisions cascade at the DB);
        //       clean up member rows that no surviving instance links; audit the removal. ──
        foreach (var gone in removed)
        {
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = $"instance[{gone.VisitInstanceId}].removed",
                OldValueText = $"campus_id={gone.CampusId};status={gone.Status}",
                NewValueText = "removed_by_pending_edit",
                CreatedAt = now,
            });

            var goneMemberIds = gone.GuestMemberLinks.Select(l => l.GuestMemberId).ToHashSet();
            request.CampusInstances.Remove(gone);
            _db.VisitRequestCampuses.Remove(gone);

            foreach (var memberId in goneMemberIds)
            {
                var linkedElsewhere = request.CampusInstances
                    .SelectMany(ci => ci.GuestMemberLinks)
                    .Any(l => l.GuestMemberId == memberId);
                if (!linkedElsewhere)
                {
                    var member = request.GuestMembers.FirstOrDefault(m => m.GuestMemberId == memberId);
                    if (member is not null)
                    {
                        request.GuestMembers.Remove(member);
                        _db.VisitGuestMembers.Remove(member);
                    }
                }
            }
        }

        // ── 10. Added campuses: new instance + form detail + independent members, routed to the
        //        campus Staff Leader, WAITING_REQUEST_APPROVAL. ──
        var addedStaging = new List<(VisitRequestCampus Instance, CampusVisitEditV2Dto Content, List<VisitGuestMember> Members)>();
        foreach (var a in added)
        {
            var snapshot = addedSnapshots[a.CampusId.Trim().ToUpperInvariant()];
            var instance = new VisitRequestCampus
            {
                CampusId = snapshot.CampusId,
                PlannedStartAt = a.PlannedStartAt,
                PlannedEndAt = a.PlannedEndAt,
                Status = VisitInstanceStatuses.WaitingRequestApproval,
                CoordinatorUserId = snapshot.ValidStaffLeaderUserId,
                CoordinatorAssignedBy = actorId,
                CoordinatorAssignedAt = now,
                RowVersion = 0,
                CreatedAt = now,
                CreatedBy = actorId,
                FormDetail = VisitRequestV2EditOps.BuildFormDetail(a, now, actorId),
            };
            request.CampusInstances.Add(instance);

            var rows = new List<VisitGuestMember>();
            uint order = 1;
            foreach (var v in a.Visitors ?? new List<VisitorDto>())
                rows.Add(new VisitGuestMember
                {
                    FullName = v.FullName, Organization = v.Organization, JobTitle = v.JobTitle,
                    Nationality = v.Nationality, MemberType = "GUEST", DisplayOrder = order++,
                    CreatedAt = now, CreatedBy = actorId,
                });
            foreach (var m in a.ExternalSupportMembers ?? new List<SupportTeamMemberDto>())
                rows.Add(new VisitGuestMember
                {
                    FullName = m.FullName, Organization = m.Organization, JobTitle = m.JobTitle,
                    Nationality = m.Nationality, MemberType = "EXTERNAL_SUPPORT", DisplayOrder = order++,
                    CreatedAt = now, CreatedBy = actorId,
                });
            foreach (var r in rows) request.GuestMembers.Add(r);

            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "instance.added",
                OldValueText = null,
                NewValueText = $"campus_id={snapshot.CampusId}",
                CreatedAt = now,
            });
            addedStaging.Add((instance, a, rows));
        }

        // ── 11. Request-level common fields (mutable subset only) + canonical recompute ──
        var commonChanged = ApplyCommonFields(request, edit, audit, now);

        var finalContents = edit.CampusVisits.Select(c => c.ToFormDto()).ToList();
        var scope = VisitRequestV2Canonical.ScopeOf(finalContents.Count);
        var hasMixed = VisitRequestV2Canonical.ComputeHasMixed(finalContents);
        var registrantEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(request.RegistrantEmail);
        var contactEmailNorm = VisitRequestFingerprintBuilder.NormalizeEmail(request.ContactPersonEmail);
        var fingerprint = VisitRequestV2Canonical.BuildFingerprint(
            registrantEmailNorm, contactEmailNorm, scope, finalContents);

        // Pure V2: each campus's content was already written to its own visit_instance_form_details.
        // The request row keeps identity, scope and lifecycle only — no compatibility projection.
        request.VisitScope = scope;
        request.HasMixedCampusDetails = hasMixed;
        request.BusinessFingerprint = fingerprint;
        request.RowVersion += 1;
        request.UpdatedAt = now;
        request.UpdatedBy = actorId;

        // ── FLUSH #1 — resolves new instance ids, form-detail PKs and new member ids. ──
        await _db.SaveChangesAsync(ct);

        // ── 12. Post-flush: composite links + immutable revision snapshots ──
        foreach (var (instance, newMembers) in changedInstances)
        {
            VisitRequestV2EditOps.LinkMembers(_db, request, instance, newMembers, now, actorId);
            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = instance.FormDetail!.FormRevision,
                ApprovalRevision = instance.FormDetail.ApprovalRevision,
                SourceType = "SAFE_EDIT",
                SnapshotJson = VisitRequestV2EditOps.SnapshotJson(instance.FormDetail, newMembers),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }
        foreach (var (instance, _, members) in addedStaging)
        {
            VisitRequestV2EditOps.LinkMembers(_db, request, instance, members, now, actorId);
            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = 1,
                ApprovalRevision = 1,
                SourceType = "CREATE",
                SnapshotJson = VisitRequestV2EditOps.SnapshotJson(instance.FormDetail!, members),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        if (commonChanged)
        {
            var nextRevision = (await _db.VisitRequestRevisionHistories
                .Where(r => r.VisitRequestId == request.VisitRequestId)
                .MaxAsync(r => (uint?)r.RequestRevision, ct) ?? 0) + 1;
            _db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                RequestRevision = nextRevision,
                SourceType = "SAFE_EDIT",
                SnapshotJson = VisitRequestV2EditOps.RequestSnapshotJson(request),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        // ── FLUSH #2 — links + revisions. Caller commits. ──
        await _db.SaveChangesAsync(ct);

        return new V2EditResult(scope, hasMixed, request.RowVersion);
    }

    public async Task<V2EditResult> ApplyResubmitAsync(
        VisitRequest request, VisitRequestEditV2Dto edit, ulong actorId, DateTime now, CancellationToken ct)
    {
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

        // ── 3. Per-instance optimistic concurrency ──
        foreach (var (content, instance) in pairs)
            if (content.ExpectedRowVersion is null || content.ExpectedRowVersion != instance.RowVersion)
                throw new ConflictException(
                    "Lịch thăm tại một cơ sở đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                    VisitRequestErrorCodes.InstanceVersionConflict);

        // ── 4. New schedule: every start ≥ now + 24h, end > start, ≥ 30 min ──
        foreach (var cv in edit.CampusVisits)
        {
            if (cv.PlannedEndAt <= cv.PlannedStartAt)
                throw new BusinessRuleException("Thời gian kết thúc phải sau thời gian bắt đầu.", VisitRequestErrorCodes.InvalidVisitTime);
            if ((cv.PlannedEndAt - cv.PlannedStartAt).TotalMinutes < MinDurationMinutes)
                throw new BusinessRuleException("Mỗi buổi thăm phải kéo dài tối thiểu 30 phút.", VisitRequestErrorCodes.InvalidVisitTime);
            if (cv.PlannedStartAt < now.AddHours(EditWindowHours))
                throw new BusinessRuleException(
                    "Thời gian bắt đầu mới phải cách hiện tại ít nhất 24 giờ.",
                    VisitRequestErrorCodes.InvalidVisitTime);
        }

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
            VisitRequestFingerprintBuilder.NormalizeEmail(request.ContactPersonEmail),
            scope, finalContents);
        var projection = pairs.OrderBy(p => p.Instance.CampusId).First().Content;

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
        var staging = new List<(VisitRequestCampus Instance, List<VisitGuestMember> Members)>();
        foreach (var (content, instance) in pairs)
        {
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
            staging.Add((instance, newMembers));
        }

        await _db.SaveChangesAsync(ct); // FLUSH #2 — instance resets + new member ids

        // ── Phase 3: member links + RESUBMIT revision snapshots (history keeps every prior revision). ──
        foreach (var (instance, members) in staging)
        {
            VisitRequestV2EditOps.LinkMembers(_db, request, instance, members, now, actorId);
            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = instance.FormDetail!.FormRevision,
                ApprovalRevision = instance.FormDetail.ApprovalRevision,
                SourceType = "RESUBMIT",
                SnapshotJson = VisitRequestV2EditOps.SnapshotJson(instance.FormDetail, members),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        var nextRevision = (await _db.VisitRequestRevisionHistories
            .Where(r => r.VisitRequestId == request.VisitRequestId)
            .MaxAsync(r => (uint?)r.RequestRevision, ct) ?? 0) + 1;
        _db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
        {
            VisitRequestId = request.VisitRequestId,
            RequestRevision = nextRevision,
            SourceType = "RESUBMIT",
            SnapshotJson = VisitRequestV2EditOps.RequestSnapshotJson(request),
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = correlationId,
        });
        _ = commonChanged; // resubmit always records a request revision; the audit already carries field diffs

        await _db.SaveChangesAsync(ct); // FLUSH #3 — links + revisions. Caller commits.

        return new V2EditResult(scope, hasMixed, request.RowVersion);
    }

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

    /// <summary>Registrant snapshot, partner and BOTH account-binding emails are immutable in a form edit.
    /// Changing the primary-contact identity is the Phase D identity workflow, never a pending edit.</summary>
    private static void ValidateImmutableFields(VisitRequest request, VisitRequestEditV2Dto edit)
    {
        var r = edit.Registrant;
        if (!string.Equals(request.RegistrantFullName, r.FullName, StringComparison.Ordinal) ||
            !string.Equals(request.RegistrantNationality, r.Nationality, StringComparison.Ordinal) ||
            !string.Equals(request.RegistrantJobTitle, r.JobTitle, StringComparison.Ordinal) ||
            // Compare NORMALIZED phones so the same number in national vs E.164 form is treated as
            // unchanged — the stored value is E.164 (create normalizes it), and a direct API edit
            // sending "0912345678" must not read as an immutable-field violation.
            !string.Equals(PhoneNumber.NormalizeOrOriginal(request.RegistrantPhone),
                           PhoneNumber.NormalizeOrOriginal(r.Phone), StringComparison.Ordinal) ||
            !string.Equals(request.RegistrantEmail, r.Email, StringComparison.OrdinalIgnoreCase) ||
            request.PartnerId != edit.PartnerId)
        {
            throw new BusinessRuleException(
                "Thông tin người đăng ký không được phép thay đổi.", "IMMUTABLE_REGISTRANT_INFO");
        }
        // Without a partner the organization is the registrant's own snapshot — immutable like the rest.
        // With a partner it is derived from the partner row, so the payload echo is not compared.
        if (!request.PartnerId.HasValue
            && !string.Equals(request.RegistrantOrganization, r.Organization, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "Thông tin người đăng ký không được phép thay đổi.", "IMMUTABLE_REGISTRANT_INFO");
        }
        if (!string.Equals(request.ContactPersonEmail, edit.PrimaryContact.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "Không được phép thay đổi email của đầu mối liên hệ.", "IMMUTABLE_CONTACT_IDENTITY");
        }
    }

    /// <summary>Applies the mutable request-level contact fields; returns whether anything changed.</summary>
    private static bool ApplyCommonFields(VisitRequest request, VisitRequestEditV2Dto edit, AuditLog audit, DateTime now)
    {
        var c = edit.PrimaryContact;
        var changed = false;
        void Apply(string field, string? oldValue, string? newValue, Action set)
        {
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return;
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = field, OldValueText = oldValue, NewValueText = newValue, CreatedAt = now,
            });
            set();
            changed = true;
        }
        Apply("contact_person_full_name", request.ContactPersonFullName, c.FullName, () => request.ContactPersonFullName = c.FullName);
        Apply("contact_person_organization", request.ContactPersonOrganization, c.Organization, () => request.ContactPersonOrganization = c.Organization);
        // Store the contact phone as E.164, like create — and normalize the stored side of the diff
        // too, so re-saving an unchanged national-format number does not log a spurious change.
        var contactPhone = PhoneNumber.NormalizeOrOriginal(c.Phone);
        Apply("contact_person_phone", PhoneNumber.NormalizeOrOriginal(request.ContactPersonPhone), contactPhone,
            () => request.ContactPersonPhone = contactPhone);
        return changed;
    }

    /// <summary>Rebuilds the CURRENT canonical content of one instance (detail + linked members) for change detection.</summary>
    private static CampusVisitFormDto CurrentContentOf(
        VisitRequest request, VisitRequestCampus instance, VisitInstanceFormDetail d)
    {
        var membersById = request.GuestMembers.ToDictionary(m => m.GuestMemberId);
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
                .Select(m => new VisitorDto(m.FullName, m.Nationality ?? string.Empty, m.JobTitle ?? string.Empty, m.Organization ?? string.Empty)).ToList(),
            linked.Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .Select(m => new SupportTeamMemberDto(m.FullName, m.JobTitle ?? string.Empty, m.Organization ?? string.Empty, m.Nationality ?? string.Empty)).ToList(),
            new ContactPointDto(d.OperationalContactFullName, d.OperationalContactOrganization, d.OperationalContactPhone, d.OperationalContactEmail),
            d.WorkingLanguage, d.TransportationNote, d.MediaConsentStatus, d.MediaConsentNote, d.NoteToFptu,
            Processing: null);
    }

    /// <summary>
    /// A campus can only be dropped by a pending edit while its instance is still WAITING and carries no
    /// downstream data (participants / agendas / logistics). Details, links and revision history cascade at
    /// the DB; anything else must block the removal rather than be cascaded blindly.
    /// </summary>
    private async Task EnsureInstanceRemovableAsync(VisitRequestCampus gone, CancellationToken ct)
    {
        if (gone.Status != VisitInstanceStatuses.WaitingRequestApproval)
            throw new BusinessRuleException(
                "Không thể bỏ cơ sở đã được xử lý (duyệt/từ chối/hủy).",
                VisitRequestErrorCodes.InstanceNotRemovable);

        var hasDownstream =
            await _db.VisitParticipants.AnyAsync(p => p.VisitInstanceId == gone.VisitInstanceId, ct)
            || await _db.VisitAgendas.AnyAsync(a => a.VisitInstanceId == gone.VisitInstanceId, ct)
            || await _db.VisitLogisticsItems.AnyAsync(l => l.VisitInstanceId == gone.VisitInstanceId, ct);
        if (hasDownstream)
            throw new BusinessRuleException(
                "Không thể bỏ cơ sở vì lịch thăm tại cơ sở này đã có dữ liệu chuẩn bị (người tham dự/chương trình/hậu cần).",
                VisitRequestErrorCodes.InstanceNotRemovable);
    }
}
