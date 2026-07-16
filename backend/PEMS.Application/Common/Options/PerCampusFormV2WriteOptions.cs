namespace PEMS.Application.Common.Options;

/// <summary>
/// SEPARATE write feature flag for per-campus visit form v2, bound from the
/// <c>"PerCampusFormV2Write"</c> config section. Default OFF.
///
/// Gating rules (enforced by the create-v2 handler):
///   • Write OFF                → the v2 create endpoint is inert; only the v1 create flow runs, byte-identical.
///   • Write ON but read OFF     → an INVALID configuration: it would create v2 records that no read path can
///                                 surface. The create-v2 handler MUST reject this explicitly rather than write
///                                 unreadable data.
///   • Read ON and write ON      → create-v2 runs.
///
/// This is intentionally independent of <see cref="PerCampusFormV2Options"/> (the read flag) and is never
/// defaulted ON in production.
/// </summary>
public sealed class PerCampusFormV2WriteOptions
{
    public const string SectionName = "PerCampusFormV2Write";

    /// <summary>When false (default), create-v2 is disabled and the v1 create flow is unchanged.</summary>
    public bool Enabled { get; set; } = false;
}
