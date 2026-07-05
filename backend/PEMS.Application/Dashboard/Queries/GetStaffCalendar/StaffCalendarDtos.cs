using System;
using System.Collections.Generic;

namespace PEMS.Application.Dashboard.Queries.GetStaffCalendar;

/// <summary>Nhóm màu legend trên bảng lịch dashboard Staff/Staff Leader.</summary>
public static class StaffCalendarColorTypes
{
    public const string New = "NEW";                                  // Mới / Chờ xử lý
    public const string NeedsAction = "NEEDS_ACTION";                 // Cần tôi xử lý
    public const string Processed = "PROCESSED";                      // Đã xử lý / đang vận hành
    public const string CancelledOrExpired = "CANCELLED_OR_EXPIRED";  // Bị hủy / từ chối / hết hạn
    public const string Mine = "MINE";                                // Tôi là host
}

/// <summary>
/// Action flags do backend tính (single source of truth). Frontend CHỈ render button theo
/// các cờ này; mọi action vẫn được re-validate ở command handler tương ứng.
/// Lưu ý: gán host là MỘT LẦN (final) — hiện không có flow host chấp nhận/từ chối, nên
/// canAcceptHost/canDeclineHost luôn false cho tới khi nghiệp vụ đó tồn tại.
/// </summary>
public sealed class StaffCalendarAllowedActionsDto
{
    public bool CanViewDetail { get; set; } = true;
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public bool CanAssignHost { get; set; }
    public bool CanAcceptHost { get; set; }
    public bool CanDeclineHost { get; set; }
    public bool CanSendHostInvitationEmail { get; set; }
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
    /// <summary>NEW | NEEDS_ACTION | PROCESSED | CANCELLED_OR_EXPIRED | MINE.</summary>
    public string ColorType { get; set; } = StaffCalendarColorTypes.New;
    public StaffCalendarAllowedActionsDto AllowedActions { get; set; } = new();
}

public sealed class StaffCalendarResponse
{
    public string ViewMode { get; set; } = "office";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<StaffCalendarItemDto> Items { get; set; } = new();
}
