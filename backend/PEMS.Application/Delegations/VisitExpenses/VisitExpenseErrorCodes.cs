namespace PEMS.Application.Delegations.VisitExpenses;

/// <summary>
/// Stable failure codes for the expense-report endpoints.
///
/// The refusals below are things a client must be able to branch on — "there is no report yet" is a
/// normal state that deserves an empty panel, not a red toast, and telling it apart from "you may not
/// look at this" is impossible from a Vietnamese sentence. Prose stays for the human; the code is what
/// the UI reads.
/// </summary>
public static class VisitExpenseErrorCodes
{
    /// <summary>No general report exists for this instance yet (not an error — an empty state).</summary>
    public const string ReportNotFound = "EXPENSE_REPORT_NOT_FOUND";

    /// <summary>The instance is not in a lifecycle state where a report may be opened.</summary>
    public const string ReportNotInitializable = "EXPENSE_REPORT_NOT_INITIALIZABLE";

    /// <summary>Only the instance's current Host may open the general report.</summary>
    public const string ReportHostRequired = "EXPENSE_REPORT_HOST_REQUIRED";

    /// <summary>The caller has no relation to this instance's campus.</summary>
    public const string ReportReadForbidden = "EXPENSE_REPORT_READ_FORBIDDEN";
}
