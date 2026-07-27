using System.Collections.Generic;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, MessageResponse>
{
    // Always the same response so the endpoint never reveals whether an email exists.
    private const string GenericMessage = "If the email exists, reset instructions have been sent.";

    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly ISecurityAuditService _audit;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        ISystemEmailDispatcher dispatcher,
        ISecurityAuditService audit,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _db = db;
        _otpService = otpService;
        _dispatcher = dispatcher;
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
                // CreateAsync persists the token itself, so the code exists before anything is emailed:
                // a send failure below leaves a usable token rather than an account that was told to
                // expect a code that was never issued.
                var code = await _otpService.CreateAsync(
                    user, OtpPurposes.ChangeSensitiveAction, request.IpAddress, request.UserAgent, cancellationToken);

                await _dispatcher.SendAsync(new SystemEmailRequest(
                    SystemEmailTemplates.AuthPasswordResetOtp,
                    new EmailRecipient(user.Email, user.FullName),
                    // The lifetime is read from the OTP service rather than written here, so the email
                    // states the one the token was actually given.
                    OtpEmailVariables.For(user.FullName, code, _otpService.CodeMinutes),
                    RelatedType: "User",
                    RelatedId: user.UserId), cancellationToken);
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
