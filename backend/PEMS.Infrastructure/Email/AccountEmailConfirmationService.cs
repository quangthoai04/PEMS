using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Email;

/// <summary>
/// Default <see cref="IAccountEmailConfirmationService"/>. Reuses the shared SHA-256 token scheme
/// (<see cref="IEmailActionTokenService"/>) so only the hash is ever stored, and builds the confirm URL
/// from <c>App:FrontendBaseUrl</c> (no hardcoded domain). Times are Vietnam wall-clock (+07:00) per the
/// project-wide datetime convention.
/// </summary>
public sealed class AccountEmailConfirmationService : IAccountEmailConfirmationService
{
    /// <summary>Lifetime of a confirmation link. Short enough to bound a mistyped-address window.</summary>
    private const int ConfirmationExpiryHours = 24;

    /// <inheritdoc />
    public int ExpiryHours => ConfirmationExpiryHours;

    private readonly IApplicationDbContext _db;
    private readonly IEmailActionTokenService _tokens;
    private readonly IDateTimeService _clock;
    private readonly string _frontendBaseUrl;

    public AccountEmailConfirmationService(
        IApplicationDbContext db, IEmailActionTokenService tokens, IDateTimeService clock, IConfiguration configuration)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _frontendBaseUrl = (configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
    }

    public async Task<string> IssuePendingAsync(
        ulong userId, string normalizedTargetEmail, bool isResend, CancellationToken cancellationToken)
    {
        var now = _clock.VietnamNow;

        // Only one live token at a time: supersede every existing PENDING row for this user so the old
        // link stops working the instant a new one is issued.
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

        var raw = _tokens.GenerateRawToken();
        _db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = userId,
            TargetEmail = normalizedTargetEmail,
            TokenHash = _tokens.Hash(raw),
            Status = AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = now.AddHours(ConfirmationExpiryHours),
            ResendCount = isResend ? priorResend + 1 : 0,
            CreatedAt = now,
        });

        return raw;
    }

    public string BuildConfirmUrl(string rawToken)
        => $"{_frontendBaseUrl}/confirm-email?token={Uri.EscapeDataString(rawToken)}";

    public string BuildLoginUrl() => $"{_frontendBaseUrl}/login";
}
