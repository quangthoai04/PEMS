using PEMS.Application.Common;

namespace PEMS.UnitTests.SharedKernel;

/// <summary>
/// PEMS time policy foundation: MySQL DATETIME stores Vietnam wall-clock; UTC/Unix lives only
/// at the protocol boundary. These tests pin the conversion helpers to that contract.
/// </summary>
public class VietnamTimeTests
{
    [Fact]
    public void Now_Returns_UnspecifiedKind()
    {
        // Kind must be Unspecified so Pomelo persists the literal wall-clock without re-conversion.
        Assert.Equal(DateTimeKind.Unspecified, VietnamTime.Now().Kind);
    }

    [Fact]
    public void Now_Is_UtcPlus7_Regardless_Of_Host_TimeZone()
    {
        var utc = DateTime.UtcNow;
        var vn = VietnamTime.Now();
        var diff = vn - utc;

        // Allow a small window for the two clock reads.
        Assert.InRange(diff.TotalMinutes, 7 * 60 - 1, 7 * 60 + 1);
    }

    [Fact]
    public void FromUtc_Converts_13UTC_To_20VN()
    {
        var utc = new DateTime(2026, 7, 2, 13, 0, 0, DateTimeKind.Utc);
        var vn = VietnamTime.FromUtc(utc);

        Assert.Equal(new DateTime(2026, 7, 2, 20, 0, 0), vn);
        Assert.Equal(DateTimeKind.Unspecified, vn.Kind);
    }

    [Fact]
    public void FromUtc_Crosses_Year_Boundary()
    {
        // UTC 18:30 on 31/12 → Vietnam 01:30 on 01/01 next year.
        var utc = new DateTime(2026, 12, 31, 18, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2027, 1, 1, 1, 30, 0), VietnamTime.FromUtc(utc));
    }

    [Fact]
    public void ToUtc_Then_FromUtc_RoundTrips_WallClock()
    {
        var wallClock = new DateTime(2026, 7, 2, 20, 0, 0);
        var roundTripped = VietnamTime.FromUtc(VietnamTime.ToUtc(wallClock));
        Assert.Equal(wallClock, roundTripped);
    }

    [Fact]
    public void ToUtc_Preserves_Unix_Instant()
    {
        // AC-06: converting VN wall-clock back to protocol time must not move the instant.
        var wallClock = new DateTime(2026, 7, 2, 20, 0, 0);
        var utc = VietnamTime.ToUtc(wallClock);

        Assert.Equal(new DateTime(2026, 7, 2, 13, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(
            new DateTimeOffset(2026, 7, 2, 20, 0, 0, TimeSpan.FromHours(7)).ToUnixTimeSeconds(),
            new DateTimeOffset(utc).ToUnixTimeSeconds());
    }

    [Fact]
    public void OAuth_ExpiresIn_3600_Is_One_Hour_In_VietnamWallClock()
    {
        // expires_in is a duration: snapshotting it through the VN conversion must keep 1 hour,
        // never 8 hours or -6 hours.
        var issuedUtc = new DateTime(2026, 7, 2, 13, 0, 0, DateTimeKind.Utc);
        var expiresUtc = issuedUtc.AddSeconds(3600);

        var issuedVn = VietnamTime.FromUtc(issuedUtc);
        var expiresVn = VietnamTime.FromUtc(expiresUtc);

        Assert.Equal(TimeSpan.FromHours(1), expiresVn - issuedVn);
    }

    [Fact]
    public void ToOffset_Attaches_Plus7_Without_Moving_WallClock()
    {
        var wallClock = new DateTime(2026, 7, 2, 20, 0, 0);
        var offset = VietnamTime.ToOffset(wallClock);

        Assert.Equal(TimeSpan.FromHours(7), offset.Offset);
        Assert.Equal(wallClock, offset.DateTime);
        Assert.Equal("2026-07-02T20:00:00+07:00", offset.ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
    }
}
