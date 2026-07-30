namespace PEMS.Application.Emails.Idempotency;

/// <summary>
/// Declares a command as one of the report/invoice sends that must not fire twice for one user action
/// (G11 / R-103).
///
/// <para>
/// Opt-in by design. The idempotency behaviour runs in the MediatR pipeline for every request, and does
/// nothing at all unless the request implements this — so preview, export, security mail, invitations,
/// the scheduler and manual compose keep exactly the semantics they had. A blanket "all POSTs are
/// idempotent" rule would have changed all of them silently.
/// </para>
/// </summary>
public interface IIdempotentEmailSend
{
    /// <summary>
    /// Stable identifier for this action, stored on the reservation. It is part of the unique key, so a
    /// client that reused one key across two different screens gets two independent reservations rather
    /// than one refusing the other.
    /// </summary>
    string OperationCode { get; }

    /// <summary>
    /// Names the business fields that make this request what it is, in a fixed order. See
    /// <see cref="EmailSendFingerprintBuilder"/> for why the request is described rather than serialised.
    /// </summary>
    void DescribeRequest(EmailSendFingerprintBuilder builder);
}

/// <summary>
/// The shape every idempotent send returns. Replay needs to reconstruct a previous success from a stored
/// message, which is only possible if the response has a known shape; all six already had exactly this
/// one, so nothing was reshaped to fit.
/// </summary>
public interface IEmailSendResult
{
    bool Success { get; set; }
    string Message { get; set; }
}

/// <summary>Operation codes. One per route, never reused.</summary>
public static class EmailSendOperations
{
    public const string HoCampusReport = "REPORT_HO_CAMPUS";
    public const string StaffLeaderPersonnelReport = "REPORT_STAFF_LEADER_PERSONNEL";
    public const string StaffLeaderDepartmentReport = "REPORT_STAFF_LEADER_DEPARTMENT";
    public const string StaffLeaderDepartmentInvoice = "INVOICE_STAFF_LEADER_DEPARTMENT";
    public const string DeptLeaderPersonnelReport = "REPORT_DEPT_LEADER_PERSONNEL";
    public const string DeptLeaderInvoiceToStaffLeader = "INVOICE_DEPT_LEADER_TO_STAFF_LEADER";

    /// <summary>
    /// Manual compose (G11-H). Added because this is the one send whose recipients the CLIENT chooses:
    /// a double-submit here posts a second human-written message to real named people, and no report id
    /// or period exists to make the duplicate recognisable. The recipient set is what identifies it.
    /// </summary>
    public const string ManualCompose = "MANUAL_COMPOSE";

    /// <summary>Reply to the original sender.</summary>
    public const string ManualReply = "MANUAL_REPLY";

    /// <summary>
    /// Reply All. A distinct code from <see cref="ManualReply"/> so that replaying a Reply key against
    /// Reply All cannot be mistaken for the same request — the two send to different people.
    /// </summary>
    public const string ManualReplyAll = "MANUAL_REPLY_ALL";

    /// <summary>The six report/invoice routes, for the tests that assert that contract.</summary>
    public static readonly string[] Reports =
    {
        HoCampusReport,
        StaffLeaderPersonnelReport,
        StaffLeaderDepartmentReport,
        StaffLeaderDepartmentInvoice,
        DeptLeaderPersonnelReport,
        DeptLeaderInvoiceToStaffLeader,
    };

    /// <summary>The manual, client-addressed sends.</summary>
    public static readonly string[] Manual = { ManualCompose, ManualReply, ManualReplyAll };

    /// <summary>Every code. The reservation table stores exactly these.</summary>
    public static readonly string[] All =
    {
        HoCampusReport,
        StaffLeaderPersonnelReport,
        StaffLeaderDepartmentReport,
        StaffLeaderDepartmentInvoice,
        DeptLeaderPersonnelReport,
        DeptLeaderInvoiceToStaffLeader,
        ManualCompose,
        ManualReply,
        ManualReplyAll,
    };
}
