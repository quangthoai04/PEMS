using System;
using System.Collections.Generic;

namespace PEMS.Application.Dashboard.Queries.GetStaffCalendar;

/// <summary>
/// Nhóm màu legend trên bảng lịch dashboard Staff/Staff Leader (chốt theo yêu cầu):
///   • Vàng (NEEDS_ACTION)  — cần xử lý: đơn chưa duyệt/chưa gán người phụ trách. Chỉ có ý
///     nghĩa với Staff Leader (người có quyền xử lý); Staff thường không thấy màu này.
///   • Xanh dương (MINE)    — tôi đang là người phụ trách (host) của đơn. Ưu tiên cao nhất:
///     nếu tôi là host thì luôn tô xanh dương bất kể ai duyệt/gán.
///   • Xanh lá (PROCESSED)  — Staff Leader đã xử lý xong (duyệt/từ chối) nhưng tôi không phải
///     host của đơn đó.
///   • Xám (CANCELLED_OR_EXPIRED) — bị hủy / đã hết hạn xử lý.
///   • Tím (PERSONAL)       — lịch cá nhân tự tạo (nút +), không phải yêu cầu đến thăm.
/// </summary>
public static class StaffCalendarColorTypes
{
    public const string NeedsAction = "NEEDS_ACTION";
    public const string Mine = "MINE";
    public const string Processed = "PROCESSED";
    public const string CancelledOrExpired = "CANCELLED_OR_EXPIRED";
    public const string Personal = "PERSONAL";
    /// <summary>Chưa có host, chưa xử lý xong, và (với Staff) không có ý nghĩa hành động — trung tính.</summary>
    public const string Neutral = "NEUTRAL";
}

/// <summary>
/// Action flags do backend tính (single source of truth). Frontend CHỈ render button theo
/// các cờ này; mọi action vẫn được re-validate ở command handler tương ứng.
/// Gán host là quyết định final và không gửi email/không có bước accept-decline: Staff được
/// gán mặc nhiên là host, chỉ có thể vào "Setup đoàn khách" (canSetupDelegation).
/// </summary>
public sealed class StaffCalendarAllowedActionsDto
{
    public bool CanViewDetail { get; set; } = true;
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanAssignHost { get; set; }
    /// <summary>True khi user hiện tại là host của instance và có thể vào trang Setup đoàn khách.</summary>
    public bool CanSetupDelegation { get; set; }
}

/// <summary>Một yêu cầu đến thăm (campus visit instance) hiển thị trên bảng lịch.</summary>
public sealed class StaffCalendarItemDto
{
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string? RequestCode { get; set; }
    public string Title { get; set; } = default!;
    public string? DelegationName { get; set; }
    public string? RegistrantFullName { get; set; }
    public string? RegistrantOrganization { get; set; }
    public ulong CampusId { get; set; }
    public string CampusName { get; set; } = default!;
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public string RequestStatus { get; set; } = default!;
    public string? CampusStatus { get; set; }
    public string? VisitScope { get; set; }
    public ulong? CurrentHostUserId { get; set; }
    public string? CurrentHostName { get; set; }
    public bool IsCurrentHost { get; set; }
    public bool IsPast { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsExpired { get; set; }
    /// <summary>Nhãn trạng thái tiếng Việt hiển thị trực tiếp (backend là nguồn chuẩn).</summary>
    public string DisplayStatus { get; set; } = default!;
    /// <summary>NEEDS_ACTION | MINE | PROCESSED | CANCELLED_OR_EXPIRED | NEUTRAL.</summary>
    public string ColorType { get; set; } = StaffCalendarColorTypes.NeedsAction;
    public StaffCalendarAllowedActionsDto AllowedActions { get; set; } = new();
}

/// <summary>Lịch cá nhân tự tạo (nút + trên bảng lịch) — không phải yêu cầu đến thăm.</summary>
public sealed class StaffCalendarPersonalEventDto
{
    public ulong CalendarEventId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
}

public sealed class StaffCalendarResponse
{
    public string ViewMode { get; set; } = "office";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<StaffCalendarItemDto> Items { get; set; } = new();
    public List<StaffCalendarPersonalEventDto> PersonalEvents { get; set; } = new();
}
