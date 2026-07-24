namespace PEMS.Application.Common.Options;

/// <summary>
/// DEPRECATED availability switch for the per-campus visit form surface, bound from the
/// <c>"PerCampusFormV2"</c> config section.
///
/// This is NOT a read-mode selector. The runtime is per-campus only: <c>IVisitFormReadService</c>
/// always resolves from visit_instance_form_details, and there is no other content source left to
/// select. Turning this off therefore does not revert to an older behaviour — it makes the affected
/// endpoints unavailable (404, or a <c>ReadRequired</c> conflict when writes are still enabled).
///
/// It must never be used to try to restore a request-level form, dual-read or dual-write. Because there
/// is exactly one runtime now, it DEFAULTS ON: a deployment that omits the section (Development,
/// Production) gets a working visit form rather than a dead one. Kept only so an operator can hard-disable
/// the surface in an emergency; slated for removal once no config references it.
/// </summary>
public sealed class PerCampusFormV2Options
{
    public const string SectionName = "PerCampusFormV2";

    /// <summary>
    /// Defaults true — the per-campus surface is the only runtime, so it serves unless an operator
    /// explicitly turns it off. When false, the endpoints refuse to serve and there is no fallback
    /// path behind them (the visit form is simply unavailable, never reverted to an older flow).
    /// </summary>
    public bool Enabled { get; set; } = true;
}
