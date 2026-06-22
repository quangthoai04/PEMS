namespace PEMS.Shared;

// Maps visit_requests.cancellation_source / visit_request_campuses.cancellation_source ENUM (SQL v8.3).
// SQL defines exactly two values; do NOT add INTERNAL_DECISION unless the SQL ENUM adds it.
public static class CancellationSource
{
    /// <summary>Visitor tự hủy sau khi đơn đã được duyệt.</summary>
    public const string SelfService = "SELF_SERVICE";

    /// <summary>Host hủy sau khi khách xác nhận hủy qua kênh ngoài hệ thống.</summary>
    public const string ExternalConfirmation = "EXTERNAL_CONFIRMATION";

    /// <summary>HO/Staff Leader operational cancellation.</summary>
    public const string InternalDecision = "INTERNAL_DECISION";
}
