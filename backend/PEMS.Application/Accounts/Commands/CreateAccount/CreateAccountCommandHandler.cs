using System.Text.Json;
using Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, CreateAccountResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeService _clock;
    private readonly AuthOptions _options;

    public CreateAccountCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IDateTimeService clock,
        AuthOptions options)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _options = options;
    }

    public async Task<CreateAccountResponse> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var actorId = _currentUser.UserId;
        var actorCampus = _currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUser.RoleCode);

        if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException("An account with this email already exists.");

        var shape = await AccountProvisioningRules.ResolveAsync(
            _db, request.RoleCode, request.SubRole, request.PrimaryCampusId, request.DepartmentId,
            privileged, actorCampus, cancellationToken);

        var now = _clock.UtcNow;
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = Clean(request.Phone),
            Gender = Clean(request.Gender),
            Nationality = Clean(request.Nationality),
            StudentCode = shape.RoleCode == RoleCodes.Student ? Clean(request.StudentCode) : null,
            RoleId = shape.RoleId,
            SubRole = shape.SubRole,
            PrimaryCampusId = shape.PrimaryCampusId,
            DepartmentId = shape.DepartmentId,
            Status = UserStatuses.Active,
            CreatedVia = CreatedViaValues.ManualCreated,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        var passwordSet = false;
        if (!string.IsNullOrEmpty(request.Password))
        {
            if (!_options.PasswordLoginEnabled)
                throw new BusinessRuleException(
                    "Temporary passwords are disabled in this environment. The user signs in via SSO/FEID.");
            if (!PasswordPolicy.IsStrong(request.Password))
                throw new ValidationException(PasswordPolicy.RequirementsMessage);

            user.PasswordHash = _passwordHasher.Hash(request.Password);
            user.AuthProviders.Add(new UserAuthProvider
            {
                // AuthProviderId is DB-generated; UserId is set via the navigation on save.
                ProviderType = ProviderTypes.LocalPassword,
                ProviderEmail = email,
                IsEnabled = true,
                LinkedAt = now,
            });
            passwordSet = true;
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        // user.UserId is now populated by the database (BIGINT AUTO_INCREMENT).
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = shape.PrimaryCampusId,
            Action = "CREATE_ACCOUNT",
            EntityType = "User",
            EntityId = user.UserId,
            NewValuesJson = JsonSerializer.Serialize(new
            {
                email,
                roleCode = shape.RoleCode,
                subRole = shape.SubRole,
                campusId = shape.PrimaryCampusId,
                departmentId = shape.DepartmentId,
            }),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new CreateAccountResponse
        {
            UserId = user.UserId,
            Email = email,
            RoleCode = shape.RoleCode,
            PrimaryCampusId = shape.PrimaryCampusId,
            PasswordSet = passwordSet,
        };
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
