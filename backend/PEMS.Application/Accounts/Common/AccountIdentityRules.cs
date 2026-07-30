using System.Globalization;
using System.Text;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Single source of truth for validating the identity fields (full name / login email) of an
/// account, for EVERY flow that provisions or edits a login identity — not only HO's:
/// <list type="bullet">
///   <item>HO — Create Account, Update Basic Account Info, Replace Staff Leader (CREATE_NEW_USER);</item>
///   <item>Department Leader — Create/Update Department Personnel (the "Phòng ban của tôi" modals);</item>
///   <item>the legacy Departments/adddepartmentpersonnel provisioning path.</item>
/// </list>
/// A caller that validates a login email anywhere else is a bug: the whitelist has exactly two
/// domains, and it drifted once already when a screen kept a private copy of it.
///
/// The frontend mirrors these rules in <c>shared/validation/loginEmailValidation.ts</c> (which the
/// account-management and department-leader-personnel validators both import) for early feedback —
/// this class is the authoritative check and must reject any payload sent straight to the API.
/// See PEMS_HO_ACCOUNT_IDENTITY_VALIDATION_IMPLEMENTATION_SPEC §3/§4/§5/§9 and
/// PEMS_DEPARTMENT_LEADER_PERSONNEL_EMAIL_DOMAIN_VALIDATION_IMPLEMENTATION_SPEC §3/§4.
/// </summary>
public static class AccountIdentityRules
{
    public const int FullNameMinLength = 2;
    public const int FullNameMaxLength = 150;
    public const int EmailMaxLength = 150;
    public const int EmailLocalPartMaxLength = 64;
    public const int ReplacementReasonMinLength = 10;
    public const int ReplacementReasonMaxLength = 500;

    /// <summary>Exact (post-lowercase) domains accepted as a PEMS login email. No subdomains.</summary>
    public static readonly IReadOnlySet<string> AllowedEmailDomains =
        new HashSet<string>(StringComparer.Ordinal) { "gmail.com", "fpt.edu.vn" };

    // Punctuation tolerated inside a person's name (O'Connor, D’Arcy, Jean-Luc, J. Smith).
    private const string NamePunctuation = "-'’.";

    // ── Messages — kept identical to the frontend copy (spec §11). ──
    public const string FullNameRequiredMessage = "Vui lòng nhập họ và tên.";
    public const string FullNameTooShortMessage = "Họ và tên phải có ít nhất 2 ký tự.";
    public const string FullNameTooLongMessage = "Họ và tên không được vượt quá 150 ký tự.";
    public const string FullNameInvalidCharsMessage =
        "Họ và tên chỉ được chứa chữ cái, khoảng trắng, dấu chấm, dấu nháy đơn và dấu gạch nối.";

    public const string EmailRequiredMessage = "Vui lòng nhập email.";
    public const string EmailFormatMessage = "Email không đúng định dạng.";
    public const string EmailTooLongMessage = "Email không được vượt quá 150 ký tự.";
    public const string EmailLocalPartTooLongMessage =
        "Phần tên email trước ký tự @ không được vượt quá 64 ký tự.";
    public const string EmailPlusNotAllowedMessage =
        "Email dùng để đăng nhập không được chứa dấu cộng (+).";
    public const string EmailDomainNotAllowedMessage =
        "Chỉ chấp nhận @gmail.com và @fpt.edu.vn.";
    public const string EmailAlreadyUsedMessage = "Email này đã được sử dụng bởi một tài khoản khác.";

    public const string ReasonRequiredMessage = "Vui lòng nhập lý do thay thế.";
    public const string ReasonTooShortMessage = "Lý do thay thế phải có ít nhất 10 ký tự.";
    public const string ReasonTooLongMessage = "Lý do thay thế không được vượt quá 500 ký tự.";
    public const string ReasonNotMeaningfulMessage = "Lý do thay thế phải chứa nội dung có ý nghĩa.";

