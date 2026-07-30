using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.UnitTests.TestInfrastructure;

/// <summary>
/// Confirmation-token service that behaves like the production one: issuing supersedes every live
/// PENDING row for the user and inserts exactly one new PENDING row holding only the token's HASH.
///
/// <para>
/// A Moq stub returning a fixed string would let "the old link stops working" pass without the old row
/// ever changing status — the single most important property of these flows, and the one a stub cannot
/// observe. Rows are added to the tracked context and NOT saved, matching the real service: the caller's
/// transaction is what commits them.
/// </para>
/// </summary>
public sealed class RecordingConfirmationService : IAccountEmailConfirmationService
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;
    private int _counter;

    public RecordingConfirmationService(IApplicationDbContext db, IDateTimeService clock)
    {
        _db = db;
        _clock = clock;
    }

    public int ExpiryHours => 24;

    /// <summary>Every raw token handed out, in order — the last one is what the mail should carry.</summary>
    public System.Collections.Generic.List<string> IssuedTokens { get; } = new();

    public async Task<string> IssuePendingAsync(
        ulong userId, string normalizedTargetEmail, bool isResend, CancellationToken cancellationToken)
    {
        var now = _clock.VietnamNow;
        var live = await _db.AccountEmailConfirmations
            .Where(c => c.UserId == userId && c.Status == AccountEmailConfirmationStatuses.Pending)
            .ToListAsync(cancellationToken);

        var priorResend = 0;
        foreach (var row in live)
        {
            row.Status = AccountEmailConfirmationStatuses.Superseded;
            row.UpdatedAt = now;
            priorResend = Math.Max(priorResend, row.ResendCount);
        }

        var raw = $"raw-token-{++_counter}";
        IssuedTokens.Add(raw);

        _db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = userId,
            TargetEmail = normalizedTargetEmail,
            // Only the hash is ever stored — a raw token in the database would be a live credential
            // sitting in a table many people can read.
            TokenHash = $"hash-of-{raw}",
            Status = AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = now.AddHours(ExpiryHours),
            ResendCount = isResend ? priorResend + 1 : 0,
            CreatedAt = now,
        });

        return raw;
    }

    public string BuildConfirmUrl(string rawToken) => $"http://x/confirm-email?token={rawToken}";
    public string BuildLoginUrl() => "http://x/login";
}
