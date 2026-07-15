namespace PEMS.Application.Common.Options;

/// <summary>
/// Feature flag for per-campus visit form v2, bound from the <c>"PerCampusFormV2"</c> config section.
/// Default OFF: the v2 read endpoint is hidden and every v1 read path is byte-for-byte unchanged.
/// The dual-read <c>IVisitFormReadService</c> itself always resolves correctly per
/// <c>form_schema_version</c>; this flag only gates whether the v2 surface is exposed.
/// </summary>
public sealed class PerCampusFormV2Options
{
    public const string SectionName = "PerCampusFormV2";

    /// <summary>When false (default), the v2 read endpoint returns 404 and v1 behaviour is unchanged.</summary>
    public bool Enabled { get; set; } = false;
}
