namespace PEMS.Shared;

// Maps visit_request_campuses.host_assignment_source ENUM (SQL v8.3).
public static class HostAssignmentSource
{
    /// <summary>HO duyệt liên cơ sở → hệ thống tự gán Staff Leader của campus.</summary>
    public const string AutoStaffLeader = "AUTO_STAFF_LEADER";

    /// <summary>Staff Leader duyệt đơn một cơ sở và chọn host.</summary>
    public const string ManualApproval = "MANUAL_APPROVAL";

    /// <summary>Host được chuyển sau đó qua chức năng Transfer Host.</summary>
    public const string Transferred = "TRANSFERRED";
}
