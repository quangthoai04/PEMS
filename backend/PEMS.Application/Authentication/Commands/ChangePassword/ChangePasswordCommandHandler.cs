using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Authentication.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, MessageResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;

    public ChangePasswordCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        ISecurityAuditService audit,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _clock = clock;
    }

    public async Task<MessageResponse> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new ForbiddenException();

        var user = await _db.Users
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
            throw new ForbiddenException();

        var hasExistingPassword = !string.IsNullOrEmpty(user.PasswordHash);

        // When the user already has a password, the current one must be supplied and correct.
        if (hasExistingPassword)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword)
                || !_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash!))
            {
                throw new BusinessRuleException("Current password is incorrect.");
            }

            if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash!))
                throw new BusinessRuleException("New password must be different from the old password.");
        }

        var now = _clock.UtcNow;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.MustSetPassword = false;
        user.UpdatedAt = now;

        var localProvider = user.AuthProviders.FirstOrDefault(p => p.ProviderType == ProviderTypes.LocalPassword);
        if (localProvider is null)
        {
            _db.UserAuthProviders.Add(new UserAuthProvider
            {
                AuthProviderId = Guid.NewGuid().ToString(),
                UserId = user.UserId,
                ProviderType = ProviderTypes.LocalPassword,
                ProviderEmail = user.Email,
                IsEnabled = true,
                LinkedAt = now
            });
        }
        else
        {
            localProvider.IsEnabled = true;
        }

        // Revoke all OTHER active sessions; keep the current one alive.
        var currentSessionId = _currentUser.SessionId;
        var otherSessions = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.SessionId != currentSessionId)
            .ToListAsync(cancellationToken);
        foreach (var session in otherSessions)
        {
            session.RevokedAt = now;
            session.RefreshRevokedAt = now;
            session.RevokedReason = SessionRevokeReasons.PasswordChanged;
            session.RevokedBy = userId;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteSecurityEventAsync(userId, user.Email, SecurityEventTypes.PasswordChanged,
            SecuritySeverities.Medium, request.IpAddress, request.UserAgent, null, cancellationToken);

        return new MessageResponse("Your password has been changed successfully.");
    }
}
