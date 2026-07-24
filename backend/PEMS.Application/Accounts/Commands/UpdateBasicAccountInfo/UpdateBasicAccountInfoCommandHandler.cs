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
/// changed. When the email changes: local-password providers are re-pointed at the new address, the
/// SSO/FEID provider rows are removed so the account re-links on next login, email verification is
/// cleared, and every active session is revoked so the old email can no longer sign in. This is the final authorization gate —
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
        // Campus/Department are included for the notification email only — the account snapshot it
        // shows must come from the database, never from the request.
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.AuthProviders)
            .Include(u => u.PrimaryCampus)
            .Include(u => u.Department)
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

        // 9. Only full name / email are accepted (the command carries nothing else). Both go through
        //    the shared identity rules on the NORMALIZED value, so a direct API call is held to the
        //    exact same standard as the modal.
        var newFullName = AccountIdentityRules.NormalizeFullName(request.FullName);
        if (AccountIdentityRules.ValidateFullName(newFullName) is { } nameError)
            throw new ValidationException(nameError);

        var newEmail = AccountIdentityRules.NormalizeEmail(request.Email);
        if (AccountIdentityRules.ValidateEmail(newEmail) is { } emailError)
            throw new ValidationException(emailError);

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
                    AccountIdentityRules.EmailAlreadyUsedMessage, AccountErrorCodes.EmailAlreadyExists);
        }

        var now = _clock.VietnamNow;
        user.FullName = newFullName;

        if (emailChanged)
        {
            user.Email = newEmail;

            // Unlink SSO/FEID by DELETING the row, not by blanking provider_subject in place: the
            // subject identifies the OLD external identity, and the database rejects a subject-less
            // SSO/FEID row outright (trigger trg_auth_providers_validate_bu — "SSO/FEID
            // provider_subject is required"). Removing the row is also what "re-link on next login"
            // actually means: LoginviaSSOCommandHandler.EnsureGoogleProviderLinkedAsync creates a
            // fresh row when the user has none, and the delete frees the old subject from
            // uq_auth_provider_subject. Sessions referencing the row survive via
            // fk_sessions_auth_provider ON DELETE SET NULL, and are revoked below regardless.
            var externalProviders = user.AuthProviders
                .Where(p => p.ProviderType == ProviderTypes.GoogleSso || p.ProviderType == ProviderTypes.FeId)
                .ToList();
            _db.UserAuthProviders.RemoveRange(externalProviders);

            // Local-password logins stay linked — only the address they point at changes.
            foreach (var provider in user.AuthProviders)
            {
                if (provider.ProviderType == ProviderTypes.GoogleSso || provider.ProviderType == ProviderTypes.FeId)
                    continue;
                provider.ProviderEmail = newEmail;
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
            emailStatus = await SendEmailChangeMailsAsync(user, oldEmail, newEmail, cancellationToken);

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
    /// Sends a removal notice to the OLD email and the updated account snapshot to the NEW email.
    /// Returns SENT (both ok), PARTIAL (only one ok) or FAILED (both failed). Never throws — the two
    /// sends are independently guarded so a failure on the first still attempts the second.
    /// </summary>
    private async Task<string> SendEmailChangeMailsAsync(
        User user, string oldEmail, string newEmail, CancellationToken cancellationToken)
    {
        var oldOk = await TrySendAsync(
            oldEmail, OldAddressSubject, BuildOldAddressBody(), cancellationToken);

        var newOk = await TrySendAsync(
            newEmail, NewAddressSubject, BuildNewAddressBody(user, newEmail), cancellationToken);

        return (oldOk, newOk) switch
        {
            (true, true) => "SENT",
            (false, false) => "FAILED",
            _ => "PARTIAL",
        };
    }

    private const string OldAddressSubject = "Địa chỉ email này đã được gỡ khỏi một tài khoản PEMS";
    private const string NewAddressSubject = "Email đăng nhập tài khoản PEMS của bạn đã được cập nhật";

    private async Task<bool> TrySendAsync(
        string toEmail, string subject, string html, CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendAsync(toEmail, subject, html, cancellationToken);
            return true;
        }
        catch
        {
            return false; // non-fatal: the account is already saved and must not be rolled back
        }
    }

    /// <summary>
    /// Notice for the address that was just unlinked. Deliberately anonymous: the old address may
    /// have been a typo belonging to an uninvolved person, so it reveals nothing about the account —
    /// no holder name, no new address, no role/campus/department, no internal id, and no
    /// "changed from A to B" phrasing that would leak the new address to a stranger.
    /// </summary>
    private static string BuildOldAddressBody() =>
        "<p>Xin chào,</p>" +
        "<p>Địa chỉ email này không còn được sử dụng để đăng nhập vào tài khoản PEMS đã liên kết trước đó.</p>" +
        "<p>Mọi phiên đăng nhập đang hoạt động bằng địa chỉ email này đã được thu hồi. " +
        "Bạn không cần thực hiện thêm thao tác nào.</p>" +
        "<p>Nếu bạn không biết về tài khoản này hoặc cho rằng đây là sự nhầm lẫn, vui lòng liên hệ " +
        "Head Office, Staff Leader phụ trách hoặc bộ phận quản trị hệ thống PEMS để được hỗ trợ.</p>" +
        "<p>Trân trọng,<br/>PEMS System</p>";

    /// <summary>
    /// Account snapshot for the new address. Every value is read from the database entity (never from
    /// the request) and HTML-encoded. The department line is omitted entirely when the account has no
    /// department, so the email never renders an empty row or the text "null".
    /// </summary>
    private static string BuildNewAddressBody(User user, string newEmail)
    {
        var nameEnc = HtmlEncode(user.FullName);
        var emailEnc = HtmlEncode(newEmail);
        var roleEnc = HtmlEncode(AccountRoleDisplayNames.Resolve(user.Role.RoleCode, user.SubRole));

        // A missing campus must not break the email — show a dash instead.
        var campusName = user.PrimaryCampus?.Name;
        var campusEnc = HtmlEncode(string.IsNullOrWhiteSpace(campusName) ? "—" : campusName);

        var departmentName = user.Department?.Name;
        var departmentLine = string.IsNullOrWhiteSpace(departmentName)
            ? string.Empty
            : $"<li>Phòng ban: <strong>{HtmlEncode(departmentName)}</strong></li>";

        return
            $"<p>Xin chào {nameEnc},</p>" +
            "<p>Email đăng nhập của tài khoản PEMS của bạn đã được cập nhật thành công.</p>" +
            "<p><strong>Thông tin tài khoản hiện tại:</strong></p>" +
            "<ul>" +
            $"<li>Email đăng nhập: <strong>{emailEnc}</strong></li>" +
            $"<li>Vai trò: <strong>{roleEnc}</strong></li>" +
            $"<li>Cơ sở: <strong>{campusEnc}</strong></li>" +
            departmentLine +
            "</ul>" +
            "<p>Từ bây giờ, bạn vui lòng sử dụng địa chỉ email trên để đăng nhập vào Internal Portal " +
            "của PEMS thông qua SSO, Google hoặc FEID.</p>" +
            "<p>Các phiên đăng nhập trước đó đã được thu hồi. Trong lần đăng nhập tiếp theo bằng email mới, " +
            "hệ thống sẽ liên kết lại phương thức đăng nhập với tài khoản của bạn.</p>" +
            "<p>Nếu bạn không mong đợi thay đổi này hoặc nhận thấy thông tin tài khoản chưa chính xác, " +
            "vui lòng liên hệ Head Office, Staff Leader phụ trách hoặc bộ phận quản trị hệ thống PEMS " +
            "để được hỗ trợ.</p>" +
            "<p>Trân trọng,<br/>PEMS System</p>";
    }

    /// <summary>
    /// Escapes the five characters that could break out of HTML element text (&amp; &lt; &gt; and both
    /// quotes) while leaving Vietnamese diacritics intact — every dynamic value here lands in element
    /// content (inside &lt;p&gt;/&lt;li&gt;/&lt;strong&gt;), so this is enough to stop injection, and
    /// it keeps the email readable rather than turning every accented letter into a numeric entity the
    /// way WebUtility.HtmlEncode would.
    /// </summary>
    private static string HtmlEncode(string? value) => (value ?? string.Empty)
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");
}
