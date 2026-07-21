using PEMS.Application.Common.DTOs;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Builds a per-campus form v2 <see cref="VisitRequest"/> aggregate in the caller's open transaction:
/// request (form_schema_version = 2, backend-derived scope + has_mixed + fingerprint + compatibility
/// projection) + N campus instances (each routed to its campus Staff Leader coordinator) + N
/// <see cref="VisitInstanceFormDetail"/> + per-campus independent guest/support members + composite links +
/// baseline instance/request revisions + create audit + a primary-contact INITIAL_CLAIM identity record when
/// the contact email differs from the registrant email.
///
/// It FLUSHES (SaveChanges) as needed to resolve DB-generated ids for the composite member links, but it does
/// NOT begin or commit the transaction — the caller owns that (so a partial failure rolls the whole thing back).
/// </summary>
public interface IVisitRequestV2CreateService
{
    Task<VisitRequest> CreateV2Async(
        VisitRequestFormDataV2 form,
        ulong? registrantUserId,
        string createdSource,
        DateTime vietnamNow,
        CancellationToken cancellationToken = default, System.Collections.Generic.Dictionary<string, System.Action<VisitRequestCampus>>? campusInitializers = null);
}
