using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.InitiateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequestV2;

/// <summary>
/// Public OTP → per-campus form v2 create. Mirrors the proven v1 <c>VerifyAndCreateVisitRequestCommandHandler</c>
/// OTP-consume / idempotent-replay / race-replay mechanics, but builds the aggregate through the shared
/// <see cref="IVisitRequestV2CreateService"/> and adds NO new OTP logic (it reuses the identical
/// <see cref="IOtpService.VerifyChallengeAsync"/> primitive). The whole thing — OTP consume + account
/// provisioning + the v2 aggregate — commits atomically or not at all.
/// </summary>
public sealed class VerifyAndCreateVisitRequestV2CommandHandler
    : IRequestHandler<VerifyAndCreateVisitRequestV2Command, VerifyAndCreateVisitRequestV2Response>
{
    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IUserProvisionService _userProvisionService;
    private readonly IVisitRequestV2CreateService _createService;
    private readonly INotificationService _notificationService;
    private readonly IOperationalContactInvitationService _contactInvitations;
    private readonly IDateTimeService _clock;
    private readonly ILogger<VerifyAndCreateVisitRequestV2CommandHandler> _logger;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public VerifyAndCreateVisitRequestV2CommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        IUserProvisionService userProvisionService,
        IVisitRequestV2CreateService createService,
        INotificationService notificationService,
        IOperationalContactInvitationService contactInvitations,
        IDateTimeService clock,
        ILogger<VerifyAndCreateVisitRequestV2CommandHandler> logger,
        PerCampusFormV2Options readFlag,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _otpService = otpService;
        _userProvisionService = userProvisionService;
        _createService = createService;
        _notificationService = notificationService;
        _contactInvitations = contactInvitations;
        _clock = clock;
        _logger = logger;
        _readFlag = readFlag;
        _writeFlag = writeFlag;
    }

    public async Task<VerifyAndCreateVisitRequestV2Response> Handle(
        VerifyAndCreateVisitRequestV2Command request, CancellationToken cancellationToken)
    {
        // ── Flag gate (identical to the authenticated create-v2) ──
        if (!_writeFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!_readFlag.Enabled)
            throw new ConflictException(
                "Cấu hình không hợp lệ: bật ghi v2 nhưng chưa bật đọc v2.",
                CreateVisitRequestV2ErrorCodes.ReadRequired);

        var form = request.Form;
        if (string.IsNullOrWhiteSpace(form.SubmissionId))
            throw new BusinessRuleException("Thiếu submissionId.", "SUBMISSION_ID_REQUIRED");

        // Defence in depth: initiate already refuses a direct processing intent, but a snapshot could only
        // ever have been bound without one — so a verify-time form carrying one is a forged retry. Reject
        // before the OTP is consumed rather than dropping the intent silently.
        RegistrantIdentityRules.EnsureNoHostProposalIntent(form);

        var now = _clock.VietnamNow;
        var email = RegistrantIdentityRules.Normalize(form.Registrant.Email);
        var currentFingerprint = V2PendingFormSnapshot.Fingerprint(form);

        // ── Idempotency pre-check (BEFORE OTP verify): a retry whose original submit already committed must
        //    replay idempotently — its OTP is already consumed, so re-verifying would surface a misleading
        //    OTP error. Never verifies the OTP on this path. ──
        var replay = await CheckIdempotentReplayAsync(form.SubmissionId, currentFingerprint, cancellationToken);
        if (replay is not null)
            return replay;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);
        var committed = false;

        VisitRequest created;
        try
        {
            // ── 1. Verify the OTP challenge (locks the challenge row FOR UPDATE). ──
            var otpResult = await _otpService.VerifyChallengeAsync(
                request.SessionToken, email, OtpPurposes.VisitRequestVerify,
                form.SubmissionId, request.OtpCode, cancellationToken);

            if (!otpResult.Success)
            {
                // A concurrent/earlier retry of THIS submission may have already created the request and
                // consumed the OTP — replay idempotently instead of erroring.
                var lateReplay = await CheckIdempotentReplayAsync(form.SubmissionId, currentFingerprint, cancellationToken);
                if (lateReplay is not null)
                {
                    await tx.CommitAsync(cancellationToken);
                    committed = true;
                    return lateReplay;
                }

                // Persist attempt/cooldown/burn state FIRST, then surface the typed error.
                await tx.CommitAsync(cancellationToken);
                committed = true;
                throw BuildOtpException(otpResult);
            }

            var otpToken = otpResult.Token!;

            // ── 2. Load the snapshot BOUND at initiate. The request is built from THIS, never the verify-time
            //       form: the client cannot alter campus/member/contact/time/content between initiate and
            //       verify. Absent/expired/consumed → the submission was never initiated (or a concurrent
            //       create already won): re-check idempotency, else a stable conflict (no request created). ──
            var pending = await _db.VisitRequestPendingForms
                .FirstOrDefaultAsync(p => p.SubmissionId == form.SubmissionId, cancellationToken);
            if (pending is null || pending.ConsumedAt is not null || pending.ExpiresAt <= now)
            {
                var lateReplay = await CheckIdempotentReplayAsync(form.SubmissionId, currentFingerprint, cancellationToken);
                if (lateReplay is not null)
                {
                    await tx.CommitAsync(cancellationToken);
                    committed = true;
                    return lateReplay;
                }
                throw new ConflictException(
                    "Không tìm thấy phiên đăng ký đã khởi tạo. Vui lòng bắt đầu lại.",
                    InitiateVisitRequestV2ErrorCodes.PendingSubmissionNotFound);
            }

            // Defence-in-depth: the verify-time form must match the bound snapshot's canonical fingerprint.
            // A mismatch means the client changed core content after initiate → stable conflict, no create.
            if (currentFingerprint != pending.FingerprintV2)
                throw new ConflictException(
                    "Nội dung biểu mẫu đã thay đổi so với lúc khởi tạo. Vui lòng gửi lại.",
                    InitiateVisitRequestV2ErrorCodes.SubmissionFormMismatch);

            var boundForm = V2PendingFormSnapshot.Deserialize(pending.SnapshotJson);
            var boundEmail = boundForm.Registrant.Email.Trim().ToLowerInvariant();

            // ── 3. Duplicate guard (business fingerprint): another submit intent with the same core visit
            //       identity committed within 15 mins → consume OTP/snapshot and throw 409 DUPLICATE_VISIT_REQUEST. ──
            var boundRegistrantEmail = VisitRequestFingerprintBuilder.NormalizeEmail(boundForm.Registrant.Email);
            var campusCount = boundForm.CampusVisits
                .Select(c => c.CampusId?.Trim().ToUpperInvariant() ?? string.Empty)
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var boundScope = campusCount > 1 ? VisitScopes.MultiCampus : VisitScopes.SingleCampus;
            // Each campus contributes its own contact address to the identity — see BuildV2.
            var fingerprint = VisitRequestFingerprintBuilder.BuildV2(
                boundRegistrantEmail, boundScope,
                boundForm.CampusVisits.Select(cv =>
                    (cv.CampusId, cv.PlannedStartAt, cv.PlannedEndAt, cv.DelegationName, cv.VisitType, cv.VisitTypeOther,
                     (string?)cv.OperationalContact.Email)));

            var duplicateWindowStart = now.AddMinutes(-15);

            // Row-level lock the fingerprint so concurrent requests sequence on the duplicate check.
            var dbContext = (DbContext)_db;
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT IGNORE INTO visit_request_fingerprint_guards (fingerprint, created_at, updated_at) VALUES ({0}, {1}, {1})",
                fingerprint, now);
            await dbContext.Set<VisitRequestFingerprintGuard>()
                .FromSqlRaw("SELECT * FROM visit_request_fingerprint_guards WHERE fingerprint = {0} FOR UPDATE", fingerprint)
                .FirstOrDefaultAsync(cancellationToken);

            var duplicate = await _db.VisitRequests.AsNoTracking()
                .Where(r => r.BusinessFingerprint == fingerprint
                            && r.SubmittedAt >= duplicateWindowStart
                            && r.Status != VisitRequestStatuses.Rejected
                            && r.Status != VisitRequestStatuses.Cancelled)
                .OrderByDescending(r => r.SubmittedAt)
                .Select(r => new { r.VisitRequestId, r.RequestCode, r.Status, r.SubmittedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (duplicate is not null)
            {
                otpToken.UsedAt = now;
                pending.ConsumedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                committed = true;

                throw new ConflictException(
                    "Một đơn đăng ký với nội dung tương tự vừa được gửi trước đó. Không có đơn mới nào được tạo.",
                    VisitRequestErrorCodes.DuplicateVisitRequest,
                    new
                    {
                        existingVisitRequestId = duplicate.VisitRequestId,
                        existingRequestCode    = duplicate.RequestCode,
                        existingStatus         = duplicate.Status,
                        existingSubmittedAt    = duplicate.SubmittedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    });
            }

            // ── 4. Provision ONLY the registrant account (from the BOUND snapshot). ──
            await _userProvisionService.ValidateRegistrantEmailUsableForPublicFlowAsync(boundEmail, cancellationToken);
            var registrantUserId = await _userProvisionService.EnsureVisitorAccountAsync(
                boundEmail, boundForm.Registrant.FullName, boundForm.Registrant.Phone, now, cancellationToken);

            // ── 5. Build the whole v2 aggregate FROM THE BOUND SNAPSHOT in this transaction. ──
            created = await _createService.CreateV2Async(
                boundForm, registrantUserId, "VISITOR_SUBMITTED", now, cancellationToken);

            // ── 6. Consume the OTP AND the bound snapshot atomically with the request. A rollback un-consumes both. ──
            otpToken.UsedAt = now;
            pending.ConsumedAt = now;
            await _db.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            committed = true;
        }
        catch (DbUpdateException) when (!committed)
        {
            // uq_visit_requests_submission_id: a concurrent retry of the SAME submission intent won the insert
            // race. Roll back, re-query the winner and replay idempotently; otherwise the error is not swallowed.
            await tx.RollbackAsync(cancellationToken);
            var raced = await CheckIdempotentReplayAsync(form.SubmissionId, currentFingerprint, cancellationToken);
            if (raced is not null)
                return raced;
            throw;
        }
        catch
        {
            if (!committed)
                await tx.RollbackAsync(cancellationToken);
            throw;
        }

        // ── 7. Post-commit notifications (best-effort, first-create only; replays never reach here). ──
        await V2CreateNotifier.NotifyStaffLeadersAfterCommitAsync(
            _db, _notificationService, _logger, created, cancellationToken);
        await V2CreateNotifier.SendOperationalContactInvitationsAfterCommitAsync(
            _db, _contactInvitations, _logger, created, cancellationToken);

        return await ToResponseAsync(created.VisitRequestId, cancellationToken, idempotent: false,
            "Đơn đăng ký thăm quan đã được gửi thành công và đang chờ phê duyệt.");
    }

    /// <summary>
    /// Same-submission replay: a v2 request already exists for this submissionId → return it (200, no new row,
    /// no OTP verify, no side effects). Consistent with the authenticated create-v2 idempotency.
    /// </summary>
    private async Task<VerifyAndCreateVisitRequestV2Response?> CheckIdempotentReplayAsync(
        string submissionId, string? currentFingerprint, CancellationToken cancellationToken)
    {
        var existing = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.SubmissionId == submissionId)
            .Select(v => new { v.VisitRequestId, v.BusinessFingerprint })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
            return null;

        if (currentFingerprint != null && !string.Equals(existing.BusinessFingerprint, currentFingerprint, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "SubmissionId đã được sử dụng cho một yêu cầu khác với nội dung khác.",
                VisitRequestErrorCodes.IdempotencyKeyReused);
        }

        return await ToResponseAsync(existing.VisitRequestId, cancellationToken, idempotent: true,
            "Đơn đăng ký này đã được ghi nhận trước đó. Không có đơn mới nào được tạo.");
    }

    private async Task<VerifyAndCreateVisitRequestV2Response> ToResponseAsync(
        ulong visitRequestId, CancellationToken ct, bool idempotent, string message)
    {
        var head = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == visitRequestId)
            .Select(v => new { v.RequestCode, v.VisitScope, v.HasMixedCampusDetails, v.Status, v.SubmittedAt })
            .FirstAsync(ct);
        var rows = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == visitRequestId)
            .OrderBy(c => c.CampusId)
            .Select(c => new { c.VisitInstanceId, c.CampusId, c.Status, c.OperationalContactUserId })
            .ToListAsync(ct);
        var instances = rows
            .Select(c => new CreateVisitRequestV2CampusRef(c.VisitInstanceId, c.CampusId, c.Status))
            .ToList();
        var pendingConfirmations = rows.Count(c =>
            c.Status != VisitInstanceStatuses.Cancelled && c.OperationalContactUserId is null);

        return new VerifyAndCreateVisitRequestV2Response(
            visitRequestId, head.RequestCode ?? string.Empty, head.VisitScope, head.HasMixedCampusDetails,
            pendingConfirmations,
            instances, idempotent, message,
            head.Status, head.SubmittedAt.ToString("yyyy-MM-ddTHH:mm:ss"), instances.Count);
    }

    /// <summary>Maps a failed challenge verification to the typed OTP error contract (same mapping as v1).</summary>
    private static OtpChallengeException BuildOtpException(OtpChallengeVerification result)
    {
        var (status, message) = result.ErrorCode switch
        {
            OtpErrorCodes.Invalid                   => (400, "Mã OTP không đúng. Vui lòng kiểm tra lại."),
            OtpErrorCodes.Expired                   => (400, "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới."),
            OtpErrorCodes.NotFound                  => (400, "Không tìm thấy phiên xác thực. Vui lòng yêu cầu mã mới."),
            OtpErrorCodes.SessionInvalid            => (400, "Phiên xác thực không còn hiệu lực. Vui lòng yêu cầu mã mới."),
            // The four rate-limit codes and the cooldown share one message builder with the issue path
            // (OtpService), so the sentence a user reads does not depend on which endpoint refused them.
            // All five carry the wait the policy already computed; they used to carry one English
            // sentence that carried neither the reason nor the time.
            OtpErrorCodes.RetryLater                => (429, RateLimitMessage(result, OtpErrorCodes.RetryLater)),
            OtpErrorCodes.ResendTooSoon             => (429, RateLimitMessage(result, OtpErrorCodes.ResendTooSoon)),
            OtpErrorCodes.StandardRateLimited       => (429, RateLimitMessage(result, OtpErrorCodes.StandardRateLimited)),
            OtpErrorCodes.RecoveryRateLimited       => (429, RateLimitMessage(result, OtpErrorCodes.RecoveryRateLimited)),
            OtpErrorCodes.AbsoluteRateLimited       => (429, RateLimitMessage(result, OtpErrorCodes.AbsoluteRateLimited)),
            OtpErrorCodes.HumanVerificationRequired => (428, "Bạn đã nhập sai quá nhiều lần. Vui lòng xác minh bạn không phải robot để nhận mã mới."),
            _                                       => (400, "Xác thực OTP thất bại.")
        };

        return new OtpChallengeException(
            status,
            result.ErrorCode ?? OtpErrorCodes.SessionInvalid,
            message,
            remainingAttempts: result.RemainingAttempts,
            retryAfterSeconds: result.RetryAfterSeconds > 0 ? result.RetryAfterSeconds : null,
            retryAt: result.RetryAt,
            humanVerificationRequired: result.HumanVerificationRequired);
    }

    /// <summary>The shared Vietnamese wording for a wait, fed the same metadata the response carries.</summary>
    private static string RateLimitMessage(OtpChallengeVerification result, string code)
        => PEMS.Application.Common.Security.OtpRateLimitMessages.Describe(
            code,
            result.RetryAfterSeconds > 0 ? result.RetryAfterSeconds : null,
            result.RetryAt);
}
