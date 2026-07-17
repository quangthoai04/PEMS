using System;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.DTOs;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Applies a SAFE-EDIT patch (plan §16.6) inside the caller's transaction on a TRACKED request loaded
/// with CampusInstances(+FormDetail) — the handler did flags/auth/editor-policy first; this service
/// re-validates every data-facing rule: FOR-UPDATE row-version guard, per-instance versions, the SAFE
/// allowlist via <c>VisitFieldClassifier</c> (fail closed), the 24h cutoff (privacy-urgent media
/// withdrawal exempt), then applies target-only, bumps form/request revisions + row versions, recomputes
/// the canonical scope/mixed/fingerprint/projection and writes field-level audit — all in one commit.
/// Never touches an amendment, a sibling instance without changes, or any approval state.
/// </summary>
public interface IVisitSafeEditService
{
    Task<VisitRequestSafeEditResponse> ApplySafeEditAsync(
        VisitRequest request, VisitRequestSafeEditDto patch, ulong actorId, DateTime now,
        CancellationToken cancellationToken);
}

/// <summary>
/// Per-campus amendment workflow (plan §16.6) — submit stores an immutable proposal (no active mutation);
/// approve applies it atomically on the FOR-UPDATE-locked amendment; reject/withdraw/expire leave the
/// active snapshot untouched. Handlers do flags/auth/scope first and own the transaction.
/// </summary>
public interface IVisitAmendmentService
{
    /// <summary>Creates the PENDING_APPROVAL amendment (diff vs the active detail; empty diff rejected).</summary>
    Task<VisitInstanceAmendment> SubmitAsync(
        VisitRequest request, VisitRequestCampus instance, VisitAmendmentProposalDto proposal,
        ulong actorId, DateTime now, CancellationToken cancellationToken);

    /// <summary>Locks (FOR UPDATE) and returns the amendment row, tracked, or null.</summary>
    Task<VisitInstanceAmendment?> LockAmendmentAsync(ulong amendmentId, CancellationToken cancellationToken);

    /// <summary>Applies the locked PENDING amendment to the active detail/members/schedule (target-only),
    /// bumps form+approval revisions and row versions, recomputes the canonical projection and writes
    /// the revision snapshot + audit. Caller (handler) commits.</summary>
    Task<VisitAmendmentDecisionResponse> ApproveAsync(
        VisitInstanceAmendment amendment, ulong actorId, string? note, DateTime now,
        CancellationToken cancellationToken);

    /// <summary>Marks the locked PENDING amendment REJECTED (reason required); active snapshot unchanged.</summary>
    Task<VisitAmendmentDecisionResponse> RejectAsync(
        VisitInstanceAmendment amendment, ulong actorId, string note, DateTime now,
        CancellationToken cancellationToken);

    /// <summary>Marks the locked PENDING amendment WITHDRAWN (requester side); active snapshot unchanged.</summary>
    Task<VisitAmendmentDecisionResponse> WithdrawAsync(
        VisitInstanceAmendment amendment, ulong actorId, DateTime now, CancellationToken cancellationToken);

    /// <summary>Expires overdue PENDING amendments (window passed or instance started). Batched, idempotent;
    /// owns its transaction. Returns the number expired.</summary>
    Task<int> ExpireDueAsync(DateTime now, int batchSize, CancellationToken cancellationToken);
}
