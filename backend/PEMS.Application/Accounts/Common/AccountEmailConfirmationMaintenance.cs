using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Common;

public sealed record AccountEmailConfirmationMaintenanceResult(
    int TokensExpired, int AccountsCancelled, int ReservationsReleased);

/// <summary>
/// Clock-driven maintenance for pending-account email confirmations (P0 #1): expires overdue PENDING
/// tokens, then auto-cancels pending accounts that have sat unconfirmed past a grace period with no live
/// token — releasing any Head slot they reserved so a reservation is never held forever. System-initiated
/// (no interactive actor); idempotent, so re-running is a cheap no-op once nothing is overdue.
/// </summary>
public interface IAccountEmailConfirmationMaintenance
{
    Task<AccountEmailConfirmationMaintenanceResult> RunAsync(CancellationToken cancellationToken);
}

public sealed class AccountEmailConfirmationMaintenance : IAccountEmailConfirmationMaintenance
{
    /// <summary>A pending account is auto-cancelled once it is this old with no live confirmation token.</summary>
    public const int AutoCancelGraceDays = 7;

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;

    public AccountEmailConfirmationMaintenance(IApplicationDbContext db, IDateTimeService clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AccountEmailConfirmationMaintenanceResult> RunAsync(CancellationToken cancellationToken)
    {
        var now = _clock.VietnamNow;

        // 1) Expire overdue live tokens.
        var overdue = await _db.AccountEmailConfirmations
            .Where(c => c.Status == AccountEmailConfirmationStatuses.Pending && c.ExpiresAt < now)
            .ToListAsync(cancellationToken);
        foreach (var token in overdue)
        {
            token.Status = AccountEmailConfirmationStatuses.Expired;
            token.UpdatedAt = now;
        }
        if (overdue.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        // 2) Auto-cancel pending accounts older than the grace period with no live token, releasing any
        //    Head slot they reserved.
        var cutoff = now.AddDays(-AutoCancelGraceDays);
        var stale = await _db.Users
            .Where(u => u.Status == UserStatuses.PendingEmailConfirmation && u.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        var cancelled = 0;
        var released = 0;

        foreach (var user in stale)
        {
            var hasLiveToken = await _db.AccountEmailConfirmations.AnyAsync(
                c => c.UserId == user.UserId
                     && c.Status == AccountEmailConfirmationStatuses.Pending
                     && c.ExpiresAt >= now,
                cancellationToken);
            if (hasLiveToken) continue;   // still awaiting a valid confirmation — leave it

            var campuses = await _db.Campuses.Where(c => c.IcHeadUserId == user.UserId).ToListAsync(cancellationToken);
            foreach (var campus in campuses)
            {
                campus.IcHeadUserId = null;
                campus.UpdatedAt = now;
                released++;
            }

            var departments = await _db.Departments.Where(d => d.HeadUserId == user.UserId).ToListAsync(cancellationToken);
            foreach (var department in departments)
            {
                department.HeadUserId = null;
                department.UpdatedAt = now;
                released++;
            }

            var tokens = await _db.AccountEmailConfirmations
                .Where(c => c.UserId == user.UserId
                            && (c.Status == AccountEmailConfirmationStatuses.Pending
                                || c.Status == AccountEmailConfirmationStatuses.Expired))
                .ToListAsync(cancellationToken);
            foreach (var token in tokens)
            {
                token.Status = AccountEmailConfirmationStatuses.Cancelled;
                token.CancelledAt = now;
                token.UpdatedAt = now;
            }

            user.Status = UserStatuses.Inactive;
            user.UpdatedAt = now;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = null,   // system-initiated sweep
                CampusId = user.PrimaryCampusId,
                Action = "AUTO_CANCEL_PENDING_ACCOUNT",
                EntityType = "User",
                EntityId = user.UserId,
                Changes = new List<AuditLogChange>(),
                CreatedAt = now,
            });

            cancelled++;
        }

        if (cancelled > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return new AccountEmailConfirmationMaintenanceResult(overdue.Count, cancelled, released);
    }
}
