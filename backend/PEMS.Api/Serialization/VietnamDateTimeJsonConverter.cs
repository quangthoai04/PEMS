using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PEMS.Application.Common;

namespace PEMS.Api.Serialization;

/// <summary>
/// API contract for PEMS timestamps: always ISO 8601 WITH the Vietnam offset, e.g.
/// <c>2026-07-02T20:00:00+07:00</c> — never the ambiguous <c>2026-07-02T20:00:00</c>.
///
/// Write: Kind.Unspecified/Local values are Vietnam wall-clock (that is what every
/// PEMS-managed MySQL DATETIME stores) and get <c>+07:00</c> attached as-is; Kind.Utc
/// values (protocol boundary leftovers such as the JWT expiry DTO) are converted to
/// Vietnam wall-clock first, so the emitted instant is always correct.
///
/// Read: strings carrying an offset/Z are converted to the equivalent Vietnam wall-clock;
/// bare strings (e.g. <c>datetime-local</c> form values) are taken verbatim as Vietnam
/// wall-clock. Result Kind is always Unspecified, ready for MySQL DATETIME persistence.
/// </summary>
public sealed class VietnamDateTimeJsonConverter : JsonConverter<DateTime>
{
    private const string OffsetFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'+07:00'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new JsonException("Empty datetime value.");

        return Parse(text);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var wallClock = value.Kind == DateTimeKind.Utc ? VietnamTime.FromUtc(value) : value;
        writer.WriteStringValue(wallClock.ToString(OffsetFormat, CultureInfo.InvariantCulture));
    }

    internal static DateTime Parse(string text)
    {
        // Offset-aware input (…Z / …+07:00 / …-05:00) → same instant as Vietnam wall-clock.
        if (DateTimeOffset.TryParse(
                text, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var dto)
            && HasExplicitOffset(text))
        {
            return DateTime.SpecifyKind(dto.ToOffset(VietnamTime.Offset).DateTime, DateTimeKind.Unspecified);
        }

        // Bare wall-clock input ("2026-07-02T20:00[:00]") → already Vietnam wall-clock.
        if (DateTime.TryParse(
                text, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var plain))
        {
            return DateTime.SpecifyKind(plain, DateTimeKind.Unspecified);
        }

        throw new JsonException($"Invalid datetime value '{text}'.");
    }

    private static bool HasExplicitOffset(string text)
    {
        var t = text.Trim();
        if (t.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            return true;

        // An offset like +07:00 / -0500 sits AFTER the time part ('T' separator present).
        var timeSeparator = t.IndexOf('T');
        if (timeSeparator < 0) timeSeparator = t.IndexOf(' ');
        if (timeSeparator < 0) return false;

        var timePart = t[(timeSeparator + 1)..];
        return timePart.Contains('+') || timePart.Contains('-');
    }
}

/// <summary>Nullable companion of <see cref="VietnamDateTimeJsonConverter"/>.</summary>
public sealed class VietnamNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    private static readonly VietnamDateTimeJsonConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var text = reader.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : VietnamDateTimeJsonConverter.Parse(text);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}
