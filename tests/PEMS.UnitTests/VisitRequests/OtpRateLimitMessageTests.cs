using System;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// What a rate-limited OTP request tells the person who made it.
///
/// <para>
/// All four quota rules used to answer with one sentence, in English, on a Vietnamese screen:
/// "Temporarily unable to issue another verification code." The policy had already computed both the
/// reason and the wait, and neither reached the user. These tests hold the two properties that were
/// missing: the four codes say four different things, and each one carries the wait.
/// </para>
/// </summary>
public class OtpRateLimitMessageTests
{
    private static readonly DateTime RetryAt = new(2026, 8, 4, 15, 30, 0);

    [Theory]
    [InlineData(OtpErrorCodes.ResendTooSoon)]
    [InlineData(OtpErrorCodes.StandardRateLimited)]
    [InlineData(OtpErrorCodes.RecoveryRateLimited)]
    [InlineData(OtpErrorCodes.AbsoluteRateLimited)]
    public void Every_rate_limit_code_is_answered_in_Vietnamese(string code)
    {
        var message = OtpRateLimitMessages.Describe(code, 45, RetryAt);

        Assert.DoesNotContain("Temporarily unable", message, StringComparison.OrdinalIgnoreCase);
        // Diacritics are the cheap, reliable signal that this is Vietnamese prose rather than a
        // fallback that happens to be ASCII.
        Assert.Matches("[àáâãèéêìíòóôõùúăđĩũơưạ-ỹ]", message);
    }

    /// <summary>
    /// The four causes must be distinguishable. A user who has exhausted the hourly quota and one who
    /// pressed resend twice in five seconds are told to do different things.
    /// </summary>
    [Fact]
    public void The_four_causes_produce_four_different_sentences()
    {
        var messages = new[]
        {
            OtpRateLimitMessages.Describe(OtpErrorCodes.ResendTooSoon, 45, RetryAt),
            OtpRateLimitMessages.Describe(OtpErrorCodes.StandardRateLimited, 45, RetryAt),
            OtpRateLimitMessages.Describe(OtpErrorCodes.RecoveryRateLimited, 45, RetryAt),
            OtpRateLimitMessages.Describe(OtpErrorCodes.AbsoluteRateLimited, 45, RetryAt),
        };

        Assert.Equal(messages.Length, messages.Distinct().Count());
    }

    /// <summary>A short cooldown is a number of seconds the user will sit through.</summary>
    [Fact]
    public void A_resend_too_soon_names_the_seconds()
    {
        var message = OtpRateLimitMessages.Describe(OtpErrorCodes.ResendTooSoon, 45, RetryAt);

        Assert.Contains("45 giây", message);
    }

    /// <summary>
    /// An hourly quota is a wall-clock time. "Còn 3412 giây" is arithmetic the reader should not have
    /// to do, which is why the hourly codes format <c>retryAt</c> rather than the duration.
    /// </summary>
    [Fact]
    public void An_hourly_quota_names_the_time_it_resets()
    {
        var message = OtpRateLimitMessages.Describe(OtpErrorCodes.StandardRateLimited, 3412, RetryAt);

        Assert.Contains("15:30", message);
        Assert.DoesNotContain("3412", message);
    }

    /// <summary>
    /// With no metadata at all the sentence must still be true. It may not invent a wait, and it may
    /// not fall back to naming a cause it was not given.
    /// </summary>
    [Fact]
    public void An_unknown_code_with_no_metadata_promises_nothing_specific()
    {
        var message = OtpRateLimitMessages.Describe("OTP_SOMETHING_NEW", null, null);

        Assert.Contains("Chưa thể cấp mã xác thực mới", message);
        Assert.DoesNotContain("giây", message);
    }

    /// <summary>
    /// A duration with no absolute time is rendered in minutes once it is worth rounding — the issue
    /// path can produce this when only <c>retryAfterSeconds</c> survives.
    /// </summary>
    [Fact]
    public void A_long_wait_without_an_absolute_time_is_expressed_in_minutes()
    {
        var message = OtpRateLimitMessages.Describe(OtpErrorCodes.AbsoluteRateLimited, 600, null);

        Assert.Contains("10 phút", message);
    }
}
