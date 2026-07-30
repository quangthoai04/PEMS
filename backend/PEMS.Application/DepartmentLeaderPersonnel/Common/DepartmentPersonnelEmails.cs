namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// The delivery-outcome vocabulary this slice returns to the client (spec §12.12).
///
/// <para>
/// This class used to hold the subjects and HTML bodies of six notifications as well. They are gone: the
/// six handlers moved onto <c>ISystemEmailDispatcher</c> and render from <c>email_templates</c>
/// (<c>DEPT_PERSONNEL_ACCOUNT_DISABLED</c>, <c>DEPT_PERSONNEL_ACCOUNT_ENABLED</c>,
/// <c>DEPT_LEADERSHIP_GRANTED</c>, <c>DEPT_LEADERSHIP_HANDED_OVER</c>, and the two
/// <c>ACCOUNT_*_EMAIL_CHANGED_*</c> notices), leaving the constants and builders here with no caller at
/// all — measured before removal: zero references across <c>backend/</c> and <c>tests/</c>.
/// </para>
/// <para>
/// Dead as they were, they were still six email subjects and six HTML bodies living in code, which is the
/// one thing the standardisation set out to end: content that an operator cannot edit and that silently
/// disagrees with the catalog. Two copies of an email's wording is one copy too many even when only one
/// of them is reachable — the next person to need a notice here would have found a working-looking
/// builder and used it.
/// </para>
/// </summary>
public static class DepartmentPersonnelEmails
{
    // ── Delivery outcome vocabulary returned to the client (spec §12.12). ──
    public const string StatusSent = "SENT";
    public const string StatusPartial = "PARTIAL";
    public const string StatusFailed = "FAILED";
    public const string StatusSkipped = "SKIPPED";
    public const string StatusNotRequired = "NOT_REQUIRED";
}
