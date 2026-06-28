namespace PEMS.Application.Emails.Common;

/// <summary>
/// Shared formatting for visit_logistics_items.priority so emails/notifications signal urgency
/// consistently: URGENT → "[KHẨN]", HIGH → "[ƯU TIÊN CAO]". Pure presentation — no schema/enum change.
/// </summary>
public static class LogisticsPriorityText
{
    /// <summary>Subject/notification-title prefix (with a trailing space) for high-urgency items, else "".</summary>
    public static string SubjectPrefix(string? priority) => priority?.Trim().ToUpperInvariant() switch
    {
        "URGENT" => "[KHẨN] ",
        "HIGH" => "[ƯU TIÊN CAO] ",
        _ => string.Empty,
    };

    /// <summary>Vietnamese label for a priority code (defaults to "Trung bình").</summary>
    public static string LabelVi(string? priority) => priority?.Trim().ToUpperInvariant() switch
    {
        "URGENT" => "Khẩn cấp",
        "HIGH" => "Cao",
        "LOW" => "Thấp",
        _ => "Trung bình",
    };

    /// <summary>Prefix a subject/title once (no double-prefix if it already carries the marker).</summary>
    public static string ApplySubjectPrefix(string? priority, string subject)
    {
        var prefix = SubjectPrefix(priority);
        if (prefix.Length == 0) return subject;
        if (subject.StartsWith("[KHẨN]") || subject.StartsWith("[ƯU TIÊN CAO]")) return subject;
        return prefix + subject;
    }
}
