using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// Issues and verifies one-time codes. Codes are stored hashed (scoped by
/// email+purpose so values are effectively unique). Enforces resend + attempt limits.
/// </summary>
public sealed class OtpService : IOtpService
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;
    private readonly int _codeMinutes;
    private readonly int _visitRequestCodeMinutes;
    private readonly int _maxAttempts;
    private readonly int _maxResendPerHour;

    public OtpService(IApplicationDbContext db, IDateTimeService clock, IConfiguration configuration)
    {
        _db                      = db;
        _clock                   = clock;
        _codeMinutes             = int.TryParse(configuration["Otp:CodeMinutes"], out var m) ? m : 15;
        _visitRequestCodeMinutes = int.TryParse(configuration["Otp:VisitRequestCodeMinutes"], out var v) ? v : 5;
        _maxAttempts             = int.TryParse(configuration["Otp:MaxAttempts"], out var a) ? a : 5;
        _maxResendPerHour        = int.TryParse(configuration["Otp:MaxResendPerHour"], out var r) ? r : 5;
    }

    public Task<string> CreateAsync(
        User user, string purpose, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        => CreateCoreAsync(user.UserId, user.Email, purpose, _codeMinutes, ipAddress, userAgent, cancellationToken);

    public Task<string> CreateForEmailAsync(
        string email, string purpose, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        => CreateCoreAsync(null, email, purpose, _visitRequestCodeMinutes, ipAddress, userAgent, cancellationToken);

    private async Task<string> CreateCoreAsync(
        string? userId, string email, string purpose, int expiryMinutes,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var now             = _clock.UtcNow;
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var windowStart     = now.AddHours(-1);

        var recentCount = await _db.OtpTokens.AsNoTracking()
            .CountAsync(t => t.Email == normalizedEmail && t.Purpose == purpose && t.CreatedAt >= windowStart, cancellationToken);

        if (recentCount >= _maxResendPerHour)
            throw new BusinessRuleException("Bạn đã yêu cầu quá nhiều lần. Vui lòng thử lại sau.");

        // Old tokens are NOT invalidated here. VerifyAsync always picks the LATEST
        // non-used token (OrderByDescending CreatedAt), so old codes naturally stop
        // working once a newer token exists. Removing the invalidation step avoids
        // a race condition where concurrent resend requests mark each other's tokens
        // as used before the user has a chance to verify.

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var rawCode = SecureTokenGenerator.GenerateNumericCode(6);
            var token   = new OtpToken
            {
                OtpTokenId  = Guid.NewGuid().ToString(),
                UserId      = userId,
                Email       = normalizedEmail,
                TokenType   = OtpTokenTypes.OtpCode,
                Purpose     = purpose,
                TokenHash   = HashCode(normalizedEmail, purpose, rawCode),
                ExpiresAt   = now.AddMinutes(expiryMinutes),
                MaxAttempts = _maxAttempts,
                ResendCount = recentCount,
                IpAddress   = Truncate(ipAddress, 45),
                UserAgent   = Truncate(userAgent, 500),
                CreatedAt   = now
            };

            _db.OtpTokens.Add(token);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return rawCode;
            }
            catch (DbUpdateException)
            {
                _db.OtpTokens.Remove(token);
            }
        }

        throw new BusinessRuleException("Không thể tạo mã xác thực. Vui lòng thử lại.");
    }

    public async Task<OtpVerificationResult> VerifyAsync(
        string email, string purpose, string rawCode, CancellationToken cancellationToken = default)
    {
        var now             = _clock.UtcNow;
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();

        var token = await _db.OtpTokens
            .Where(t => t.Email == normalizedEmail && t.Purpose == purpose && t.UsedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null)
            return new OtpVerificationResult(false, "no_active_token", null);

        if (token.ExpiresAt <= now)
            return new OtpVerificationResult(false, "expired", null);

        if (token.AttemptCount >= token.MaxAttempts)
            return new OtpVerificationResult(false, "max_attempts", null);

        token.AttemptCount += 1;

        var matches = !string.IsNullOrEmpty(rawCode)
                      && string.Equals(token.TokenHash, HashCode(normalizedEmail, purpose, rawCode), StringComparison.Ordinal);

        if (matches)
        {
            token.UsedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new OtpVerificationResult(true, null, token);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new OtpVerificationResult(false, "mismatch", null);
    }

    private static string HashCode(string email, string purpose, string code)
        => SecureTokenGenerator.Hash($"{email}:{purpose}:{code}");

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
