using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PEMS.Application.Campuses.Common;

/// <summary>
/// Single source of truth for validating campus master data (code / name / city / address /
/// phone / email) across UC-81 Create and UC-85 Update. The frontend mirrors these rules in
/// <c>features/campus-management/validation/campusMasterValidation.ts</c> for early feedback —
/// this class is the authoritative check and must reject any payload sent straight to the API.
/// Every rule validates the NORMALIZED value from <see cref="CampusNormalization"/>, i.e. exactly
/// what the handler eventually writes. Uniqueness is NOT checked here (needs the database) —
/// see <see cref="CampusDuplicateGuard"/>.
/// See PEMS_HO_CAMPUS_MASTER_DATA_VALIDATION_IMPLEMENTATION_SPEC §4–§9 and §15.
/// </summary>
public static class CampusMasterRules
{
    public const int CodeMinLength = 2;
    public const int CodeMaxLength = 20;
    public const int NameMinLength = 3;
    public const int NameMaxLength = 150;
    public const int CityMaxLength = 100;
    public const int AddressMinLength = 5;
    public const int AddressMaxLength = 255;
    public const int PhoneMaxLength = 30;
    public const int PhoneMinDigits = 8;
    public const int PhoneMaxDigits = 15;
    public const int EmailMaxLength = 150;
    public const int EmailLocalPartMaxLength = 64;

    /// <summary>
    /// Exact (post-lowercase) domains accepted as a campus contact email. No subdomains, no Gmail —
    /// this is an official institutional address, not a personal one (spec §9.1).
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedCampusEmailDomains =
        new HashSet<string>(StringComparer.Ordinal) { "fpt.edu.vn", "fe.edu.vn" };

    /// <summary>Separators allowed inside a campus code, but never doubled or at either end.</summary>
    private const string CodeSeparators = "-_";

    /// <summary>Punctuation tolerated in a campus name: "FPT Education (Hòa Lạc)", "D'Or & Co.".</summary>
    private const string NamePunctuation = "-.'’&(),";

    /// <summary>Punctuation tolerated in a street address: "Lô E2a-7, Đường D1", "#12/3".</summary>
    private const string AddressPunctuation = ",.-/()'’#";

    /// <summary>Characters a phone number may be written with (spec §8.1).</summary>
    private const string PhonePunctuation = "+ ().-";

    // ── Messages — kept byte-identical to the frontend copy (spec §16). ──
    public const string CodeRequiredMessage = "Vui lòng nhập mã campus.";
    public const string CodeTooShortMessage = "Mã campus phải có ít nhất 2 ký tự.";
    public const string CodeTooLongMessage = "Mã campus không được vượt quá 20 ký tự.";
    public const string CodeInvalidCharsMessage =
        "Mã campus chỉ được chứa chữ cái không dấu, chữ số, dấu gạch ngang hoặc gạch dưới.";
    public const string CodeSeparatorEdgeMessage =
        "Mã campus không được bắt đầu hoặc kết thúc bằng dấu phân cách.";
    public const string CodeConsecutiveSeparatorMessage =
        "Mã campus không được chứa các dấu phân cách liên tiếp.";
    public const string CodeAlreadyExistsMessage = "Mã campus đã tồn tại.";

    public const string NameRequiredMessage = "Vui lòng nhập tên campus.";
    public const string NameTooShortMessage = "Tên campus phải có ít nhất 3 ký tự.";
    public const string NameTooLongMessage = "Tên campus không được vượt quá 150 ký tự.";
    public const string NameNotMeaningfulMessage = "Tên campus phải chứa ít nhất một chữ cái.";
    public const string NameInvalidCharsMessage = "Tên campus chứa ký tự không hợp lệ.";
    public const string NameAlreadyExistsMessage = "Tên campus đã tồn tại.";

    public const string CityRequiredMessage = "Vui lòng chọn tỉnh/thành phố.";
    public const string CityNotAllowedMessage = "Tỉnh/thành phố được chọn không hợp lệ.";

    public const string AddressRequiredMessage = "Vui lòng nhập địa chỉ.";
    public const string AddressTooShortMessage = "Địa chỉ phải có ít nhất 5 ký tự.";
    public const string AddressTooLongMessage = "Địa chỉ không được vượt quá 255 ký tự.";
    public const string AddressNotMeaningfulMessage = "Địa chỉ phải chứa thông tin có ý nghĩa.";
    public const string AddressInvalidCharsMessage = "Địa chỉ chứa ký tự không hợp lệ.";
    public const string AddressAlreadyExistsMessage = "Địa chỉ này đã được sử dụng cho campus khác.";

