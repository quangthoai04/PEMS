using System;
using PEMS.Application.Dashboard.Queries.GetStaffCalendar;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Dashboard.Common;

/// <summary>
/// Logic chung cho dashboard bảng lịch Staff/Staff Leader: tính action flags, nhãn trạng thái
/// và nhóm màu legend cho một campus visit instance. Dùng chung bởi query danh sách lịch và
/// query chi tiết để hai nơi không bao giờ lệch nhau. Quy tắc action bám sát UC-20/UC-22
/// (ViewGuestDelegationList.BuildAllowedActions): Staff Leader chỉ xử lý đơn thuộc campus mình;
/// gán host là một lần (không có accept/decline host trong nghiệp vụ hiện tại).
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

        // Trạng thái terminal (CANCELLED/REJECTED/CLOSED): không cho bất kỳ action mutate nào.
        if (isCancelled || isRejected || x.CampusStatus == VisitInstanceStatus.Closed)
            return actions;

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

        // Email mời host được gửi ngay trong bước gán host (template HOST_ASSIGNMENT).
        actions.CanSendHostInvitationEmail = actions.CanAssignHost;

        // Nghiệp vụ hiện tại: host được gán MỘT lần, không có flow chấp nhận/từ chối làm host
        // → hai cờ này giữ nguyên false (frontend sẽ không render button tương ứng).
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

        if (isCancelled)
            return ("Đã hủy", StaffCalendarColorTypes.CancelledOrExpired, true, false, isPast);
        if (isRejected)
            return ("Đã từ chối", StaffCalendarColorTypes.CancelledOrExpired, false, false, isPast);
        if (isExpired)
            return ("Đã hết hạn xử lý", StaffCalendarColorTypes.CancelledOrExpired, false, true, isPast);

        var (label, processed) = x.CampusStatus switch
        {
            VisitInstanceStatus.WaitingRequestApproval => ("Chờ duyệt", false),
            VisitInstanceStatus.WaitingHostAssignment => ("Chờ gán host", false),
            VisitInstanceStatus.Assigned => ("Đã gán host", true),
            VisitInstanceStatus.BeforeVisit => ("Chuẩn bị đón tiếp", true),
            VisitInstanceStatus.DuringVisit => ("Đang tiếp khách", true),
            VisitInstanceStatus.AfterVisit => ("Sau tiếp khách", true),
            VisitInstanceStatus.Closed => ("Đã hoàn tất", true),
            _ => ("Chờ xử lý", false),
        };

        string color;
        if (isMine)
            color = StaffCalendarColorTypes.Mine;                 // Tôi là host
        else if (actions.CanApprove || actions.CanAssignHost)
            color = StaffCalendarColorTypes.NeedsAction;          // Cần tôi xử lý
        else if (processed)
            color = StaffCalendarColorTypes.Processed;            // Đã xử lý
        else
            color = StaffCalendarColorTypes.New;                  // Mới / Chờ xử lý

        return (label, color, false, false, isPast);
    }
}
