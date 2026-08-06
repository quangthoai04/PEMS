using System.Linq;
using System.Text.Json;
using MediatR;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.VisitRequestOtp;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.InitiateVisitRequestV2;

/// <summary>
/// STEP 1 of the PUBLIC per-campus form v2 flow. Validates the FULL v2 form (same canonical structural
/// rules as authenticated create-v2 — no v1 3-hour / 1-support rules), mints an OTP challenge via the
/// existing <c>IOtpService</c> primitive, and BINDS the canonical v2 snapshot to the submit intent so
/// <c>verify</c> builds the request from exactly what was OTP-verified. No visit request is created here.
/// Gated by BOTH v2 feature flags exactly like create-v2 (write OFF → 404).
/// </summary>
public sealed record InitiateVisitRequestV2Command(VisitRequestFormDataV2 Form)
    : IRequest<InitiateVisitRequestResponse>;

/// <summary>Stable error codes for the public v2 initiate/verify snapshot binding.</summary>
public static class InitiateVisitRequestV2ErrorCodes
{
    /// <summary>verify was called for a submission that was never initiated (or already consumed/expired).</summary>
    public const string PendingSubmissionNotFound = "PER_CAMPUS_V2_PENDING_NOT_FOUND";

    /// <summary>The verify-time form does not match the snapshot bound at initiate (client changed core content).</summary>
    public const string SubmissionFormMismatch = "PER_CAMPUS_V2_SUBMISSION_FORM_MISMATCH";
}

/// <summary>
/// Pure, deterministic (de)serialization of the bound v2 snapshot and its canonical fingerprint. Both the
/// initiate handler (writes) and the verify handler (reads) use THIS so the fingerprint compare is symmetric
/// and the request is rebuilt from the identical serialized shape. The canonical string carries PII — only the
/// SHA-256 fingerprint is ever compared/persisted alongside; the JSON is stored in a secured pending row and
/// never logged.
/// </summary>
public static class V2PendingFormSnapshot
{
    // Symmetric options: the SAME serializer round-trips the record faithfully (wall-clock DateTimes stay
    // Unspecified — no timezone shift — because there is no converter and Kind is preserved by System.Text.Json).
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(VisitRequestFormDataV2 form) => JsonSerializer.Serialize(form, Options);

    public static VisitRequestFormDataV2 Deserialize(string json)
        => JsonSerializer.Deserialize<VisitRequestFormDataV2>(json, Options)
           ?? throw new System.InvalidOperationException("Không đọc được dữ liệu biểu mẫu đã lưu.");

    /// <summary>Effective scope from the campus count — matches the create service's derivation.</summary>
    public static string EffectiveScope(VisitRequestFormDataV2 form)
        => form.CampusVisits.Count > 1 ? VisitScopes.MultiCampus : VisitScopes.SingleCampus;

    /// <summary>
    /// v2 canonical fingerprint of the whole form (registrant + per-campus core, which now carries
    /// each campus's operational-contact address — there is no request-level contact).
    /// </summary>
    public static string Fingerprint(VisitRequestFormDataV2 form)
        => VisitRequestFingerprintBuilder.BuildV2(
            form.Registrant.Email,
            EffectiveScope(form),
            form.CampusVisits.Select(c =>
                (c.CampusId, c.PlannedStartAt, c.PlannedEndAt, c.DelegationName, c.VisitType, c.VisitTypeOther,
                 (string?)c.OperationalContact.Email)));
}