    public const string PhoneRequiredMessage = "Vui lòng nhập số điện thoại.";
    public const string PhoneDigitCountMessage = "Số điện thoại phải có từ 8 đến 15 chữ số.";
    public const string PhoneFormatMessage = "Số điện thoại không đúng định dạng.";
    public const string PhonePlusPlacementMessage = "Dấu + chỉ được đặt ở đầu số điện thoại.";
    public const string PhoneTooLongMessage = "Số điện thoại không được vượt quá 30 ký tự.";
    public const string PhoneAlreadyExistsMessage = "Số điện thoại này đã được sử dụng cho campus khác.";

    public const string EmailRequiredMessage = "Vui lòng nhập email.";
    public const string EmailFormatMessage = "Email không đúng định dạng.";
    public const string EmailTooLongMessage = "Email không được vượt quá 150 ký tự.";
    public const string EmailLocalPartTooLongMessage =
        "Phần tên email trước ký tự @ không được vượt quá 64 ký tự.";
    public const string EmailPlusNotAllowedMessage =
        "Email liên hệ campus không được chứa dấu cộng (+).";
    public const string EmailDomainNotAllowedMessage =
        "Email campus phải sử dụng tên miền @fpt.edu.vn hoặc @fe.edu.vn.";
    public const string EmailAlreadyExistsMessage = "Email này đã được sử dụng cho campus khác.";

    // ────────────────────────────── Campus code (spec §4) ──────────────────────────────

    /// <summary>
    /// True when the NORMALIZED (uppercased) code holds only A–Z, 0–9 and the separators
    /// '-'/'_', with no separator at either end and no two separators in a row.
    /// </summary>
    public static bool IsValidCampusCode(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (!HasCodeCharsOnly(value)) return false;
        if (IsCodeSeparator(value[0]) || IsCodeSeparator(value[^1])) return false;
        return !HasConsecutiveCodeSeparators(value);
    }

