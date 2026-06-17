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
    private readonly int _maxAttempts;
    private readonly int _maxResendPerHour;

    public OtpService(IApplicationDbContext db, IDateTimeService clock, IConfiguration configuration)
    {
        _db = db;
        _clock = clock;
        _codeMinutes = int.TryParse(configuration["Otp:CodeMinutes"], out var m) ? m : 15;
        _maxAttempts = int.TryParse(configuration["Otp:MaxAttempts"], out var a) ? a : 5;
        _maxResendPerHour = int.TryParse(configuration["Otp:MaxResendPerHour"], out var r) ? r : 5;
    }

    public async Task<string> CreateAsync(
        User user, string purpose, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var email = user.Email.Trim().ToLowerInvariant();
        var windowStart = now.AddHours(-1);

        var recentCount = await _db.OtpTokens.AsNoTracking()
            .CountAsync(t => t.Email == email && t.Purpose == purpose && t.CreatedAt >= windowStart, cancellationToken);

        if (recentCount >= _maxResendPerHour)
            throw new BusinessRuleException("Too many requests. Please try again later.");

        // Invalidate any previous still-active codes for this email + purpose.
        var active = await _db.OtpTokens
            .Where(t => t.Email == email && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var t in active)
            t.UsedAt = now;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var rawCode = SecureTokenGenerator.GenerateNumericCode(6);
            var token = new OtpToken
            {
                OtpTokenId = Guid.NewGuid().ToString(),
                UserId = user.UserId,
                Email = email,
                TokenType = OtpTokenTypes.OtpCode,
                Purpose = purpose,
                TokenHash = HashCode(email, purpose, rawCode),
                ExpiresAt = now.AddMinutes(_codeMinutes),
                MaxAttempts = _maxAttempts,
                ResendCount = recentCount,
                IpAddress = Truncate(ipAddress, 45),
                UserAgent = Truncate(userAgent, 500),
                CreatedAt = now
            };

            _db.OtpTokens.Add(token);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return rawCode;
            }
            catch (DbUpdateException)
            {
                // Extremely rare token_hash collision — drop and regenerate.
                _db.OtpTokens.Remove(token);
            }
        }

        throw new BusinessRuleException("Unable to generate a code. Please try again.");
    }

    public async Task<OtpVerificationResult> VerifyAsync(
        string email, string purpose, string rawCode, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
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
