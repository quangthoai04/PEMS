using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using PEMS.Application.Profiles.Common;

namespace PEMS.Application.Profiles.Commands.UpdateProfile;

/// <summary>
/// UC-15 — Update my profile (text fields only). Self-service: the target user is the
/// authenticated caller, never a userId from the client.
/// <para>
/// Only the four safe fields are bound. Any other JSON property (email, role, status,
/// primaryCampusId, ...) lands in <see cref="ExtraFields"/> and causes the whole request
/// to be rejected — there is no partial update (spec §4 / TC-17).
/// </para>
/// </summary>
public sealed class UpdateProfileCommand : IRequest<ProfileResponse>
{
    public string? FullName { get; set; }

    /// <summary>"MALE" | "FEMALE" | "OTHER" | null. Validated against the allowed set in the handler.</summary>
    public string? Gender { get; set; }

    public string? Phone { get; set; }

    /// <summary>Only VISITOR may set this (spec §3.2 / §9). Sending it as a non-VISITOR is rejected.</summary>
    public string? Nationality { get; set; }

    /// <summary>Captures any disallowed/unknown field present in the payload.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }
}
