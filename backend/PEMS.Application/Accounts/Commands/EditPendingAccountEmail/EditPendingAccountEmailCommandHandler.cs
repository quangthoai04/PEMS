using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Commands.EditPendingAccountEmail;

/// <summary>
/// Changes a pending account's email. Authorized to HO / the account's Staff Leader. The new email is
/// normalized, validated and checked for uniqueness; the address is updated and a fresh confirmation is
/// issued (superseding the old token — and because the old token's target email no longer matches the
/// account, it can never confirm). A neutral notice goes to the old address. No duplicate account is created.
/// </summary>
public sealed class EditPendingAccountEmailCommandHandler
    : IRequestHandler<EditPendingAccountEmailCommand, EditPendingAccountEmailResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IAccountEmailConfirmationService _confirmations;
    private readonly IEmailService _email;

    public EditPendingAccountEmailCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IAccountEmailConfirmationService confirmations, IEmailService email)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _confirmations = confirmations;
        _email = email;
    }

    public async Task<EditPendingAccountEmailResponse> Handle(
        EditPendingAccountEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);
        if (user is null) throw new NotFoundException("Tài khoản không tồn tại.");

        PendingAccountAuthorization.EnsureCanManagePending(_currentUser, user);

        if (user.Status != UserStatuses.PendingEmailConfirmation)
            throw new BusinessRuleException(
                "Chỉ có thể sửa email của tài khoản đang chờ xác nhận.", "ACCOUNT_NOT_PENDING");

        var newEmail = AccountIdentityRules.NormalizeEmail(request.NewEmail);
        if (AccountIdentityRules.ValidateEmail(newEmail) is { } emailError)
            throw new ValidationException(emailError);

        var oldEmail = user.Email;
        if (string.Equals(newEmail, oldEmail, System.StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Email mới trùng với email hiện tại.", "EMAIL_UNCHANGED");

        if (await _db.Users.AsNoTracking().AnyAsync(u => u.Email == newEmail && u.UserId != user.UserId, cancellationToken))
            throw new ConflictException(
                AccountIdentityRules.EmailAlreadyUsedMessage, AccountErrorCodes.EmailAlreadyExists);

        var now = _clock.VietnamNow;
        user.Email = newEmail;
        user.UpdatedAt = now;

        // Issue a fresh token bound to the NEW email; supersedes the old one (whose target email no longer matches).
        var rawToken = await _confirmations.IssuePendingAsync(user.UserId, newEmail, isResend: false, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var result = await _email.TrySendAsync(
            newEmail, AccountConfirmationEmail.Subject,
            AccountConfirmationEmail.BuildHtml(user.FullName, _confirmations.BuildConfirmUrl(rawToken)),
            cancellationToken);

        // Neutral notice to the previous address — best-effort, never blocks.
        try
        {
            await _email.TrySendAsync(
                oldEmail, AccountConfirmationEmail.EmailChangedNoticeSubject,
                AccountConfirmationEmail.BuildEmailChangedNoticeHtml(), cancellationToken);
        }
        catch { /* notice is best-effort */ }

        return new EditPendingAccountEmailResponse
        {
            Success = true,
            Email = newEmail,
            EmailNotificationStatus = AccountConfirmationEmail.MapStatus(result.Status),
            Message = "Đã cập nhật email và gửi lại xác nhận.",
        };
    }
}
