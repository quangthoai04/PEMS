using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.EditPendingAccountEmail;

/// <summary>
/// Changes a pending account's email (and, optionally, its full name in the same breath). Authorized to
/// HO / the account's Staff Leader. The new email is normalized, validated and checked for uniqueness;
/// the address is updated, the authentication bindings are re-pointed, and a fresh confirmation token is
/// issued — which supersedes the old one, so the link mailed to the previous address stops working.
///
/// <para>
/// The mail sent to the NEW address is the SAME <c>ACCOUNT_EMAIL_CONFIRMATION</c> template the create
/// flow uses, complete with the activation button: the account has still never proven it owns an
/// address, so what the new holder needs is a way to activate, not a notice that something changed.
/// A neutral notice goes to the old address. The account stays PENDING until the holder confirms —
/// nothing here activates it.
/// </para>
/// </summary>
public sealed class EditPendingAccountEmailCommandHandler
    : IRequestHandler<EditPendingAccountEmailCommand, EditPendingAccountEmailResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IAccountEmailConfirmationService _confirmations;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IPendingAccountEmailChangeService _emailChange;

    public EditPendingAccountEmailCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IAccountEmailConfirmationService confirmations, ISystemEmailDispatcher dispatcher,
        IPendingAccountEmailChangeService emailChange)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _confirmations = confirmations;
        _dispatcher = dispatcher;
        _emailChange = emailChange;
    }

    public async Task<EditPendingAccountEmailResponse> Handle(
        EditPendingAccountEmailCommand request, CancellationToken cancellationToken)
    {
        // Role for the confirmation email's variables, AuthProviders because the address change has to
        // re-point them — both are loaded up front so the write below needs no second round trip.
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.AuthProviders)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);
        if (user is null) throw new NotFoundException("Tài khoản không tồn tại.");

        // The confirmation email states the account's role. Prefer the loaded navigation, falling back
        // to a lookup by id so a context that did not materialize it still produces a correct email —
        // and so the scope check below, which turns on what the account IS, always has a role to read.
        var roleCode = user.Role?.RoleCode
            ?? await _db.Roles.AsNoTracking()
                .Where(r => r.RoleId == user.RoleId)
                .Select(r => r.RoleCode)
                .FirstOrDefaultAsync(cancellationToken)
            ?? RoleCodes.Staff;

        PendingAccountAuthorization.EnsureCanManagePending(_currentUser, user, roleCode);

        if (user.Status != UserStatuses.PendingEmailConfirmation)
            throw new BusinessRuleException(
                "Chỉ có thể sửa email của tài khoản đang chờ xác nhận.", "ACCOUNT_NOT_PENDING");

        var now = _clock.VietnamNow;
        var actorId = _currentUser.UserId;

        // ── Everything that must be true together: the new identity, the re-pointed providers, the
        //    dead old token and the live new one. A half-applied version of this leaves an account
        //    whose only live link points at an address it no longer has.
        PreparedPendingEmailChange change;
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            // The new identity, the re-pointed providers and the token that replaces the old one are
            // all applied by the shared service — the SAME one the Staff Leader's combined role+email
            // edit uses, so neither flow can quietly skip a step the other performs.
            change = await _emailChange.PrepareAsync(user, request.NewEmail, request.FullName, cancellationToken);

            user.UpdatedAt = now;
            user.UpdatedBy = actorId;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = user.PrimaryCampusId ?? _currentUser.PrimaryCampusId,
                Action = "EDIT_PENDING_ACCOUNT_EMAIL",
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>
                {
                    // Addresses and names only. The raw token and the confirmation URL are deliberately
                    // absent: an audit row is read by more people than may activate the account.
                    new AuditLogChange
                    {
                        FieldName = "PendingAccountEmail",
                        OldValueText = JsonSerializer.Serialize(new
                        {
                            email = change.OldEmail,
                            fullName = change.OldFullName,
                        }),
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            email = change.NewEmail,
                            fullName = change.NewFullName,
                            oldConfirmationSuperseded = true,
                            newConfirmationIssued = true,
                        }),
                    },
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── Post-commit. A delivery failure must not undo a committed address change: the account is
        //    correct and still pending either way, and HO can fall back on "Gửi lại email xác nhận".
        var notificationStatus = "FAILED";
        try
        {
            var result = await _dispatcher.SendAsync(
                await PendingAccountEmailChangeMails.BuildConfirmationAsync(
                    _db, _confirmations, user.UserId, user.FullName, roleCode, user.SubRole,
                    user.PrimaryCampusId, change.NewEmail, change.RawToken, _currentUser.UserId,
                    cancellationToken),
                cancellationToken);

            notificationStatus = result.NotificationStatus;
        }
        catch
        {
            // Reported as FAILED rather than thrown: the caller needs to be told the address WAS
            // changed, which a 500 would hide.
        }

        // Neutral notice to the previous address — best-effort, never blocks, and never affects the
        // status above: the mail that decides whether this account can be activated is the one sent to
        // the NEW address.
        try
        {
            await _dispatcher.SendAsync(
                PendingAccountEmailChangeMails.BuildOldAddressNotice(
                    user.UserId, change.OldEmail, _currentUser.UserId),
                cancellationToken);
        }
        catch { /* notice is best-effort */ }

        return new EditPendingAccountEmailResponse
        {
            Success = true,
            Email = change.NewEmail,
            EmailNotificationStatus = notificationStatus,
            Message = "Đã cập nhật email và gửi lại xác nhận.",
        };
    }
}