    /// <summary>
    /// Strips control characters, collapses every whitespace run into a single space and trims.
    /// Casing, diacritics and name punctuation are preserved verbatim (spec §3.1).
    /// </summary>
    public static string NormalizeFullName(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }
            // Control characters are dropped outright — they never belong in a name.
            if (char.IsControl(ch)) continue;

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(ch);
        }

        return builder.ToString();
    }

    /// <summary>Trims and lowercases. Nothing else — the local-part is never rewritten (spec §3.2).</summary>
    public static string NormalizeEmail(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Trim().ToLowerInvariant();

    /// <summary>Trims and collapses whitespace runs for a free-text reason.</summary>
    public static string NormalizeReason(string? value) => NormalizeFullName(value);

    /// <summary>
    /// True when the NORMALIZED name contains only letters, spaces and name punctuation, holds at
    /// least one letter, and has no runs of repeated punctuation ("---", "..." → false).
    /// </summary>
    public static bool IsValidFullName(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        var hasLetter = false;
        var previousWasPunctuation = false;
        foreach (var ch in value)
        {
            if (char.IsLetter(ch))
            {
                hasLetter = true;
                previousWasPunctuation = false;
                continue;
            }
            // Combining marks (Vietnamese tone marks on decomposed input) ride along with letters.
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                previousWasPunctuation = false;
                continue;
            }
            if (ch == ' ')
            {
                previousWasPunctuation = false;
                continue;
            }
            if (NamePunctuation.Contains(ch))
            {
                if (previousWasPunctuation) return false; // "--", "..", "-." are not names
                previousWasPunctuation = true;
                continue;
            }
            return false; // digits, emoji, <, >, @, / … all rejected
        }

        return hasLetter;
    }

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

    /// <summary>Exact domain match — never a suffix check, so subdomains and look-alikes fail.</summary>
    public static bool HasAllowedEmailDomain(string email) => AllowedEmailDomains.Contains(GetDomain(email));

    /// <summary>Local-part present, ≤64 chars, no leading/trailing/consecutive dot, safe charset.</summary>
    public static bool HasValidLocalPart(string email)
    {
        var local = GetLocalPart(email);
        if (local.Length == 0 || local.Length > EmailLocalPartMaxLength) return false;
        if (local[0] == '.' || local[^1] == '.' || local.Contains("..")) return false;

        foreach (var ch in local)
        {
            var ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')
                || ch == '.' || ch == '_' || ch == '-';
            if (!ok) return false;
        }
        return true;
    }

    public static bool ContainsPlusAddressing(string email) => email.Contains('+');

    /// <summary>
    /// Structural check of a NORMALIZED email: exactly one '@', a valid local-part, and a
    /// syntactically plausible domain. Domain whitelisting is a separate step so the caller can
    /// report the more specific "domain không được phép" message.
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

        foreach (var ch in domain)
        {
            var ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '-';
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>
    /// Full name validation in the spec order (§9.1). Returns the first violated message, or null
    /// when the NORMALIZED value is acceptable.
    /// </summary>
    public static string? ValidateFullName(string? rawValue)
    {
        var value = NormalizeFullName(rawValue);
        if (value.Length == 0) return FullNameRequiredMessage;
        if (value.Length < FullNameMinLength) return FullNameTooShortMessage;
        if (value.Length > FullNameMaxLength) return FullNameTooLongMessage;
        return IsValidFullName(value) ? null : FullNameInvalidCharsMessage;
    }

    /// <summary>
    /// Email validation in the spec order (§9.2), excluding uniqueness (DB-dependent — done in the
    /// handler). Returns the first violated message, or null when the NORMALIZED value is acceptable.
    /// </summary>
    public static string? ValidateEmail(string? rawValue)
    {
        var value = NormalizeEmail(rawValue);
        if (value.Length == 0) return EmailRequiredMessage;
        if (value.Length > EmailMaxLength) return EmailTooLongMessage;
        if (ContainsPlusAddressing(value)) return EmailPlusNotAllowedMessage;

        var local = GetLocalPart(value);
        if (local.Length > EmailLocalPartMaxLength) return EmailLocalPartTooLongMessage;
        if (!HasValidEmailShape(value)) return EmailFormatMessage;
        return HasAllowedEmailDomain(value) ? null : EmailDomainNotAllowedMessage;
    }

    /// <summary>Replacement reason: required, 10–500 chars and not just punctuation (spec §6.3.3).</summary>
    public static string? ValidateReplacementReason(string? rawValue)
    {
        var value = NormalizeReason(rawValue);
        if (value.Length == 0) return ReasonRequiredMessage;
        if (value.Length < ReplacementReasonMinLength) return ReasonTooShortMessage;
        if (value.Length > ReplacementReasonMaxLength) return ReasonTooLongMessage;
        return value.Any(char.IsLetterOrDigit) ? null : ReasonNotMeaningfulMessage;
    }
}
