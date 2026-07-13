using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PEMS.Application.Admin.Common;

/// <summary>
/// Masks credentials/secrets before audit-log values leave the backend. Two layers:
/// field-name blacklist (whole value replaced) and JSON-property scrubbing inside
/// free-text values, so secrets never reach the Admin UI or the browser devtools.
/// </summary>
public static class SensitiveDataMask
{
    public const string Mask = "***MASKED***";

    private static readonly string[] SensitiveFieldFragments =
    {
        "password", "token", "credential", "secret", "cookie", "apikey", "api_key",
        "authorization", "otp", "refresh", "private_key", "privatekey", "passwd",
        "client_secret", "service_account", "serviceaccount",
    };

    // "password": "...", refreshToken=..., etc. inside serialized values.
    private static readonly Regex JsonSensitiveProperty = new(
        "(\"(?<key>[^\"]*(password|token|credential|secret|cookie|api[_-]?key|otp|private[_-]?key|client[_-]?secret)[^\"]*)\"\\s*:\\s*)(\"[^\"]*\"|[^,}\\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsSensitiveField(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return false;
        var normalized = fieldName.Replace("-", "_").ToLowerInvariant();
        return SensitiveFieldFragments.Any(normalized.Contains);
    }

    /// <summary>Masks a single audit value: whole value when the field is sensitive,
    /// otherwise scrubs sensitive JSON properties embedded in the text.</summary>
    public static string? MaskValue(string? fieldName, string? value)
    {
        if (value is null) return null;
        if (IsSensitiveField(fieldName)) return Mask;
        return JsonSensitiveProperty.Replace(value, m => $"{m.Groups[1].Value}\"{Mask}\"");
    }
}
