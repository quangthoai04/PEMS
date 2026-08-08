using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// The person an operational-contact invitation was raised FOR, as captured in
/// <c>visit_request_identity_changes.pending_snapshot_json</c> at the moment the invitation was
/// created.
///
/// <para>
/// It exists as one shared type because three writers produce this JSON — request creation, the
/// pre-decision replace, and the transfer — and two readers consume it: the accept handler, which
/// copies the details onto the campus, and the invitation email, which addresses the person by name.
/// They had drifted: the create path serialises the entity's own property names
/// (<c>operationalContactFullName</c>) while the two command handlers serialise short ones
/// (<c>fullName</c>), so a reader that knew only one shape silently saw an empty snapshot for the
/// other. <see cref="Read"/> accepts both, which is why the alias attributes below are not
/// redundant.
/// </para>
/// </summary>
public sealed class PendingContactSnapshot
{
    public string? FullName { get; set; }
    public string? Organization { get; set; }
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    // The create path's long-form spelling of the same five facts. Deserialisation is
    // case-insensitive, so one alias each covers both `operationalContactFullName` and
    // `OperationalContactFullName`.
    [JsonPropertyName("operationalContactFullName")] public string? OperationalContactFullName { get; set; }
    [JsonPropertyName("operationalContactOrganization")] public string? OperationalContactOrganization { get; set; }
    [JsonPropertyName("operationalContactJobTitle")] public string? OperationalContactJobTitle { get; set; }
    [JsonPropertyName("operationalContactPhone")] public string? OperationalContactPhone { get; set; }

    /// <summary>The invited person's name in whichever shape the snapshot was written, or null.</summary>
    public string? ResolvedFullName => Pick(FullName, OperationalContactFullName);

    /// <summary>The invited person's organization in whichever shape the snapshot was written.</summary>
    public string? ResolvedOrganization => Pick(Organization, OperationalContactOrganization);

    /// <summary>The invited person's job title in whichever shape the snapshot was written.</summary>
    public string? ResolvedJobTitle => Pick(JobTitle, OperationalContactJobTitle);

    /// <summary>The invited person's phone in whichever shape the snapshot was written.</summary>
    public string? ResolvedPhone => Pick(Phone, OperationalContactPhone);

    private static string? Pick(string? first, string? second)
        => !string.IsNullOrWhiteSpace(first) ? first
            : !string.IsNullOrWhiteSpace(second) ? second
                : null;

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Reads a stored snapshot, or null when there is nothing readable.
    ///
    /// <para>
    /// Never throws. A snapshot that cannot be parsed must not block a legitimate acceptance or drop an
    /// invitation email: the account and the invited address — the things that actually decide authority
    /// — live in their own columns and are unaffected. Callers fall back to something neutral instead.
    /// A redacted row (retention sweep nulls this column) reads the same way.
    /// </para>
    /// </summary>
    public static PendingContactSnapshot? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<PendingContactSnapshot>(json, ReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
