using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.CancelPendingAccount;

/// <summary>
/// Cancels a pending account. Authorized to HO / the account's Staff Leader. Releases the reservation
/// (clears any campus IC-head / department-head pointer that referenced this pending user), cancels its
/// PENDING confirmation token(s), and deactivates the account — all atomically. This guarantees a Head
/// slot is never held forever by an unconfirmed account.
/// </summary>
public sealed class CancelPendingAccountCommandHandler
    : IRequestHandler<CancelPendingAccountCommand, CancelPendingAccountResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CancelPendingAccountCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CancelPendingAccountResponse> Handle(
        CancelPendingAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);
        if (user is null) throw new NotFoundException("Tài khoản không tồn tại.");

        PendingAccountAuthorization.EnsureCanManagePending(_currentUser, user);

        if (user.Status != UserStatuses.PendingEmailConfirmation)
            throw new BusinessRuleException(
                "Chỉ có thể huỷ tài khoản đang chờ xác nhận email.", "ACCOUNT_NOT_PENDING");

        var now = _clock.VietnamNow;
        var actorId = _currentUser.UserId;
        var releasedReservation = false;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            // Release any campus IC-head reservation held by this pending user.
            var campuses = await _db.Campuses
                .Where(c => c.IcHeadUserId == user.UserId).ToListAsync(cancellationToken);
            foreach (var campus in campuses)
            {
                campus.IcHeadUserId = null;
                campus.UpdatedBy = actorId;
                campus.UpdatedAt = now;
                releasedReservation = true;
            }

            // Release any department-head reservation held by this pending user.
            var departments = await _db.Departments
                .Where(d => d.HeadUserId == user.UserId).ToListAsync(cancellationToken);
            foreach (var department in departments)
            {
                department.HeadUserId = null;
                department.UpdatedBy = actorId;
                department.UpdatedAt = now;
                releasedReservation = true;
            }

            // Cancel every live confirmation token.
            var confirmations = await _db.AccountEmailConfirmations
                .Where(c => c.UserId == user.UserId && c.Status == AccountEmailConfirmationStatuses.Pending)
                .ToListAsync(cancellationToken);
            foreach (var confirmation in confirmations)
            {
                confirmation.Status = AccountEmailConfirmationStatuses.Cancelled;
                confirmation.CancelledAt = now;
                confirmation.UpdatedAt = now;
            }

            // Deactivate the never-confirmed account (cannot log in; no authority; reservation released).
            user.Status = UserStatuses.Inactive;
            user.UpdatedAt = now;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = user.PrimaryCampusId,
                Action = "CANCEL_PENDING_ACCOUNT",
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>(),
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

        return new CancelPendingAccountResponse
        {
            Success = true,
            ReleasedHeadReservation = releasedReservation,
            Message = "Đã huỷ tài khoản chờ xác nhận và giải phóng vị trí đã giữ (nếu có).",
        };
    }
}
