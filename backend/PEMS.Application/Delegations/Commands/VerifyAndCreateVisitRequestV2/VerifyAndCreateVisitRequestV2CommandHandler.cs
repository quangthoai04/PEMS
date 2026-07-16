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

        var now = _clock.VietnamNow;
        var email = form.Registrant.Email.Trim().ToLowerInvariant();

        // ── Idempotency pre-check (BEFORE OTP verify): a retry whose original submit already committed must
        //    replay idempotently — its OTP is already consumed, so re-verifying would surface a misleading
        //    OTP error. Never verifies the OTP on this path. ──
        var replay = await CheckIdempotentReplayAsync(form.SubmissionId, cancellationToken);
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
                var lateReplay = await CheckIdempotentReplayAsync(form.SubmissionId, cancellationToken);
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

            // ── 2. Provision ONLY the registrant account. The primary contact, if different, is deliberately
            //       NOT provisioned here — CreateV2Async leaves visitor_user_id NULL and creates the
            //       INITIAL_CLAIM (Phase D confirms/links contact B only after B explicitly accepts). ──
            await _userProvisionService.ValidateRegistrantEmailUsableForPublicFlowAsync(email, cancellationToken);
            var registrantUserId = await _userProvisionService.EnsureVisitorAccountAsync(
                email, form.Registrant.FullName, form.Registrant.Phone, now, cancellationToken);

            // ── 3. Build the whole v2 aggregate in this transaction. ──
            created = await _createService.CreateV2Async(
                form, registrantUserId, "VISITOR_SUBMITTED", now, cancellationToken);

            // ── 4. Consume the OTP atomically with the request. A rollback below un-consumes it. ──
            otpToken.UsedAt = now;
            await _db.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            committed = true;
        }
        catch (DbUpdateException) when (!committed)
        {
            // uq_visit_requests_submission_id: a concurrent retry of the SAME submission intent won the insert
            // race. Roll back, re-query the winner and replay idempotently; otherwise the error is not swallowed.
            await tx.RollbackAsync(cancellationToken);
            var raced = await CheckIdempotentReplayAsync(form.SubmissionId, cancellationToken);
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

        // ── 5. Post-commit notifications (best-effort, first-create only; replays never reach here). ──
        await V2CreateNotifier.NotifyStaffLeadersAfterCommitAsync(
            _db, _notificationService, _logger, created, cancellationToken);

        return await ToResponseAsync(created.VisitRequestId, cancellationToken, idempotent: false,
            "Đơn đăng ký thăm quan đã được gửi thành công và đang chờ phê duyệt.");
    }

    /// <summary>
    /// Same-submission replay: a v2 request already exists for this submissionId → return it (200, no new row,
    /// no OTP verify, no side effects). Consistent with the authenticated create-v2 idempotency.
    /// </summary>
    private async Task<VerifyAndCreateVisitRequestV2Response?> CheckIdempotentReplayAsync(
        string submissionId, CancellationToken cancellationToken)
    {
        var existing = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.SubmissionId == submissionId && v.FormSchemaVersion >= FormSchemaVersions.PerCampus)
            .Select(v => v.VisitRequestId)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing == 0)
            return null;
        return await ToResponseAsync(existing, cancellationToken, idempotent: true,
            "Đơn đăng ký này đã được ghi nhận trước đó. Không có đơn mới nào được tạo.");
    }

    private async Task<VerifyAndCreateVisitRequestV2Response> ToResponseAsync(
        ulong visitRequestId, CancellationToken ct, bool idempotent, string message)
    {
        var head = await _db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == visitRequestId)
            .Select(v => new { v.RequestCode, v.VisitScope, v.HasMixedCampusDetails, v.PrimaryContactAccessStatus, v.VisitorUserId })
            .FirstAsync(ct);
        var instances = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == visitRequestId)
            .OrderBy(c => c.CampusId)
            .Select(c => new CreateVisitRequestV2CampusRef(c.VisitInstanceId, c.CampusId, c.Status))
            .ToListAsync(ct);
        return new VerifyAndCreateVisitRequestV2Response(
            visitRequestId, head.RequestCode ?? string.Empty, head.VisitScope, head.HasMixedCampusDetails,
            head.PrimaryContactAccessStatus,
            ContactClaimPending: head.VisitorUserId is null,
            instances, idempotent, message);
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
            OtpErrorCodes.RetryLater                => (429, "Bạn thao tác quá nhanh. Vui lòng chờ trước khi thử lại."),
            OtpErrorCodes.ResendTooSoon             => (429, "Temporarily unable to issue another verification code."),
            OtpErrorCodes.StandardRateLimited       => (429, "Temporarily unable to issue another verification code."),
            OtpErrorCodes.RecoveryRateLimited       => (429, "Temporarily unable to issue another verification code."),
            OtpErrorCodes.AbsoluteRateLimited       => (429, "Temporarily unable to issue another verification code."),
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
}
