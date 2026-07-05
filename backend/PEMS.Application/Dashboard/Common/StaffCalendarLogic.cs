using System;
using PEMS.Application.Dashboard.Queries.GetStaffCalendar;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Dashboard.Common;

/// <summary>
/// Logic chung cho dashboard bảng lịch Staff/Staff Leader: tính action flags, nhãn trạng thái
/// và nhóm màu legend cho một campus visit instance. Dùng chung bởi query danh sách lịch và
/// query chi tiết để hai nơi không bao giờ lệch nhau.
///
/// Quy tắc màu (chốt, theo thứ tự ưu tiên):
///   1. Xanh dương (MINE)      — tôi đang là host, bất kể ai duyệt/gán (ưu tiên cao nhất).
///   2. Xám (CANCELLED/EXPIRED)— bị hủy hoặc đã hết hạn xử lý.
///   3. Vàng (NEEDS_ACTION)    — chỉ Staff Leader: đơn chưa duyệt/chưa gán người phụ trách.
///   4. Xanh lá (PROCESSED)    — Staff Leader đã xử lý xong (duyệt hoặc từ chối) mà tôi
///      không phải host.
///   5. Trung tính (NEUTRAL)   — còn lại (ví dụ multi-campus đang chờ HO duyệt).
///
/// Quy tắc action:
///   • Staff Leader chỉ xử lý (duyệt/từ chối/gán host) đơn thuộc campus mình, đúng trạng thái.
///   • Gán host là MỘT LẦN, không gửi email, không có bước accept/decline — Staff được gán
///     mặc nhiên là host ngay, chỉ có action "Setup đoàn khách" (canSetupDelegation).
///   • Host luôn phải là STAFF thường (ràng buộc DB) — Staff Leader không thể tự nhận làm host,
///     nên "duyệt" và "gán host" luôn là một hành động duy nhất (mở modal chọn host).
/// </summary>
public static class StaffCalendarLogic
{
    public sealed record InstanceSnapshot(
        string RequestStatus,
        string? CampusStatus,
        string? VisitScope,
        ulong CampusId,
        ulong? CurrentHostUserId,
        DateTime PlannedStartAt,
        DateTime PlannedEndAt);

    public sealed record ViewerContext(
        ulong UserId,
        bool IsStaffLeader,
        ulong? PrimaryCampusId);

    public static StaffCalendarAllowedActionsDto BuildAllowedActions(
        InstanceSnapshot x, ViewerContext viewer, DateTime now)
    {
        var actions = new StaffCalendarAllowedActionsDto { CanViewDetail = true };

        bool isCancelled = x.RequestStatus == VisitRequestStatuses.Cancelled
            || x.CampusStatus == VisitInstanceStatus.Cancelled;
        bool isRejected = x.RequestStatus == VisitRequestStatuses.Rejected;
        bool sameCampus = viewer.PrimaryCampusId.HasValue && x.CampusId == viewer.PrimaryCampusId.Value;
        bool beforeStart = x.PlannedStartAt > now;
        bool isHost = x.CurrentHostUserId.HasValue && x.CurrentHostUserId.Value == viewer.UserId;

        // Terminal (CANCELLED/REJECTED/CLOSED): chỉ còn xem chi tiết, không action mutate.
        if (isCancelled || isRejected || x.CampusStatus == VisitInstanceStatus.Closed)
        {
            actions.CanSetupDelegation = isHost;
            return actions;
        }

        if (viewer.IsStaffLeader && sameCampus)
        {
            // SINGLE_CAMPUS đang chờ duyệt: duyệt = duyệt + gán host trong một bước (UC-22).
            if (x.VisitScope == VisitScopes.SingleCampus
                && x.RequestStatus == VisitRequestStatuses.PendingApproval
                && x.CampusStatus == VisitInstanceStatus.WaitingRequestApproval)
            {
                actions.CanApprove = true;
                actions.CanReject = true;
                actions.CanAssignHost = true;
            }
            // MULTI_CAMPUS đã được HO duyệt, chặng của campus mình chờ gán host.
            else if (x.VisitScope == VisitScopes.MultiCampus
                && x.RequestStatus == VisitRequestStatuses.Approved
                && x.CampusStatus == VisitInstanceStatus.WaitingHostAssignment
                && beforeStart)
            {
                actions.CanAssignHost = true;
            }
        }

        // Host (Staff Leader hoặc Staff thường đều có thể được gán làm host) → vào Setup đoàn khách.
        actions.CanSetupDelegation = isHost
            && x.CampusStatus != VisitInstanceStatus.WaitingRequestApproval
            && x.CampusStatus != VisitInstanceStatus.WaitingHostAssignment;

        return actions;
    }

