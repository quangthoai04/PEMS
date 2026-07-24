namespace PEMS.Application.Common.Options;

/// <summary>
/// DEPRECATED write availability flag for per-campus visit form v2, bound from the
/// <c>"PerCampusFormV2Write"</c> config section.
///
/// Gating rules (enforced by the create-v2 handler):
///   • Write OFF                → the create endpoint is inert. There is no older create flow behind it, so
///                                 this makes creating a visit request unavailable rather than reverting it.
///   • Write ON but read OFF     → an INVALID configuration: it would create records that no read path can
///                                 surface. The create handler MUST reject this explicitly rather than write
///                                 unreadable data.
///   • Read ON and write ON      → create runs.
///
/// Independent of <see cref="PerCampusFormV2Options"/> (the read flag). Because per-campus create is the
/// only create flow now, it DEFAULTS ON so a deployment that omits the section still accepts visit
/// requests. Kept only as an operator kill-switch; slated for removal alongside the read flag.
/// </summary>
public sealed class PerCampusFormV2WriteOptions
{
    public const string SectionName = "PerCampusFormV2Write";

    /// <summary>Defaults true — per-campus create is the only create flow. When false, create-v2 is
    /// inert and nothing runs behind it (creating a visit request is simply unavailable).</summary>
    public bool Enabled { get; set; } = true;
}
