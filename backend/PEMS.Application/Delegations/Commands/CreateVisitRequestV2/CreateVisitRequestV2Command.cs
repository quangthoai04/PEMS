using System.Collections.Generic;
using MediatR;
using PEMS.Application.Common.DTOs;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// Authenticated per-campus form v2 create. The submitting (registrant) account is the current user; the
/// request-level primary contact may be a different VISITOR (INITIAL_CLAIM path). Gated by BOTH feature flags:
/// write OFF → 404 (endpoint inert); write ON but read OFF → explicit reject (would create unreadable v2).
/// Idempotent by <see cref="VisitRequestFormDataV2.SubmissionId"/>.
/// </summary>
public sealed record CreateVisitRequestV2Command(VisitRequestFormDataV2 Form)
    : IRequest<CreateVisitRequestV2Response>;

public sealed record CreateVisitRequestV2CampusRef(ulong VisitInstanceId, ulong CampusId, string Status);

public sealed record CreateVisitRequestV2Response(
    ulong VisitRequestId,
    string RequestCode,
    string VisitScope,
    bool HasMixedCampusDetails,
    string PrimaryContactAccessStatus,
    bool ContactClaimPending,
    IReadOnlyList<CreateVisitRequestV2CampusRef> Instances,
    bool Idempotent);

/// <summary>Stable error codes for the create-v2 flag gate.</summary>
public static class CreateVisitRequestV2ErrorCodes
{
    public const string WriteDisabled = "PER_CAMPUS_V2_WRITE_DISABLED";
    public const string ReadRequired = "PER_CAMPUS_V2_READ_REQUIRED";
}
