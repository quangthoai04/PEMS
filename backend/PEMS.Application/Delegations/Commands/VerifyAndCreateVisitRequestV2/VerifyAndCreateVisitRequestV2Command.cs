using System.Collections.Generic;
using MediatR;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

namespace PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequestV2;

/// <summary>
/// PUBLIC (unauthenticated) per-campus form v2 create — the OTP-gated sibling of the authenticated
/// <see cref="CreateVisitRequestV2Command"/>. The OTP challenge is verified against the REGISTRANT email
/// (<see cref="VisitRequestFormDataV2.Registrant"/>.Email), identified by the opaque <see cref="SessionToken"/>
/// and bound to <see cref="VisitRequestFormDataV2.SubmissionId"/> (the idempotency key). Gated by BOTH feature
/// flags exactly like the authenticated create: write OFF → 404, which makes this endpoint unavailable
/// rather than falling back to anything; write ON but read OFF → explicit reject.
///
/// Only the registrant account is provisioned here. A different primary contact (contact ≠ registrant) is
/// NEVER given an account at submit time — it stays PENDING_CONFIRMATION with an INITIAL_CLAIM (Phase D).
/// </summary>
public sealed record VerifyAndCreateVisitRequestV2Command(
    VisitRequestFormDataV2 Form,
    string OtpCode,
    string SessionToken) : IRequest<VerifyAndCreateVisitRequestV2Response>;

public sealed record VerifyAndCreateVisitRequestV2Response(
    ulong VisitRequestId,
    string RequestCode,
    string VisitScope,
    bool HasMixedCampusDetails,
    string PrimaryContactAccessStatus,
    bool ContactClaimPending,
    IReadOnlyList<CreateVisitRequestV2CampusRef> Instances,
    bool Idempotent,
    string Message,
    // ── The receipt (plan §15) ──
    // Read from the request that was just committed. The public flow has no session and no list to
    // fall back on, so this response is the ONLY thing that can tell the visitor their request
    // exists, what it is called, and where it stands.
    string Status,
    /// <summary>Vietnam wall-clock "yyyy-MM-ddTHH:mm:ss" — no offset, like every other DATETIME here.</summary>
    string SubmittedAt,
    int CampusCount);
