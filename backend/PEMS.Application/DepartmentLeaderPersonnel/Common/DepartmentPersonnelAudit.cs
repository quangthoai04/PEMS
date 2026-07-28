namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// Audit action names for this slice (spec §19). Kept as constants so a query over
/// <c>audit_logs.action</c> cannot silently miss rows because a handler typed the string differently.
/// </summary>
public static class DepartmentPersonnelAuditActions
{
    public const string AddPersonnel = "ADD_DEPARTMENT_PERSONNEL";
    public const string UpdatePersonnel = "UPDATE_DEPARTMENT_PERSONNEL";
    public const string UpdatePersonnelIdentity = "UPDATE_DEPARTMENT_PERSONNEL_IDENTITY";
    public const string CorrectPendingPersonnelEmail = "CORRECT_PENDING_DEPARTMENT_PERSONNEL_EMAIL";
    public const string DisablePersonnel = "DISABLE_DEPARTMENT_PERSONNEL";
    public const string EnablePersonnel = "ENABLE_DEPARTMENT_PERSONNEL";
    public const string ResendPersonnelConfirmation = "RESEND_DEPARTMENT_PERSONNEL_CONFIRMATION";
    public const string TransferLeadership = "TRANSFER_DEPARTMENT_LEADERSHIP";
}

/// <summary>
/// Helpers for what an audit row may contain (spec §19). The audit trail records WHAT changed and by
/// whom — never a raw or hashed confirmation token, never a password hash, never a JWT or refresh
/// token, and never a rendered email body.
/// </summary>
public static class DepartmentPersonnelAudit
{
    /// <summary>
    /// Masks the local-part of an address for the audit trail, keeping enough to recognise the record
    /// without storing the full address a second time. Domain is preserved because it carries the
    /// organisational meaning ("did we move someone off @fpt.edu.vn?").
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;

        var at = email.IndexOf('@');
        if (at <= 0) return "***";

        var local = email[..at];
        var domain = email[at..];

        // 1-2 char local-parts would be fully revealed by "first + last", so mask them entirely.
        var maskedLocal = local.Length <= 2
            ? new string('*', local.Length)
            : $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}";

        return maskedLocal + domain;
    }
}
