using System.Text.RegularExpressions;

namespace PEMS.Application.Common.Security;

/// <summary>
/// Central password strength policy: min 8 chars, at least one upper, lower,
/// digit and special character.
/// </summary>
public static partial class PasswordPolicy
{
    public const int MinLength = 8;

    public const string RequirementsMessage =
        "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a number and a special character.";

    public static bool IsStrong(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return false;

        return HasUpper().IsMatch(password)
               && HasLower().IsMatch(password)
               && HasDigit().IsMatch(password)
               && HasSpecial().IsMatch(password);
    }

    [GeneratedRegex("[A-Z]")] private static partial Regex HasUpper();
    [GeneratedRegex("[a-z]")] private static partial Regex HasLower();
    [GeneratedRegex("[0-9]")] private static partial Regex HasDigit();
    [GeneratedRegex("[^A-Za-z0-9]")] private static partial Regex HasSpecial();
}
