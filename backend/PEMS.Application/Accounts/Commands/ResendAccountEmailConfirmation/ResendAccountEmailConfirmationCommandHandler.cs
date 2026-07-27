using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Commands.ResendAccountEmailConfirmation;

/// <summary>
/// Re-issues the confirmation link for a pending account. Authorized to HO / the account's Staff Leader.
/// Rate-limited (a per-account cooldown + a maximum number of resends) and issuing supersedes the previous
/// token, so an old link stops working and no duplicate account is ever created. The delivery outcome is
/// reported truthfully.
/// </summary>
public sealed class ResendAccountEmailConfirmationCommandHandler
    : IRequestHandler<ResendAccountEmailConfirmationCommand, ResendAccountEmailConfirmationResponse>
{
    private const int CooldownSeconds = 60;
    private const int MaxResends = 5;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IAccountEmailConfirmationService _confirmations;
    private readonly ISystemEmailDispatcher _dispatcher;

    public ResendAccountEmailConfirmationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IAccountEmailConfirmationService confirmations, ISystemEmailDispatcher dispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _confirmations = confirmations;
        _dispatcher = dispatcher;
    }

    public async Task<ResendAccountEmailConfirmationResponse> Handle(
        ResendAccountEmailConfirmationCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);
        if (user is null) throw new NotFoundException("Tài khoản không tồn tại.");

        PendingAccountAuthorization.EnsureCanManagePending(_currentUser, user);

        if (user.Status != UserStatuses.PendingEmailConfirmation)
            throw new BusinessRuleException(
                "Tài khoản không ở trạng thái chờ xác nhận email.", "ACCOUNT_NOT_PENDING");

        var now = _clock.VietnamNow;
        var latest = await _db.AccountEmailConfirmations
            .Where(c => c.UserId == user.UserId)
            .OrderByDescending(c => c.ConfirmationId)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.Status == AccountEmailConfirmationStatuses.Pending)
        {
            if (latest.CreatedAt.AddSeconds(CooldownSeconds) > now)
                throw new BusinessRuleException(
                    "Vui lòng đợi một lát trước khi gửi lại email xác nhận.", "RESEND_TOO_SOON");
            if (latest.ResendCount >= MaxResends)
                throw new BusinessRuleException(
                    "Đã đạt số lần gửi lại tối đa. Vui lòng chỉnh sửa email hoặc liên hệ quản trị.", "RESEND_LIMIT_REACHED");
        }

        var rawToken = await _confirmations.IssuePendingAsync(user.UserId, user.Email, isResend: true, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // The confirmation email states the account's role, so resolve it by id rather than relying on a
        // navigation the caller may not have loaded.
        var roleCode = await _db.Roles.AsNoTracking()
            .Where(r => r.RoleId == user.RoleId)
            .Select(r => r.RoleCode)
            .FirstOrDefaultAsync(cancellationToken) ?? RoleCodes.Staff;

        var result = await _dispatcher.SendAsync(new SystemEmailRequest(
            SystemEmailTemplates.AccountEmailConfirmation,
            new EmailRecipient(user.Email, user.FullName),
            await AccountEmailVariables.ForConfirmationAsync(
                _db, user.FullName, roleCode, user.SubRole,
                user.PrimaryCampusId, _confirmations.ExpiryHours, cancellationToken),
            TrustedBlocks: AccountEmailVariables.ConfirmationBlocks(_confirmations.BuildConfirmUrl(rawToken)),
            RelatedType: "User",
            RelatedId: user.UserId,
            SentBy: _currentUser.UserId), cancellationToken);

        var newResendCount = (latest?.Status == AccountEmailConfirmationStatuses.Pending ? latest.ResendCount : 0) + 1;

        return new ResendAccountEmailConfirmationResponse
        {
            Success = true,
            EmailNotificationStatus = result.NotificationStatus,
            ResendCount = newResendCount,
            Message = "Đã gửi lại email xác nhận.",
        };
    }
}
