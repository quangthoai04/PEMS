using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using System.Text.Json;

namespace PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;

public sealed class ResendVisitRequestOtpCommandHandler
    : IRequestHandler<ResendVisitRequestOtpCommand, MessageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IDateTimeService _clock;

    public ResendVisitRequestOtpCommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        IEmailService emailService,
        IDateTimeService clock)
    {
        _db           = db;
        _otpService   = otpService;
        _emailService = emailService;
        _clock        = clock;
    }

    public async Task<MessageResponse> Handle(
        ResendVisitRequestOtpCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var pending = await _db.PendingVisitRequests
            .FirstOrDefaultAsync(p => p.PendingId == request.SessionToken, cancellationToken)
            ?? throw new NotFoundException("Phiên đăng ký không tồn tại hoặc đã hết hạn.");

        if (pending.ExpiresAt <= now)
            throw new BusinessRuleException("Phiên đăng ký đã hết hạn. Vui lòng điền lại form.");

        // Extend session window on resend so the user has enough time
        pending.ExpiresAt = now.AddMinutes(10);

        var rawCode = await _otpService.CreateForEmailAsync(
            pending.Email,
            OtpPurposes.VisitRequestVerify,
            null,
            null,
            cancellationToken);

        // We need the registrant name for the email — pull it from the JSON
        var fullName = ExtractFullName(pending.FormDataJson);

        await _emailService.SendVisitRequestOtpAsync(
            pending.Email,
            fullName,
            rawCode,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return new MessageResponse("Mã xác thực mới đã được gửi tới email của bạn.");
    }

    private static string ExtractFullName(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("registerFullName", out var prop))
                return prop.GetString() ?? "Quý khách";
        }
        catch { /* ignore */ }
        return "Quý khách";
    }
}
