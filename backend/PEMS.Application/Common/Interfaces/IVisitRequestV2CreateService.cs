using PEMS.Application.Common.DTOs;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// One campus's reception-host arrangement as the create service should persist it: already
/// authorized, already resolved (SELF has been turned into the caller's own id), and carrying the
/// person who authorized it. It never names a CURRENT host — a proposal only becomes an assignment
/// when the confirmation gate opens and <c>IProposedHostActivationService</c> revalidates it.
/// </summary>
public sealed record CampusHostProposalSeed(
    string Mode,
    ulong? ProposedHostUserId,
    ulong? ProposedByUserId)
{
    /// <summary>The default for every campus nobody named a host for — including every external submit.</summary>
    public static readonly CampusHostProposalSeed WaitForLater =
        new(HostSelectionModes.WaitForLater, null, null);
}

/// <summary>
/// Builds a per-campus form <see cref="VisitRequest"/> aggregate in the caller's open transaction:
/// request (identity, lifecycle, plus backend-derived scope + has_mixed + fingerprint — facts about the
/// campus set, never campus content) + N campus instances (each routed to its campus Staff Leader
/// coordinator, each carrying its own operational contact and reception-host arrangement) + N
/// <see cref="VisitInstanceFormDetail"/> + per-campus independent guest/support members + composite links +
/// baseline instance/request revisions + create audit + one INITIAL_CONFIRMATION invitation per campus
/// whose operational contact is somebody other than the registrant.
///
/// It FLUSHES (SaveChanges) as needed to resolve DB-generated ids for the composite member links, but it does
/// NOT begin or commit the transaction — the caller owns that (so a partial failure rolls the whole thing back).
/// </summary>
public interface IVisitRequestV2CreateService
{
    /// <param name="allowShortNoticeCreate">
    /// Exempts every campus in this form from the 72h registration floor
    /// (<see cref="PEMS.Domain.Policies.VisitMutationPolicy.MinScheduleLeadHours"/>) — never from the
    /// absolute "must be in the future" guard, which every caller is held to regardless. Defaults to
    /// <c>false</c> so every EXISTING caller (public/OTP create, every integration test) keeps the 72h
    /// floor unless it explicitly opts in. Only
    /// <see cref="PEMS.Application.Delegations.Commands.CreateVisitRequestV2.CreateVisitRequestV2CommandHandler"/>
    /// may pass <c>true</c>, and only after it has proven the actor is an authenticated internal
    /// Staff/Staff Leader registering THEMSELF — see the short-notice implementation plan.
    /// </param>
    Task<VisitRequest> CreateV2Async(
        VisitRequestFormDataV2 form,
        ulong? registrantUserId,
        string createdSource,
        DateTime vietnamNow,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, CampusHostProposalSeed>? hostProposals = null,
        bool allowShortNoticeCreate = false);
}