    public static (string DisplayStatus, string ColorType, bool IsCancelled, bool IsExpired, bool IsPast)
        ResolveStatus(InstanceSnapshot x, ViewerContext viewer, StaffCalendarAllowedActionsDto actions, DateTime now)
    {
        bool isCancelled = x.RequestStatus == VisitRequestStatuses.Cancelled
            || x.CampusStatus == VisitInstanceStatus.Cancelled;
        bool isRejected = x.RequestStatus == VisitRequestStatuses.Rejected;
        bool isPast = x.PlannedEndAt < now;
        bool isPending = x.RequestStatus == VisitRequestStatuses.PendingApproval
            || x.CampusStatus == VisitInstanceStatus.WaitingHostAssignment;
        bool isExpired = !isCancelled && !isRejected && isPending && isPast;
        bool isMine = x.CurrentHostUserId.HasValue && x.CurrentHostUserId.Value == viewer.UserId;
        bool hasNoHost = !x.CurrentHostUserId.HasValue;
        // "Đã xử lý": Staff Leader đã ra quyết định (duyệt xong tới ASSIGNED trở lên, hoặc từ chối).
        bool isDecided = isRejected
            || x.CampusStatus == VisitInstanceStatus.Assigned
            || x.CampusStatus == VisitInstanceStatus.BeforeVisit
            || x.CampusStatus == VisitInstanceStatus.DuringVisit
            || x.CampusStatus == VisitInstanceStatus.AfterVisit
            || x.CampusStatus == VisitInstanceStatus.Closed;

        string label;
        if (isRejected) label = "Đã từ chối";
        else if (isCancelled) label = "Đã hủy";
        else if (isExpired) label = "Đã hết hạn xử lý";
        else
        {
            label = x.CampusStatus switch
            {
                VisitInstanceStatus.WaitingRequestApproval => "Chờ duyệt",
                VisitInstanceStatus.WaitingHostAssignment => "Chờ gán host",
                VisitInstanceStatus.Assigned => "Đã gán host",
                VisitInstanceStatus.BeforeVisit => "Chuẩn bị đón tiếp",
                VisitInstanceStatus.DuringVisit => "Đang tiếp khách",
                VisitInstanceStatus.AfterVisit => "Sau tiếp khách",
                VisitInstanceStatus.Closed => "Đã hoàn tất",
                _ => "Chờ xử lý",
            };
        }

        // Thứ tự ưu tiên: tôi là host > hủy/hết hạn > cần xử lý (chỉ Staff Leader) > đã xử lý > trung tính.
        string color;
        if (isMine)
            color = StaffCalendarColorTypes.Mine;
        else if (isCancelled || isExpired)
            color = StaffCalendarColorTypes.CancelledOrExpired;
        else if (hasNoHost && !isRejected && viewer.IsStaffLeader)
            color = StaffCalendarColorTypes.NeedsAction;
        else if (isDecided)
            color = StaffCalendarColorTypes.Processed;
        else
            color = StaffCalendarColorTypes.Neutral;

        return (label, color, isCancelled, isExpired, isPast);
    }
}
