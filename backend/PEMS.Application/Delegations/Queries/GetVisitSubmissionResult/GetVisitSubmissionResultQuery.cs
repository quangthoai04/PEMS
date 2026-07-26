using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitSubmissionResult;

/// <summary>
/// "Did my submission go through?" — the answer to a verify whose RESPONSE was lost (plan §10).
///
/// A dropped connection after the transaction commits is indistinguishable, from the browser, from
/// one that never reached the server. Without this the visitor's only options were to give up or to
/// submit again; the second is how duplicate delegations get created. The lookup is keyed on the
/// client-minted <c>submissionId</c> — never on email, because one person legitimately files several
/// requests and "the newest one for this address" is not the same question.
/// </summary>
public sealed record GetVisitSubmissionResultQuery(string SubmissionId)
    : IRequest<VisitSubmissionResultDto>;

public static class VisitSubmissionStates
{
    /// <summary>The request exists. <c>VisitRequestId</c>/<c>RequestCode</c> are populated.</summary>
    public const string Completed = "COMPLETED";

    /// <summary>Initiated and still open: the OTP was never completed, so nothing was created yet.</summary>
    public const string Pending = "PENDING";

    /// <summary>
    /// The submission was consumed WITHOUT producing a request — the duplicate guard refused it, or
    /// the snapshot expired after being used. Re-verifying will not help.
    /// </summary>
    public const string Failed = "FAILED";

    /// <summary>No trace of this submissionId at all. Safe to submit again.</summary>
    public const string NotFound = "NOT_FOUND";
}

/// <summary>
/// Deliberately minimal. The caller is anonymous (the public OTP flow has no session), so this
/// returns only what someone who already holds the submissionId submitted themselves — never the
/// registrant's identity, contact details or form content.
/// </summary>
public sealed record VisitSubmissionResultDto(
    string State,
    ulong? VisitRequestId,
    string? RequestCode,
    string? Status,
    string? SubmittedAt,
    int? CampusCount);
