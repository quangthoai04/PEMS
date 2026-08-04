using System;
using PEMS.Domain.Constants;

namespace PEMS.Application.Common.Security;

/// <summary>
/// What a rate-limited OTP request says to the person who made it.
///
/// <para>
/// Four distinct quota rules — a resend inside the minimum interval, the hourly standard cap, the hourly
/// human-recovery cap and the absolute hourly ceiling — all answered with the same sentence, in English,
/// on a Vietnamese screen: "Temporarily unable to issue another verification code." It named neither
/// which limit was hit nor when the user could try again, although the policy had computed both and the
/// response was already carrying them as <c>retryAfterSeconds</c> and <c>retryAt</c>.
/// </para>
/// <para>
/// This is the message of last resort, not the primary one: the client renders from
/// <c>errorCode</c> + the retry metadata so it can show a live countdown in the user's chosen language.
/// But the message travels to places the client's catalog does not — logs, integration tests, an API
/// consumer that is not the PEMS frontend — so it has to be true and readable on its own.
/// </para>
/// </summary>
public static class OtpRateLimitMessages
{
    /// <summary>
    /// The Vietnamese sentence for <paramref name="errorCode"/>, naming the wait when the policy
    /// supplied one. A short wait is expressed in seconds (a countdown the user will sit through); an
    /// hourly quota is expressed as the wall-clock time it resets, because "còn 3.412 giây" is not
    /// something anybody can act on.
    /// </summary>
    public static string Describe(string? errorCode, int? retryAfterSeconds, DateTime? retryAt)
        => errorCode switch
        {
            OtpErrorCodes.ResendTooSoon =>
                $"Bạn vừa yêu cầu mã xác thực. Vui lòng thử lại sau {Seconds(retryAfterSeconds)}.",

            OtpErrorCodes.StandardRateLimited =>
                "Bạn đã yêu cầu quá nhiều mã xác thực trong một giờ. "
                + $"Có thể thử lại {When(retryAt, retryAfterSeconds)}.",

            OtpErrorCodes.RecoveryRateLimited =>
                "Bạn đã dùng hết lượt khôi phục mã xác thực trong giờ này. "
                + $"Có thể thử lại {When(retryAt, retryAfterSeconds)}.",

            OtpErrorCodes.AbsoluteRateLimited =>
                "Tạm thời không thể cấp thêm mã xác thực cho email này. "
                + $"Có thể thử lại {When(retryAt, retryAfterSeconds)}.",

            OtpErrorCodes.RetryLater =>
                $"Bạn thao tác quá nhanh. Vui lòng chờ {Seconds(retryAfterSeconds)} trước khi thử lại.",

            // Kept generic on purpose: a code this method does not know is a code whose cause it cannot
            // state, and inventing a reason is worse than admitting there is a wait.
            _ => retryAt is not null || retryAfterSeconds is > 0
                ? $"Chưa thể cấp mã xác thực mới. Có thể thử lại {When(retryAt, retryAfterSeconds)}."
                : "Chưa thể cấp mã xác thực mới. Vui lòng thử lại sau.",
        };

    private static string Seconds(int? retryAfterSeconds)
        => retryAfterSeconds is > 0 ? $"{retryAfterSeconds} giây" : "ít phút nữa";

    /// <summary>
    /// "lúc HH:mm" when an absolute reset time is known, "sau N phút" when only a duration is, and a
    /// vague-but-honest phrase when neither is. All times are Vietnam wall-clock, like every other
    /// datetime this system emits.
    /// </summary>
    private static string When(DateTime? retryAt, int? retryAfterSeconds)
    {
        if (retryAt is { } at) return $"lúc {at:HH:mm dd/MM/yyyy}";
        if (retryAfterSeconds is > 0)
        {
            var minutes = (int)Math.Ceiling(retryAfterSeconds.Value / 60.0);
            return minutes <= 1 ? $"sau {retryAfterSeconds} giây" : $"sau {minutes} phút";
        }
        return "sau ít phút";
    }
}
