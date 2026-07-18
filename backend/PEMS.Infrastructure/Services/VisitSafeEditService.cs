using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Services;

/// <summary>See <see cref="IVisitSafeEditService"/>. The classifier (<see cref="VisitFieldClassifier"/>)
/// is the single gate: anything outside SAFE/PRIVACY_URGENT fails closed here regardless of what the
/// endpoint accepted structurally.</summary>
public sealed class VisitSafeEditService : IVisitSafeEditService
{
    private const int CutoffHours = 24;

    private readonly IApplicationDbContext _db;

    public VisitSafeEditService(IApplicationDbContext db) => _db = db;

    private sealed record Change(
        string FieldPath, ulong? InstanceId, string? OldValue, string? NewValue, string Class,
        Action Apply);

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

        // ── 1. Request-level safe subset (registrant display fields + contact snapshot, never emails) ──
        if (patch.Registrant is { } reg)
        {
            if (string.IsNullOrWhiteSpace(reg.FullName))
                throw new BusinessRuleException("Họ tên người đăng ký không được để trống.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
            Diff(changes, VisitFieldClassifier.RegistrantFullName, null,
                request.RegistrantFullName, reg.FullName.Trim(), v => request.RegistrantFullName = v!);
            Diff(changes, VisitFieldClassifier.RegistrantOrganization, null,
                request.RegistrantOrganization, Clean(reg.Organization), v => request.RegistrantOrganization = v);
            Diff(changes, VisitFieldClassifier.RegistrantJobTitle, null,
                request.RegistrantJobTitle, Clean(reg.JobTitle), v => request.RegistrantJobTitle = v);
            Diff(changes, VisitFieldClassifier.RegistrantPhone, null,
                request.RegistrantPhone, Clean(reg.Phone), v => request.RegistrantPhone = v);
        }
        if (patch.Contact is { } contact)
        {
            if (string.IsNullOrWhiteSpace(contact.FullName) || string.IsNullOrWhiteSpace(contact.Phone))
                throw new BusinessRuleException("Họ tên/điện thoại đầu mối liên hệ không được để trống.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);
            Diff(changes, VisitFieldClassifier.ContactFullName, null,
                request.ContactPersonFullName, contact.FullName.Trim(), v => request.ContactPersonFullName = v!);
            Diff(changes, VisitFieldClassifier.ContactOrganization, null,
                request.ContactPersonOrganization, Clean(contact.Organization), v => request.ContactPersonOrganization = v);
            Diff(changes, VisitFieldClassifier.ContactPhone, null,
                request.ContactPersonPhone, contact.Phone.Trim(), v => request.ContactPersonPhone = v!);
        }

        // ── 2. Per-instance safe subset ──
        var touchedInstances = new List<VisitRequestCampus>();
        foreach (var ip in patch.Instances ?? new List<SafeInstancePatchDto>())
        {
            var instance = request.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == ip.VisitInstanceId)
                ?? throw new BusinessRuleException("Cơ sở được sửa không thuộc đơn này.",
                    VisitFormV2ErrorCodes.VisitInstanceScopeForbidden);
            if (ip.ExpectedRowVersion != instance.RowVersion)
                throw new ConflictException(
                    "Lịch thăm tại một cơ sở đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.",
                    VisitFormV2ErrorCodes.VisitFormConcurrencyConflict);
            if (instance.Status is not (VisitInstanceStatuses.WaitingRequestApproval
                or VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit))
                throw new BusinessRuleException(
                    "Cơ sở này đã bắt đầu/kết thúc hoặc không còn hoạt động nên không thể sửa.",
                    VisitRequestErrorCodes.VisitRequestNotEditable);
            var detail = instance.FormDetail
                ?? throw new ConflictException("Thiếu dữ liệu biểu mẫu theo cơ sở.",
                    VisitFormV2ErrorCodes.VisitFormDetailMissing);

            if (ip.MediaConsentStatus is not ("AGREED" or "DECLINED"))
                throw new BusinessRuleException("Trạng thái truyền thông không hợp lệ.",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

            var before = changes.Count;
            Diff(changes, VisitFieldClassifier.TransportationNote, instance.VisitInstanceId,
                detail.TransportationNote, Clean(ip.TransportationNote), v => detail.TransportationNote = v);
            Diff(changes, VisitFieldClassifier.NoteToFptu, instance.VisitInstanceId,
                detail.NoteToFptu, Clean(ip.NoteToFptu), v => detail.NoteToFptu = v);
            Diff(changes, VisitFieldClassifier.MediaConsentNote, instance.VisitInstanceId,
                detail.MediaConsentNote, Clean(ip.MediaConsentNote), v => detail.MediaConsentNote = v);
            Diff(changes, VisitFieldClassifier.MediaConsentStatus, instance.VisitInstanceId,
                detail.MediaConsentStatus, ip.MediaConsentStatus, v => detail.MediaConsentStatus = v!);
            if (changes.Count > before)
                touchedInstances.Add(instance);
        }

        if (changes.Count == 0)
            throw new BusinessRuleException("Không có thay đổi nào để áp dụng.",
                VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

        // ── 3. Classifier gate — fail closed on anything that is not SAFE/PRIVACY_URGENT ──
        foreach (var c in changes)
            if (c.Class is not (AmendmentChangeClasses.Safe or AmendmentChangeClasses.PrivacyUrgent))
                throw new BusinessRuleException(
                    $"Trường '{c.FieldPath}' không thuộc nhóm sửa nhanh. Vui lòng gửi đề xuất thay đổi (amendment).",
                    VisitFormV2ErrorCodes.SafeEditFieldNotAllowed);

        // ── 4. 24h cutoff — privacy-urgent media withdrawal (plus its note, same instance) is exempt ──
        foreach (var instance in touchedInstances)
        {
            if (instance.PlannedStartAt >= now.AddHours(CutoffHours)) continue;
            var instanceChanges = changes.Where(c => c.InstanceId == instance.VisitInstanceId).ToList();
            var hasUrgent = instanceChanges.Any(c => c.Class == AmendmentChangeClasses.PrivacyUrgent);
            var nonExempt = instanceChanges.Where(c =>
                c.Class != AmendmentChangeClasses.PrivacyUrgent
                && !(hasUrgent && c.FieldPath == VisitFieldClassifier.MediaConsentNote)).ToList();
            if (nonExempt.Count > 0)
                throw new BusinessRuleException(
                    "Lịch thăm bắt đầu trong vòng 24 giờ nên chỉ có thể rút quyền truyền thông; các thay đổi khác vui lòng liên hệ FPTU.",
                    VisitRequestErrorCodes.VisitCancelWindowExpired);
        }
        if (changes.Any(c => c.InstanceId is null))
        {
            var earliest = request.CampusInstances
                .Where(c => c.Status is VisitInstanceStatuses.WaitingRequestApproval
                    or VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit)
                .Select(c => (DateTime?)c.PlannedStartAt)
                .DefaultIfEmpty(null)
                .Min();
            if (earliest is not null && earliest < now.AddHours(CutoffHours))
                throw new BusinessRuleException(
                    "Lịch thăm bắt đầu trong vòng 24 giờ nên không thể sửa thông tin chung. Vui lòng liên hệ FPTU.",
                    VisitRequestErrorCodes.VisitCancelWindowExpired);
        }

        // ── 5. Apply + revisions + audit, one commit ──
        var correlationId = Guid.NewGuid().ToString("N");
        foreach (var c in changes) c.Apply();

        foreach (var instance in touchedInstances)
        {
            var detail = instance.FormDetail!;
            detail.FormRevision += 1;
            detail.RowVersion += 1;
            detail.UpdatedAt = now;
            detail.UpdatedBy = actorId;
            instance.RowVersion += 1;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;

            var members = MembersOf(request, instance);
            _db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = detail.FormRevision,
                ApprovalRevision = detail.ApprovalRevision,
                SourceType = FormRevisionSourceTypes.SafeEdit,
                SnapshotJson = VisitRequestV2EditOps.SnapshotJson(detail, members),
                AppliedBy = actorId,
                AppliedAt = now,
                Reason = correlationId,
            });
        }

        if (changes.Any(c => c.InstanceId is null))
        {
            var nextRevision = (await _db.VisitRequestRevisionHistories
                .Where(r => r.VisitRequestId == request.VisitRequestId)
                .MaxAsync(r => (uint?)r.RequestRevision, ct) ?? 0) + 1;
            _db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
            {
                VisitRequestId = request.VisitRequestId,
                RequestRevision = nextRevision,
                SourceType = FormRevisionSourceTypes.SafeEdit,
                SnapshotJson = VisitRequestV2EditOps.RequestSnapshotJson(request),
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

        await _db.SaveChangesAsync(ct);

        return new VisitRequestSafeEditResponse(
            request.VisitRequestId,
            changes.Select(c => new SafeEditAppliedChange(c.FieldPath, c.InstanceId, c.Class)).ToList(),
            request.RowVersion,
            request.CampusInstances.ToDictionary(c => c.VisitInstanceId, c => c.RowVersion),
            "Đã cập nhật các thông tin cho phép sửa nhanh.");
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
            VisitRequestFingerprintBuilder.NormalizeEmail(request.ContactPersonEmail),
            scope, contents);

        var projection = request.CampusInstances.OrderBy(c => c.CampusId).First().FormDetail!;
        request.VisitScope = scope;
        request.HasMixedCampusDetails = hasMixed;
        request.BusinessFingerprint = fingerprint;
        request.DelegationName = projection.DelegationName;
        request.VisitType = projection.VisitType!;
        request.VisitTypeOther = projection.VisitTypeOther;
        request.Purpose = projection.Purpose;
        request.WorkingContent = projection.WorkingContent;
        request.WorkingLanguage = projection.WorkingLanguage;
        request.TransportationNote = projection.TransportationNote;
        request.MediaConsentStatus = projection.MediaConsentStatus!;
        request.MediaConsentNote = projection.MediaConsentNote;
        request.NoteToFptu = projection.NoteToFptu;
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
                .Select(m => new VisitorDto(m.FullName, m.Nationality ?? string.Empty, m.JobTitle ?? string.Empty, m.Organization ?? string.Empty)).ToList(),
            members.Where(m => m.MemberType == "EXTERNAL_SUPPORT")
                .Select(m => new SupportTeamMemberDto(m.FullName, m.JobTitle ?? string.Empty, m.Organization ?? string.Empty, m.Nationality ?? string.Empty)).ToList(),
            new ContactPointDto(
                d.OperationalContactFullName ?? string.Empty, d.OperationalContactOrganization ?? string.Empty,
                d.OperationalContactPhone ?? string.Empty, d.OperationalContactEmail ?? string.Empty),
            d.WorkingLanguage ?? "EN", d.TransportationNote, d.MediaConsentStatus ?? "DECLINED", d.MediaConsentNote,
            d.NoteToFptu, null);
    }
}
