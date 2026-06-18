using System.Text.Json;
using MediatR;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequest;

public sealed class InitiateVisitRequestCommandHandler
    : IRequestHandler<InitiateVisitRequestCommand, InitiateVisitRequestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IDateTimeService _clock;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false
    };

    public InitiateVisitRequestCommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        IEmailService emailService,
        IDateTimeService clock)
    {
        _db          = db;
        _otpService  = otpService;
        _emailService = emailService;
        _clock       = clock;
    }

    public async Task<InitiateVisitRequestResponse> Handle(
        InitiateVisitRequestCommand request, CancellationToken cancellationToken)
    {
        var now   = _clock.UtcNow;
        var email = request.RegisterEmail.Trim().ToLowerInvariant();

        // 1. Serialise form data so it can be reconstructed after OTP passes
        var formData = new PendingVisitRequestFormData(
            request.RegisterFullName,
            request.RegisterNationality,
            request.RegisterOrganization,
            request.RegisterJobTitle,
            request.RegisterPhone,
            email,
            request.DelegationName,
            request.VisitScope,
            request.VisitSlots,
            request.Purpose,
            request.WorkingContent,
            request.Visitors,
            request.SupportTeam,
            request.ContactPoint,
            request.IsContactSelf,
            request.Language,
            request.Vehicle,
            request.Notes);

        var pending = new PendingVisitRequest
        {
            PendingId    = Guid.NewGuid().ToString(),
            Email        = email,
            FormDataJson = JsonSerializer.Serialize(formData, _json),
            ExpiresAt    = now.AddMinutes(10),   // window covers OTP delivery + entry
            IpAddress    = null,
            CreatedAt    = now
        };

        _db.PendingVisitRequests.Add(pending);
        await _db.SaveChangesAsync(cancellationToken);

        // 2. Generate OTP (5-minute code, email-only — user may not exist yet)
        var rawCode = await _otpService.CreateForEmailAsync(
            email,
            OtpPurposes.VisitRequestVerify,
            null,
            null,
            cancellationToken);

        // 3. Send OTP email
        await _emailService.SendVisitRequestOtpAsync(
            email,
            request.RegisterFullName,
            rawCode,
            cancellationToken);

        return new InitiateVisitRequestResponse(
            SessionToken: pending.PendingId,
            Message:      "Mã xác thực đã được gửi tới email của bạn. Vui lòng kiểm tra hộp thư.",
            MaskedEmail:  MaskEmail(email));
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        return email[..2] + new string('*', Math.Max(0, at - 2)) + email[at..];
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
