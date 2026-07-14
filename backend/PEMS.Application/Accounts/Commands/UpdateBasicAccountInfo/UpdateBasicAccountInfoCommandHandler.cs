using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.UpdateBasicAccountInfo;

/// <summary>
/// HO basic-info edit handler (spec §9/§10). Updates ONLY full name + login email of another HO or
/// a Staff Leader. Role/sub-role/campus/department/status are loaded from the database and never
/// changed. When the email changes: the auth providers are re-pointed and their SSO/FEID subject is
/// reset so the account re-links on next login, email verification is cleared, and every active
/// session is revoked so the old email can no longer sign in. This is the final authorization gate —
/// a direct API call from a non-HO caller (or on an out-of-scope / self / LOCKED target) is rejected.
/// </summary>
public sealed class UpdateBasicAccountInfoCommandHandler
    : IRequestHandler<UpdateBasicAccountInfoCommand, UpdateBasicAccountInfoResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRoleAccessPolicy _accessPolicy;
    private readonly ISessionService _sessionService;
    private readonly IDateTimeService _clock;
    private readonly IEmailService _emailService;

    public UpdateBasicAccountInfoCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IRoleAccessPolicy accessPolicy,
        ISessionService sessionService,
        IDateTimeService clock,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _sessionService = sessionService;
        _clock = clock;
        _emailService = emailService;
    }

    public async Task<UpdateBasicAccountInfoResponse> Handle(
        UpdateBasicAccountInfoCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUser.UserId;

        // 1. Caller authenticated. 2/3. Caller is HO with account-management permission.
        if (!_currentUser.IsAuthenticated || actorId is null)
            throw new AuthBusinessException(AccountErrorCodes.AccountListForbidden,
                "Bạn cần đăng nhập để thực hiện thao tác này.", 401);

        if (_currentUser.RoleCode != RoleCodes.Ho || !_accessPolicy.CanAccessAccountManagement(_currentUser))
            throw new ForbiddenException("Chỉ Head Office mới được chỉnh sửa thông tin tài khoản này.");

        // 4. Target exists (role loaded from DB — never trusted from the client).
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        // 5. Not the caller. 6. Not LOCKED. 7. In HO scope (HO or STAFF/LEADER).
        if (user.UserId == actorId.Value)
            throw new ForbiddenException("Bạn không thể chỉnh sửa thông tin tài khoản của chính mình.");

        if (user.Status == UserStatuses.Locked)
            throw new BusinessRuleException(
                "Tài khoản đang bị khóa vì lý do bảo mật và không thể chỉnh sửa tại đây.");

        var targetInScope = user.Role.RoleCode == RoleCodes.Ho
            || (user.Role.RoleCode == RoleCodes.Staff && user.SubRole == UserSubRoles.Leader);
        if (!targetInScope)
            throw new ForbiddenException("Tài khoản này nằm ngoài phạm vi quản lý của HO.");

        // 9. Only full name / email are accepted (the command carries nothing else).
        var newFullName = (request.FullName ?? string.Empty).Trim();
        if (newFullName.Length == 0)
            throw new ValidationException("Vui lòng nhập họ và tên.");
        if (newFullName.Length > 150)
            throw new ValidationException("Họ và tên không được vượt quá 150 ký tự.");

        var newEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (newEmail.Length == 0)
            throw new ValidationException("Vui lòng nhập email.");
        if (newEmail.Length > 150)
            throw new ValidationException("Email không được vượt quá 150 ký tự.");
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(newEmail))
            throw new ValidationException("Email không đúng định dạng.");

        var oldFullName = user.FullName;
        var oldEmail = user.Email;
        var emailChanged = !string.Equals(oldEmail, newEmail, StringComparison.Ordinal);

        // Email uniqueness (excludes the target itself so re-saving the same email is a no-op).
        if (emailChanged)
        {
            var emailTaken = await _db.Users.AnyAsync(
                u => u.Email == newEmail && u.UserId != user.UserId, cancellationToken);
            if (emailTaken)
                throw new ConflictException(
                    "Email này đã được sử dụng bởi một tài khoản khác.", AccountErrorCodes.EmailAlreadyExists);
        }

        var now = _clock.VietnamNow;
        user.FullName = newFullName;

        if (emailChanged)
        {
            user.Email = newEmail;
            // Re-point auth providers to the new email; reset SSO/FEID subject so the account
            // re-links on next login, and clear verification so the new email is re-verified.
            foreach (var provider in user.AuthProviders)
            {
                provider.ProviderEmail = newEmail;
                if (provider.ProviderType == ProviderTypes.GoogleSso || provider.ProviderType == ProviderTypes.FeId)
                    provider.ProviderSubject = null;
            }
            user.EmailVerifiedAt = null;
        }

        user.UpdatedAt = now;
        user.UpdatedBy = actorId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = user.PrimaryCampusId ?? _currentUser.PrimaryCampusId,
            Action = "UPDATE_ACCOUNT_BASIC_INFO",
            EntityType = "User",
            EntityId = user.UserId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "BasicInfo",
                    OldValueText = JsonSerializer.Serialize(new { fullName = oldFullName, email = oldEmail }),
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        fullName = newFullName,
                        email = newEmail,
                        authenticationRelinkRequired = emailChanged,
                    }),
                }
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Email change forces every active session out (the old email can no longer sign in).
        var revoked = 0;
        if (emailChanged)
        {
            revoked = await _sessionService.RevokeAllActiveSessionsAsync(
                user.UserId, SessionRevokeReasons.AccountEmailChanged, actorId, cancellationToken);
        }

        // Notifications/emails run after commit; a failure must not roll back the saved account.
        var emailStatus = "NOT_REQUIRED";
        if (emailChanged)
            emailStatus = await SendEmailChangeMailsAsync(user.FullName, oldEmail, newEmail, cancellationToken);

        return new UpdateBasicAccountInfoResponse
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            EmailChanged = emailChanged,
            RevokedSessions = revoked,
            EmailNotificationStatus = emailStatus,
            Message = emailChanged
                ? "Cập nhật thông tin tài khoản thành công. Email đăng nhập đã thay đổi và các phiên hiện tại đã bị thu hồi."
                : "Cập nhật thông tin tài khoản thành công.",
        };
    }

    /// <summary>
    /// Sends a change notice to the OLD email and the updated account info to the NEW email.
    /// Returns SENT (both ok), PARTIAL (only one ok) or FAILED (both failed). Never throws.
    /// </summary>
    private async Task<string> SendEmailChangeMailsAsync(
        string fullName, string oldEmail, string newEmail, CancellationToken cancellationToken)
    {
        var name = System.Net.WebUtility.HtmlEncode(fullName);
        var oldEnc = System.Net.WebUtility.HtmlEncode(oldEmail);
        var newEnc = System.Net.WebUtility.HtmlEncode(newEmail);

        var oldOk = false;
        try
        {
            var html =
                $"<p>Xin chào {name},</p>" +
                $"<p>Email đăng nhập của tài khoản PEMS này vừa được thay đổi từ <strong>{oldEnc}</strong> sang <strong>{newEnc}</strong>.</p>" +
                "<p>Địa chỉ email cũ sẽ không còn dùng để đăng nhập được nữa. Nếu bạn không thực hiện thay đổi này, " +
                "vui lòng liên hệ ngay Head Office hoặc quản trị hệ thống.</p>" +
                "<p>Trân trọng,<br/>PEMS System</p>";
            await _emailService.SendAsync(oldEmail, "Email đăng nhập PEMS của bạn đã được thay đổi", html, cancellationToken);
            oldOk = true;
        }
        catch { /* non-fatal */ }

        var newOk = false;
        try
        {
            var html =
                $"<p>Xin chào {name},</p>" +
                $"<p>Email đăng nhập của tài khoản PEMS của bạn hiện là <strong>{newEnc}</strong>.</p>" +
                "<p>Bạn vui lòng đăng nhập bằng địa chỉ email này thông qua SSO/Google/FEID. " +
                "Hệ thống sẽ liên kết lại đăng nhập cho bạn trong lần đăng nhập tiếp theo.</p>" +
                "<p>Trân trọng,<br/>PEMS System</p>";
            await _emailService.SendAsync(newEmail, "Thông tin đăng nhập tài khoản PEMS của bạn", html, cancellationToken);
            newOk = true;
        }
        catch { /* non-fatal */ }

        return (oldOk, newOk) switch
        {
            (true, true) => "SENT",
            (false, false) => "FAILED",
            _ => "PARTIAL",
        };
    }
}
