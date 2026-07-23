namespace PEMS.Application.Common.Options;

/// <summary>
/// SEPARATE write feature flag for per-campus visit form v2, bound from the
/// <c>"PerCampusFormV2Write"</c> config section. Default OFF.
///
/// Gating rules (enforced by the create-v2 handler):
///   • Write OFF                → the create endpoint is inert. There is no older create flow behind it, so
///                                 this makes creating a visit request unavailable rather than reverting it.
///   • Write ON but read OFF     → an INVALID configuration: it would create records that no read path can
///                                 surface. The create handler MUST reject this explicitly rather than write
///                                 unreadable data.
///   • Read ON and write ON      → create runs.
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
