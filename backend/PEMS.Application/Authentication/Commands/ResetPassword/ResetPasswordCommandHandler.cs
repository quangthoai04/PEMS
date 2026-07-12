using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, MessageResponse>
{
    private const string InvalidCodeMessage = "Invalid or expired reset code.";

    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionService _sessionService;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;

    public ResetPasswordCommandHandler(
        IApplicationDbContext db,
        IOtpService otpService,
        IPasswordHasher passwordHasher,
        ISessionService sessionService,
        ISecurityAuditService audit,
        IDateTimeService clock)
    {
        _db = db;
        _otpService = otpService;
        _passwordHasher = passwordHasher;
        _sessionService = sessionService;
        _audit = audit;
        _clock = clock;
    }

    public async Task<MessageResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var verification = await _otpService.VerifyAsync(
            email, OtpPurposes.ChangeSensitiveAction, request.OtpOrToken, cancellationToken);

        if (!verification.Success || verification.Token is null)
            throw new BusinessRuleException(InvalidCodeMessage);

        var user = await _db.Users
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
            throw new BusinessRuleException(InvalidCodeMessage);

        // New password must differ from the current one.
        if (!string.IsNullOrEmpty(user.PasswordHash)
            && _passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            throw new BusinessRuleException("New password must be different from the old password.");
        }

        var now = _clock.VietnamNow;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        // Schema v4.5 removed these fields
        user.UpdatedAt = now;

        // Ensure a usable local-password provider exists.
        var localProvider = user.AuthProviders.FirstOrDefault(p => p.ProviderType == ProviderTypes.LocalPassword);
        if (localProvider is null)
        {
            _db.UserAuthProviders.Add(new UserAuthProvider
            {
                UserId = user.UserId,
                ProviderType = ProviderTypes.LocalPassword,
                ProviderEmail = email,
                IsEnabled = true,
                LinkedAt = now
            });
        }
        else
        {
            localProvider.IsEnabled = true;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Invalidate every existing session after a password reset.
        await _sessionService.RevokeAllActiveSessionsAsync(user.UserId, SessionRevokeReasons.PasswordReset, null, cancellationToken);



        return new MessageResponse("Your password has been reset. Please sign in with your new password.");
    }
}
