using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, MessageResponse>
{
    // Always the same response so the endpoint never reveals whether an email exists.
    private const string GenericMessage = "If the email exists, reset instructions have been sent.";

    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly ISecurityAuditService _audit;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        IEmailService emailService,
        ISecurityAuditService audit,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _db = db;
        _otpService = otpService;
        _emailService = emailService;
        _audit = audit;
        _logger = logger;
    }

    public async Task<MessageResponse> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        try
        {
            var user = await _db.Users
                .Include(u => u.AuthProviders)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            // Only issue a code to accounts that actually have a local password method.
            var hasLocalPassword = user is not null
                && (!string.IsNullOrEmpty(user.PasswordHash)
                    || user.AuthProviders.Any(p => p.ProviderType == ProviderTypes.LocalPassword && p.IsEnabled));

            if (user is not null && hasLocalPassword && user.Status == UserStatuses.Active)
            {
                var code = await _otpService.CreateAsync(
                    user, OtpPurposes.ChangeSensitiveAction, request.IpAddress, request.UserAgent, cancellationToken);

                await _emailService.SendPasswordResetAsync(user.Email, user.FullName, code, cancellationToken);

                await _audit.WriteSecurityEventAsync(user.UserId, email, SecurityEventTypes.PasswordResetRequested,
                    SecuritySeverities.Low, request.IpAddress, request.UserAgent, null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Never leak failures (rate-limit, email errors, ...) — log internally, return generic.
            _logger.LogWarning(ex, "Forgot-password processing failed for {Email}.", email);
        }

        return new MessageResponse(GenericMessage);
    }
}
