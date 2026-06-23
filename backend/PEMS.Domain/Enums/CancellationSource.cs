namespace PEMS.Shared;

// Maps visit_requests.cancellation_source / visit_request_campuses.cancellation_source ENUM (SQL v8.3).
public static class CancellationSource
{
    /// <summary>Visitor tự hủy sau khi đơn đã được duyệt.</summary>
    public const string SelfService = "SELF_SERVICE";

    /// <summary>Host hủy sau khi khách xác nhận hủy qua kênh ngoài hệ thống.</summary>
    public const string ExternalConfirmation = "EXTERNAL_CONFIRMATION";

}
