using System;

namespace PEMS.Application.Common;

/// <summary>
/// PEMS time policy: every PEMS-managed MySQL DATETIME column stores Vietnam wall-clock
/// (Asia/Ho_Chi_Minh, UTC+07:00, no DST) — business, audit AND security timestamps
/// (OTP/session/token expiry snapshots) alike. UTC/Unix exists only inside external
/// protocols (JWT NumericDate, OAuth expires_in/exp); convert at that boundary with
/// <see cref="FromUtc"/> / <see cref="ToUtc"/>, never by adding 7 hours by hand.
/// Anything that compares against DB columns must use Vietnam-local "now", never
/// <see cref="DateTime.UtcNow"/> — otherwise it is off by the +07:00 offset.
/// </summary>
public static class VietnamTime
{
    /// <summary>Fixed offset of Asia/Ho_Chi_Minh (no DST).</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    private static readonly TimeZoneInfo Tz = ResolveTimeZone();

    /// <summary>The Asia/Ho_Chi_Minh time zone (platform-resolved).</summary>
    public static TimeZoneInfo TimeZone => Tz;

    private static TimeZoneInfo ResolveTimeZone()
    {
        // Windows uses "SE Asia Standard Time"; Linux/macOS use the IANA id.
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { /* fall through */ }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.CreateCustomTimeZone("VN", Offset, "Vietnam", "Vietnam"); }
    }

    /// <summary>Current wall-clock time in Asia/Ho_Chi_Minh (Kind = Unspecified — matches DB DATETIME).</summary>
    public static DateTime Now() =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz), DateTimeKind.Unspecified);

    /// <summary>
    /// Converts a UTC instant (e.g. a JWT/OAuth expiry) to Vietnam wall-clock for persistence.
    /// Same instant, different representation — never lengthens or shortens a lifetime.
    /// </summary>
    public static DateTime FromUtc(DateTime utc)
    {
        var normalized = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(normalized, Tz), DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Converts a Vietnam wall-clock value (as stored in MySQL DATETIME) back to the UTC instant,
    /// for JWT NumericDate emission or external-protocol comparisons.
    /// </summary>
    public static DateTime ToUtc(DateTime vietnamWallClock)
    {
        var unspecified = DateTime.SpecifyKind(vietnamWallClock, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Tz);
    }

    /// <summary>Attaches the +07:00 offset to a Vietnam wall-clock value (API/DTO boundary).</summary>
    public static DateTimeOffset ToOffset(DateTime vietnamWallClock) =>
        new(DateTime.SpecifyKind(vietnamWallClock, DateTimeKind.Unspecified), Offset);
}