    /// <summary>Only A–Z, 0–9, '-' and '_' — diacritics and spaces are excluded by construction.</summary>
    public static bool HasCodeCharsOnly(string value)
        => value.Length > 0 && value.All(ch =>
            (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || IsCodeSeparator(ch));

    public static bool HasConsecutiveCodeSeparators(string value)
    {
        for (var i = 1; i < value.Length; i++)
        {
            if (IsCodeSeparator(value[i]) && IsCodeSeparator(value[i - 1])) return true;
        }
        return false;
    }

    private static bool IsCodeSeparator(char ch) => CodeSeparators.Contains(ch);

    /// <summary>
    /// Campus code validation in the spec order (§15.1), excluding uniqueness. Returns the first
    /// violated message, or null when the NORMALIZED value is acceptable.
    /// </summary>
    public static string? ValidateCampusCode(string? rawValue)
    {
        var value = CampusNormalization.Code(rawValue);
        if (value.Length == 0) return CodeRequiredMessage;
        if (value.Length < CodeMinLength) return CodeTooShortMessage;
        if (value.Length > CodeMaxLength) return CodeTooLongMessage;
        if (!HasCodeCharsOnly(value)) return CodeInvalidCharsMessage;
        if (IsCodeSeparator(value[0]) || IsCodeSeparator(value[^1])) return CodeSeparatorEdgeMessage;
        return HasConsecutiveCodeSeparators(value) ? CodeConsecutiveSeparatorMessage : null;
    }

    // ────────────────────────────── Campus name (spec §5) ──────────────────────────────

    /// <summary>True when the NORMALIZED name holds at least one letter (rejects "123", "...").</summary>
    public static bool IsMeaningfulText(string value) => value.Any(char.IsLetter);

    /// <summary>
    /// True when the NORMALIZED name holds only Unicode letters/marks, digits, spaces and the
    /// allowed punctuation. HTML tags, emoji and control characters all fail here.
    /// </summary>
    public static bool HasNameCharsOnly(string value) => HasAllowedCharsOnly(value, NamePunctuation);

    public static bool IsValidCampusName(string value)
        => value.Length >= NameMinLength
            && value.Length <= NameMaxLength
            && IsMeaningfulText(value)
            && HasNameCharsOnly(value);

    /// <summary>Campus name validation in the spec order (§15.2), excluding uniqueness.</summary>
    public static string? ValidateCampusName(string? rawValue)
    {
        var value = CampusNormalization.Text(rawValue);
        if (value.Length == 0) return NameRequiredMessage;
        if (value.Length < NameMinLength) return NameTooShortMessage;
        if (value.Length > NameMaxLength) return NameTooLongMessage;
        if (!IsMeaningfulText(value)) return NameNotMeaningfulMessage;
        return HasNameCharsOnly(value) ? null : NameInvalidCharsMessage;
    }

    // ────────────────────────────── City (spec §6) ──────────────────────────────

    /// <summary>True when the value maps onto a supported province (case-insensitive).</summary>
    public static bool IsAllowedCity(string value) => CampusCities.IsAllowed(value);

    /// <summary>
    /// City validation in the spec order (§15.3). Legacy rows that still hold an unmigrated value
    /// are handled by the update handler, which only enforces the whitelist when the city actually
    /// changes (spec §6.3) — this method always enforces it.
    /// </summary>
    public static string? ValidateCampusCity(string? rawValue)
    {
        var value = CampusNormalization.City(rawValue);
        if (value.Length == 0) return CityRequiredMessage;
        return IsAllowedCity(value) ? null : CityNotAllowedMessage;
    }

    // ────────────────────────────── Address (spec §7) ──────────────────────────────

    /// <summary>
    /// True when the NORMALIZED address holds only Unicode letters/marks, digits, spaces and the
    /// allowed punctuation. Newlines and control characters are already gone after normalization.
    /// </summary>
    public static bool HasAddressCharsOnly(string value) => HasAllowedCharsOnly(value, AddressPunctuation);

    public static bool IsValidAddress(string value)
        => value.Length >= AddressMinLength
            && value.Length <= AddressMaxLength
            && IsMeaningfulText(value)
            && HasAddressCharsOnly(value);

    /// <summary>Address validation in the spec order (§15.4), excluding uniqueness.</summary>
    public static string? ValidateCampusAddress(string? rawValue)
    {
        var value = CampusNormalization.Text(rawValue);
        if (value.Length == 0) return AddressRequiredMessage;
        if (value.Length < AddressMinLength) return AddressTooShortMessage;
        if (value.Length > AddressMaxLength) return AddressTooLongMessage;
        if (!IsMeaningfulText(value)) return AddressNotMeaningfulMessage;
        return HasAddressCharsOnly(value) ? null : AddressInvalidCharsMessage;
    }

    // ────────────────────────────── Phone (spec §8) ──────────────────────────────

    /// <summary>Digits, '+', spaces, parentheses, '.' and '-' only — letters and extensions fail.</summary>
    public static bool HasPhoneCharsOnly(string value)
        => value.Length > 0 && value.All(ch => char.IsAsciiDigit(ch) || PhonePunctuation.Contains(ch));

    /// <summary>At most one '+', and only as the very first character.</summary>
    public static bool HasValidPlusPlacement(string value)
    {
        var first = value.IndexOf('+');
        return first < 0 || (first == 0 && value.LastIndexOf('+') == 0);
    }

    /// <summary>
    /// True when the canonical key is a Vietnamese number: domestic "0…" or the "+84…" form that
    /// <see cref="CampusNormalization.PhoneKey"/> folds onto "0…" (spec §8.1).
    /// </summary>
    public static bool HasVietnamesePrefix(string value)
        => CampusNormalization.PhoneKey(value).StartsWith('0');

    public static bool IsValidPhone(string value)
        => value.Length <= PhoneMaxLength
            && HasPhoneCharsOnly(value)
            && HasValidPlusPlacement(value)
            && CountDigits(value) >= PhoneMinDigits
            && CountDigits(value) <= PhoneMaxDigits
            && HasVietnamesePrefix(value);

    /// <summary>Phone validation in the spec order (§15.5), excluding uniqueness.</summary>
    public static string? ValidateCampusPhone(string? rawValue)
    {
        var value = CampusNormalization.PhoneDisplay(rawValue);
        if (value.Length == 0) return PhoneRequiredMessage;
        if (value.Length > PhoneMaxLength) return PhoneTooLongMessage;
        if (!HasPhoneCharsOnly(value)) return PhoneFormatMessage;
        // Checked before the digit count so "84+2473005588" reports the specific '+' message.
        if (!HasValidPlusPlacement(value)) return PhonePlusPlacementMessage;

        var digits = CountDigits(value);
        if (digits < PhoneMinDigits || digits > PhoneMaxDigits) return PhoneDigitCountMessage;
        return HasVietnamesePrefix(value) ? null : PhoneFormatMessage;
    }

    public static int CountDigits(string value) => value.Count(char.IsAsciiDigit);

    // ────────────────────────────── Email (spec §9) ──────────────────────────────

    /// <summary>Local-part of a NORMALIZED email, or empty when the address has no single '@'.</summary>
    public static string GetLocalPart(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 || at != email.LastIndexOf('@') ? string.Empty : email[..at];
    }

    /// <summary>Domain of a NORMALIZED email, or empty when the address has no single '@'.</summary>
    public static string GetDomain(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 || at != email.LastIndexOf('@') ? string.Empty : email[(at + 1)..];
    }

    /// <summary>
    /// Exact domain match — never <c>Contains</c> or <c>EndsWith</c>, so "abc@student.fpt.edu.vn",
    /// "abc@fpt.edu.vn.fake.com" and "abc@fakefpt.edu.vn" all fail (spec §9.5).
    /// </summary>
    public static bool HasAllowedCampusEmailDomain(string email)
        => AllowedCampusEmailDomains.Contains(GetDomain(email));

    /// <summary>Local-part present, ≤64 chars, no leading/trailing/consecutive dot, safe charset.</summary>
    public static bool HasValidLocalPart(string email)
    {
        var local = GetLocalPart(email);
        if (local.Length == 0 || local.Length > EmailLocalPartMaxLength) return false;
        if (local[0] == '.' || local[^1] == '.' || local.Contains("..")) return false;

        return local.All(ch =>
            (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-');
    }

    /// <summary>
    /// Structural check of a NORMALIZED email: exactly one '@', a valid local-part and a
    /// syntactically plausible domain. Whitelisting is a separate step so the caller can report the
    /// more specific "domain không được phép" message.
    /// </summary>
    public static bool HasValidEmailShape(string email)
    {
        if (email.Length == 0) return false;
        if (email.Any(ch => char.IsWhiteSpace(ch) || char.IsControl(ch))) return false;
        if (!HasValidLocalPart(email)) return false;

        var domain = GetDomain(email);
        if (domain.Length == 0 || domain.Length > 253) return false;
        if (domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains("..")) return false;
        if (!domain.Contains('.')) return false;

        return domain.All(ch =>
            (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '-');
    }

    public static bool IsValidCampusEmail(string value)
        => value.Length <= EmailMaxLength
            && !value.Contains('+')
            && HasValidEmailShape(value)
            && HasAllowedCampusEmailDomain(value);

    /// <summary>Email validation in the spec order (§15.6), excluding uniqueness.</summary>
    public static string? ValidateCampusEmail(string? rawValue)
    {
        var value = CampusNormalization.Email(rawValue);
        if (value.Length == 0) return EmailRequiredMessage;
        if (value.Length > EmailMaxLength) return EmailTooLongMessage;
        if (value.Contains('+')) return EmailPlusNotAllowedMessage;

        if (GetLocalPart(value).Length > EmailLocalPartMaxLength) return EmailLocalPartTooLongMessage;
        if (!HasValidEmailShape(value)) return EmailFormatMessage;
        return HasAllowedCampusEmailDomain(value) ? null : EmailDomainNotAllowedMessage;
    }

    // ────────────────────────────── Shared charset helper ──────────────────────────────

    /// <summary>
    /// True when every character is a Unicode letter, a combining mark (decomposed Vietnamese tone
    /// marks), a digit, a space, or one of <paramref name="punctuation"/>.
    /// </summary>
    private static bool HasAllowedCharsOnly(string value, string punctuation)
    {
        foreach (var ch in value)
        {
            if (char.IsLetter(ch) || char.IsDigit(ch) || ch == ' ') continue;
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (punctuation.Contains(ch)) continue;
            return false; // '<', '>', '/', emoji, control chars …
        }
        return true;
    }
}
