using PhoneNumbers;

namespace PEMS.Shared;

/// <summary>
/// The canonical phone rule for the whole system. It exists because the frontend validated with
/// libphonenumber while the backend only checked <c>MaximumLength(50)</c> — so anything at all,
/// including "090abc123", was accepted by a direct API call. Both sides now apply the same parse.
///
/// Accepted input:
///   • national Vietnamese form, e.g. "0912345678" (parsed with default region VN);
///   • full international form, e.g. "+84912345678" or any other country's valid number.
/// Anything else — letters, too short, too long, a number the metadata says cannot exist,
/// extensions — is rejected rather than stored.
///
/// Stored form is always E.164 ("+84912345678"), so the same person is never persisted under two
/// spellings. E.164 caps the national portion at 15 digits; the library enforces that per region.
/// </summary>
public static class PhoneNumber
{
    private const string DefaultRegion = "VN";
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    /// <summary>True when <paramref name="input"/> parses AND is a real number for its region.</summary>
    public static bool IsValid(string? input) => TryNormalize(input, out _);

    /// <summary>
    /// Parses and returns the E.164 form. Returns false (with <paramref name="e164"/> null) for any
    /// input that is not a valid number — callers must treat that as a validation failure, never as
    /// "store it as typed".
    /// </summary>
    public static bool TryNormalize(string? input, out string? e164)
    {
        e164 = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var trimmed = input.Trim();

        // A leading '+' means the caller supplied the country code; otherwise assume Vietnam.
        // Passing a region for an already-international number is harmless, but being explicit
        // keeps the two cases readable.
        var region = trimmed.StartsWith('+') ? null : DefaultRegion;

        try
        {
            var parsed = Util.Parse(trimmed, region);
            if (!Util.IsValidNumber(parsed)) return false;

            // An extension survives Parse but has no place in a stored contact number.
            if (!string.IsNullOrEmpty(parsed.Extension)) return false;

            e164 = Util.Format(parsed, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    /// <summary>
    /// E.164 for a valid number, otherwise the trimmed input unchanged. For persistence paths that
    /// run AFTER validation has already passed, where the value is known good.
    /// </summary>
    public static string NormalizeOrOriginal(string? input)
        => TryNormalize(input, out var e164) ? e164! : (input?.Trim() ?? string.Empty);
}
