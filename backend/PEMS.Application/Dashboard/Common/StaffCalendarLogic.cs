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
///   3. Vàng (NEEDS_ACTION)    — chỉ Staff Leader: campus instance của campus mình chưa xử lý.
///   4. Xanh lá (PROCESSED)    — Staff Leader đã xử lý xong (duyệt hoặc từ chối) mà tôi
///      không phải host.
///   5. Trung tính (NEUTRAL)   — còn lại.
///
/// Quy tắc action (campus-independent approval, SQL v10):
///   • Mỗi Staff Leader xử lý (duyệt kèm gán host / từ chối) campus instance của campus mình,
///     bất kể đơn một hay nhiều cơ sở — không còn bước HO duyệt, không còn WAITING_HOST_ASSIGNMENT.
///   • Gán host là MỘT LẦN, nằm trong chính action duyệt, không gửi email, không accept/decline.
///   • Host có thể là IC Staff cùng campus hoặc CHÍNH Staff Leader đang duyệt (self-host).
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
        bool isRejected = x.CampusStatus == VisitInstanceStatus.Rejected;
        bool sameCampus = viewer.PrimaryCampusId.HasValue && x.CampusId == viewer.PrimaryCampusId.Value;
        bool isHost = x.CurrentHostUserId.HasValue && x.CurrentHostUserId.Value == viewer.UserId;

        // Terminal (CANCELLED/REJECTED/CLOSED): chỉ còn xem chi tiết, không action mutate.
        if (isCancelled || isRejected || x.CampusStatus == VisitInstanceStatus.Closed)
        {
            actions.CanSetupDelegation = isHost;
            return actions;
        }

        if (viewer.IsStaffLeader && sameCampus)
        {
            // Campus instance của campus mình đang chờ xử lý: duyệt = duyệt + gán host trong
            // một bước (bắt buộc chọn host, có thể tự chọn mình); hoặc từ chối kèm lý do.
            // Global contact-confirmation gate (spec §3.4, xem CampusApprovalExecutor.ApproveAndAssignHostAsync):
            // trong khi request còn PENDING_CONTACT_CONFIRMATION, chưa campus nào của request được quyết
            // định — kể cả campus mà chính đầu mối vận hành của nó đã xác nhận, nếu một campus anh chị em
            // khác trong cùng request chưa xác nhận. Mirror gate ở đây để UI không hiện nút mà command
            // layer chắc chắn sẽ từ chối (409) — command layer vẫn là nơi enforce thật, đây chỉ là hiển thị.
            if (x.CampusStatus == VisitInstanceStatus.WaitingRequestApproval
                && !VisitRequestStatuses.IsBehindContactGate(x.RequestStatus))
            {
                actions.CanApprove = true;
                actions.CanReject = true;
                actions.CanAssignHost = true;
            }
        }

        // Host (IC Staff hoặc chính Staff Leader self-host) → vào Setup đoàn khách.
        actions.CanSetupDelegation = isHost
            && x.CampusStatus != VisitInstanceStatus.WaitingRequestApproval;

        return actions;
    }

    public static (string DisplayStatus, string ColorType, bool IsCancelled, bool IsExpired, bool IsPast)
        ResolveStatus(InstanceSnapshot x, ViewerContext viewer, StaffCalendarAllowedActionsDto actions, DateTime now)
    {
        bool isCancelled = x.RequestStatus == VisitRequestStatuses.Cancelled
            || x.CampusStatus == VisitInstanceStatus.Cancelled;
        bool isRejected = x.CampusStatus == VisitInstanceStatus.Rejected;
        bool isPast = x.PlannedEndAt < now;
        bool isPending = x.CampusStatus == VisitInstanceStatus.WaitingRequestApproval;
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
                // Đang chờ khách xác nhận đầu mối vận hành — chờ KHÁCH, không phải chờ Staff Leader
                // xử lý. Cùng nghĩa/cùng chữ với VisitRowLabels.Resolve cho giá trị này.
                VisitInstanceStatus.WaitingContactConfirmation => "Chờ xác nhận",
                VisitInstanceStatus.WaitingRequestApproval => "Chờ xử lý tại campus",
                VisitInstanceStatus.Assigned => "Đã gán host",
                VisitInstanceStatus.BeforeVisit => "Chuẩn bị đón tiếp",
                VisitInstanceStatus.DuringVisit => "Đang tiếp khách",
                VisitInstanceStatus.AfterVisit => "Sau tiếp khách",
                VisitInstanceStatus.Closed => "Đã hoàn tất",
                _ => "Chờ xử lý",
            };
        }

        // Đang chờ khách xác nhận đầu mối: chưa có gì cho Staff Leader làm ở bước này (BuildAllowedActions
        // không cấp action nào), nên KHÔNG được tô NEEDS_ACTION — dễ nhầm "cần xử lý" thành việc của mình.
        bool isWaitingContact = x.CampusStatus == VisitInstanceStatus.WaitingContactConfirmation;

        // Thứ tự ưu tiên: tôi là host > hủy/hết hạn > cần xử lý (chỉ Staff Leader) > đã xử lý / đã có người phụ trách > trung tính.
        string color;
        if (isMine)
            color = StaffCalendarColorTypes.Mine;
        else if (isCancelled || isExpired)
            color = StaffCalendarColorTypes.CancelledOrExpired;
        else if (hasNoHost && !isRejected && !isWaitingContact && viewer.IsStaffLeader)
            color = StaffCalendarColorTypes.NeedsAction;
        else if (isDecided || !hasNoHost)
            color = StaffCalendarColorTypes.Processed;
        else
            color = StaffCalendarColorTypes.Neutral;

        return (label, color, isCancelled, isExpired, isPast);
    }
}
