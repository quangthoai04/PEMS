namespace PEMS.Application.Common.Options;

/// <summary>
/// Availability switch for the per-campus visit form surface, bound from the <c>"PerCampusFormV2"</c>
/// config section.
///
/// This is NOT a read-mode selector. The runtime is per-campus only: <c>IVisitFormReadService</c>
/// always resolves from visit_instance_form_details, and there is no other content source left to
/// select. Turning this off therefore does not revert to an older behaviour — it makes the affected
/// endpoints unavailable (404, or a <c>ReadRequired</c> conflict when writes are still enabled).
///
/// It must never be used to try to restore a request-level form, dual-read or dual-write.
/// </summary>
public sealed class PerCampusFormV2Options
{
    public const string SectionName = "PerCampusFormV2";

    /// <summary>
    /// When false, the per-campus read/write endpoints refuse to serve. There is no fallback path
    /// behind them, so a deployment that needs the visit form working must leave this on.
    /// </summary>
    public bool Enabled { get; set; } = false;
}
