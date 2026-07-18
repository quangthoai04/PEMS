using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequestV2;

/// <summary>
/// Public v2 initiate: validate v2 form (pipeline) → fail-fast email checks → mint OTP challenge (existing
/// primitive) → BIND the canonical v2 snapshot to the submission intent. No request is created; the bound
/// snapshot is what <c>verify</c> builds from, so the client cannot alter campus/member/contact/time/content
/// between initiate and verify.
/// </summary>
public sealed class InitiateVisitRequestV2CommandHandler
    : IRequestHandler<InitiateVisitRequestV2Command, InitiateVisitRequestResponse>
{
    // How long the bound snapshot lives — comfortably longer than the OTP window (incl. resends) so the
    // real gate stays the OTP; this is only a cleanup bound. Expired snapshots are swept lazily at verify.
    private const int PendingSnapshotMinutes = 60;

    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IUserProvisionService _userProvisionService;
    private readonly IRequestMetadataService _requestMetadata;
    private readonly IDateTimeService _clock;
    private readonly IConfiguration _configuration;
    private readonly PerCampusFormV2Options _readFlag;
    private readonly PerCampusFormV2WriteOptions _writeFlag;

    public InitiateVisitRequestV2CommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        IEmailService emailService,
        IUserProvisionService userProvisionService,
        IRequestMetadataService requestMetadata,
        IDateTimeService clock,
        IConfiguration configuration,
        PerCampusFormV2Options readFlag,
        PerCampusFormV2WriteOptions writeFlag)
    {
        _db = db;
        _otpService = otpService;
        _emailService = emailService;
        _userProvisionService = userProvisionService;
        _requestMetadata = requestMetadata;
        _clock = clock;
        _configuration = configuration;
        _readFlag = readFlag;
        _writeFlag = writeFlag;
    }

    public async Task<InitiateVisitRequestResponse> Handle(
        InitiateVisitRequestV2Command request, CancellationToken cancellationToken)
    {
        // ── Flag gate (identical to create-v2): write OFF → 404 (v1 initiate is the only public path);
        //    write ON + read OFF → reject (would produce an unreadable v2 request at verify). ──
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

        // ── Fail fast (same depth as v1 initiate): the registrant email links a VISITOR account at verify —
        //    an internal/inactive email must never enter the public flow; the primary-contact email is what
        //    becomes the VISITOR login. Reject BEFORE any OTP is sent. ──
        await _userProvisionService.ValidateRegistrantEmailUsableForPublicFlowAsync(email, cancellationToken);
        await _userProvisionService.ValidateContactEmailCanBeUsedForVisitorAsync(
            form.PrimaryContact.Email, cancellationToken);

        // ── Mint the OTP challenge (bound to email + purpose + submissionId). Persists on its own. ──
        var issue = await _otpService.CreateChallengeAsync(
            email, OtpPurposes.VisitRequestVerify, form.SubmissionId, OtpIssueReasons.Initial,
            _requestMetadata.IpAddress, _requestMetadata.UserAgent, cancellationToken);

        // ── Bind the canonical v2 snapshot to this submission intent (create or refresh; survives resends). ──
        await BindPendingSnapshotAsync(form, email, now, cancellationToken);

        // ── Deliver the code. ──
        try
        {
            await _emailService.SendVisitRequestOtpAsync(
                email, form.Registrant.FullName, issue.Code, cancellationToken);
        }
        catch (Exception ex)
        {
            var details = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            throw new BusinessRuleException($"Không thể gửi mã OTP. Lỗi: {ex.Message} - {details}");
        }

        var isEmailEnabled = bool.TryParse(_configuration["Smtp:Enabled"], out var e) && e;
        var msg = isEmailEnabled
            ? "Mã xác thực đã được gửi tới email của bạn. Vui lòng kiểm tra hộp thư."
            : "Hệ thống đang ở chế độ DEV (Smtp:Enabled=false). Mã xác thực đã được in ra log của backend.";

        return new InitiateVisitRequestResponse(
            SessionToken: issue.SessionToken,
            Message: msg,
            MaskedEmail: MaskEmail(email),
            ExpiresAt: issue.ExpiresAt,
            ResendAfterSeconds: issue.ResendAfterSeconds,
            MaxAttempts: issue.MaxAttempts);
    }

    private async Task BindPendingSnapshotAsync(
        VisitRequestFormDataV2 form, string email, DateTime now, CancellationToken ct)
    {
        var snapshot = V2PendingFormSnapshot.Serialize(form);
        var fingerprint = V2PendingFormSnapshot.Fingerprint(form);
        var expiresAt = now.AddMinutes(PendingSnapshotMinutes);

        var existing = await _db.VisitRequestPendingForms
            .FirstOrDefaultAsync(p => p.SubmissionId == form.SubmissionId, ct);

        if (existing is null)
        {
            _db.VisitRequestPendingForms.Add(new VisitRequestPendingForm
            {
                SubmissionId = form.SubmissionId,
                RegistrantEmail = email,
                FormSchemaVersion = (short)FormSchemaVersions.PerCampus,
                FingerprintV2 = fingerprint,
                SnapshotJson = snapshot,
                CreatedAt = now,
                ExpiresAt = expiresAt,
                ConsumedAt = null,
            });
        }
        else
        {
            // Re-initiate of the same intent before verify: refresh the bound snapshot + expiry.
            existing.RegistrantEmail = email;
            existing.FingerprintV2 = fingerprint;
            existing.SnapshotJson = snapshot;
            existing.ExpiresAt = expiresAt;
            existing.ConsumedAt = null;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent initiate of the SAME submissionId won the unique insert — the snapshot is already
            // bound to the identical intent; the OTP challenge above is the authoritative gate. Non-fatal.
        }
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        return email[..2] + new string('*', Math.Max(0, at - 2)) + email[at..];
    }
}
