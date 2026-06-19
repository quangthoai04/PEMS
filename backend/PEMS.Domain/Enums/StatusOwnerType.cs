namespace PEMS.Shared;

// Maps visit_status_logs.status_owner_type ENUM (SQL v8.3).
public static class StatusOwnerType
{
    /// <summary>Log entry refers to visit_requests.status.</summary>
    public const string Request = "REQUEST";

    /// <summary>Log entry refers to visit_request_campuses.status.</summary>
    public const string CampusInstance = "CAMPUS_INSTANCE";
}
