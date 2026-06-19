namespace PEMS.Shared;

// Maps visit_requests.visit_scope ENUM (SQL v8.3).
public static class VisitScope
{
    /// <summary>Staff Leader duyệt request tổng.</summary>
    public const string SingleCampus = "SINGLE_CAMPUS";

    /// <summary>HO duyệt request tổng.</summary>
    public const string MultiCampus = "MULTI_CAMPUS";
}
