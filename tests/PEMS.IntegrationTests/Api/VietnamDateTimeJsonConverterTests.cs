using System.Text.Json;
using PEMS.Api.Serialization;

namespace PEMS.IntegrationTests.Api;

/// <summary>
/// API timestamp contract (§8.4): every DateTime serializes as ISO 8601 with the explicit
/// +07:00 offset; reads accept both offset-bearing and bare Vietnam wall-clock strings.
/// Pure unit tests — no database needed. Lives here because the converter is in PEMS.Api.
/// </summary>
public class VietnamDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new VietnamDateTimeJsonConverter());
        options.Converters.Add(new VietnamNullableDateTimeJsonConverter());
        return options;
    }

    private sealed record Payload(DateTime At, DateTime? MaybeAt);

    /// <summary>Reads back the emitted `at` string (the encoder may escape '+' as + — same value).</summary>
    private static string EmittedAt(Payload payload)
    {
        var json = JsonSerializer.Serialize(payload, Options);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("at").GetString()!;
    }

    [Fact]
    public void Write_UnspecifiedWallClock_Emits_Plus7_Offset()
    {
        var at = EmittedAt(new Payload(new DateTime(2026, 7, 2, 20, 0, 0), null));
        Assert.Equal("2026-07-02T20:00:00+07:00", at);
    }

    [Fact]
    public void Write_UtcKind_Converts_To_Same_Instant_In_VN()
    {
        // Protocol leftovers (e.g. the JWT expiry DTO) are Kind=Utc — the emitted
        // +07:00 string must be the SAME instant, not a mislabeled wall-clock.
        var at = EmittedAt(new Payload(new DateTime(2026, 7, 2, 13, 0, 0, DateTimeKind.Utc), null));
        Assert.Equal("2026-07-02T20:00:00+07:00", at);
    }

    [Theory]
    [InlineData("\"2026-07-02T20:00:00+07:00\"")] // canonical API form
    [InlineData("\"2026-07-02T13:00:00Z\"")]      // UTC form of the same instant
    [InlineData("\"2026-07-02T20:00\"")]          // bare datetime-local form (VN wall-clock)
    [InlineData("\"2026-07-02T20:00:00\"")]       // bare with seconds
    public void Read_All_Forms_Yield_Same_VN_WallClock(string json)
    {
        var value = JsonSerializer.Deserialize<DateTime>(json, Options);

        Assert.Equal(new DateTime(2026, 7, 2, 20, 0, 0), value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void Read_Negative_Offset_Converts_To_VN()
    {
        // 08:00 -05:00 == 13:00Z == 20:00 +07:00.
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-07-02T08:00:00-05:00\"", Options);
        Assert.Equal(new DateTime(2026, 7, 2, 20, 0, 0), value);
    }

    [Fact]
    public void RoundTrip_Does_Not_Drift()
    {
        // AC-03: serialize → deserialize repeatedly must never shift the wall-clock.
        var original = new DateTime(2026, 7, 15, 9, 0, 0);
        var once = JsonSerializer.Deserialize<DateTime>(JsonSerializer.Serialize(original, Options), Options);
        var twice = JsonSerializer.Deserialize<DateTime>(JsonSerializer.Serialize(once, Options), Options);

        Assert.Equal(original, once);
        Assert.Equal(original, twice);
    }

    [Fact]
    public void Read_DateOnly_Is_VN_Midnight()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-07-02\"", Options);
        Assert.Equal(new DateTime(2026, 7, 2, 0, 0, 0), value);
    }
}
