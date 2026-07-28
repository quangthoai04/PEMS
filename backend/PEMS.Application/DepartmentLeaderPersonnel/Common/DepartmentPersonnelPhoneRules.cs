using System.Linq;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// Phone validation for department personnel (spec §10). Deliberately its own rule set rather than a
/// reference into the Campus master-data rules: personnel phones are a different business object with
/// their own required/optional policy, and coupling the two would make a change to campus contact
/// rules silently re-validate every staff record.
///
/// Same shape as every other identity rule in the codebase: normalize first, then validate the
/// NORMALIZED value, returning the first violated message or null.
/// </summary>
public static class DepartmentPersonnelPhoneRules
{
    public const int MaxLength = 30;
    public const int MinDigits = 8;
    public const int MaxDigits = 15;

    /// <summary>Separators tolerated inside a written phone number.</summary>
    private const string Punctuation = "+ .-()";

    public const string RequiredMessage = "Vui lòng nhập số điện thoại.";
    public const string TooLongMessage = "Số điện thoại không được vượt quá 30 ký tự.";
    public const string FormatMessage = "Số điện thoại không đúng định dạng.";
    public const string PlusPlacementMessage = "Dấu + chỉ được đặt ở đầu số điện thoại.";
    public const string DigitCountMessage = "Số điện thoại phải có từ 8 đến 15 chữ số.";

    /// <summary>Collapses whitespace runs and trims. The written form is preserved otherwise.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return string.Join(' ', value.Split(' ', System.StringSplitOptions.RemoveEmptyEntries
                                                 | System.StringSplitOptions.TrimEntries));
    }

    public static int CountDigits(string value) => value.Count(char.IsAsciiDigit);

    /// <summary>At most one '+', and only as the very first character.</summary>
    public static bool HasValidPlusPlacement(string value)
    {
        var first = value.IndexOf('+');
        return first < 0 || (first == 0 && value.LastIndexOf('+') == 0);
    }

    public static bool HasPhoneCharsOnly(string value)
        => value.Length > 0 && value.All(ch => char.IsAsciiDigit(ch) || Punctuation.Contains(ch));

    /// <summary>
    /// Validates a personnel phone. Phone is REQUIRED for department personnel (spec §10), so an empty
    /// value is rejected rather than treated as "not provided".
    /// </summary>
    public static string? Validate(string? rawValue)
    {
        var value = Normalize(rawValue);
        if (value.Length == 0) return RequiredMessage;
        if (value.Length > MaxLength) return TooLongMessage;
        if (!HasPhoneCharsOnly(value)) return FormatMessage;
        // Checked before the digit count so "84+123456789" reports the specific '+' message.
        if (!HasValidPlusPlacement(value)) return PlusPlacementMessage;

        var digits = CountDigits(value);
        return digits < MinDigits || digits > MaxDigits ? DigitCountMessage : null;
    }

    public static bool IsValid(string? rawValue) => Validate(rawValue) is null;
}
