using System;

namespace PEMS.Application.Common;

/// <summary>
/// PEMS stores DATETIME columns as Vietnam wall-clock (Asia/Ho_Chi_Minh), not UTC. Anything that
/// compares against those columns (e.g. reminder scheduled_at) must use Vietnam-local "now", never
/// <see cref="DateTime.UtcNow"/> — otherwise it is off by the +07:00 offset.
/// </summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo Tz = ResolveTimeZone();

    private static TimeZoneInfo ResolveTimeZone()
    {
        // Windows uses "SE Asia Standard Time"; Linux/macOS use the IANA id.
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch { /* fall through */ }
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam"); }
    }

    /// <summary>Current wall-clock time in Asia/Ho_Chi_Minh (Kind = Unspecified — matches DB DATETIME).</summary>
    public static DateTime Now() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz);
}
