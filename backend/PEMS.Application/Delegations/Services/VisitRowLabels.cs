using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// The two labels that answer "where is this?" and "what am I to it?" — deliberately separate from
/// each other and from the next task.
///
/// They live on the server for the same reason the next task does: the same campus status means a
/// different thing to the person who approved it, the person hosting it and the person who filed it,
/// and a client that maps status → text has to pick one of those readings for everybody.
///
/// The wording is Vietnamese because this screen is Vietnamese; the raw status CODE travels alongside
/// on the same row, so a client that wants its own wording still can.
/// </summary>
public static class VisitRowLabels
{
    /// <summary>
    /// Process status. The campus instance wins when there is one — a request aggregate of
    /// PARTIALLY_APPROVED says nothing useful about the campus the reader is looking at.
    /// </summary>
    public static string Status(string requestStatus, string? campusStatus) => campusStatus switch
    {
        VisitInstanceStatus.Cancelled => "Đã hủy",
        VisitInstanceStatus.Rejected => "Đã bị từ chối",
        VisitInstanceStatus.WaitingRequestApproval => "Chờ xử lý tại cơ sở",
        VisitInstanceStatus.Assigned => "Đã duyệt và phân công",
        VisitInstanceStatus.BeforeVisit => "Đang chuẩn bị",
        VisitInstanceStatus.DuringVisit => "Đang tiếp khách",
        VisitInstanceStatus.AfterVisit => "Chờ đóng đoàn",
        VisitInstanceStatus.Closed => "Đã đóng đoàn",
        _ => requestStatus switch
        {
            VisitRequestStatuses.Cancelled => "Đã hủy",
            VisitRequestStatuses.Rejected => "Đã bị từ chối",
            VisitRequestStatuses.PendingApproval => "Chờ xử lý",
            VisitRequestStatuses.PartiallyApproved => "Duyệt một phần",
            VisitRequestStatuses.Approved => "Đã duyệt",
            _ => requestStatus,
        },
    };

    /// <summary>
    /// What the signed-in user is to this row. Never an authorization input — the relation is
    /// descriptive, and every action is still decided by the capability verdicts.
    /// </summary>
    public static string Relation(string relation) => relation switch
    {
        "HOST" => "Bạn phụ trách tiếp đón",
        "TEMP_HOST" => "Bạn tạm phụ trách tiếp đón",
        "VISITOR_OWNER" => "Bạn là đầu mối chính",
        "REGISTRANT_VIEWER" => "Bạn là người đăng ký",
        "CAMPUS_APPROVER" => "Bạn có quyền duyệt tại cơ sở",
        "IC_SUPPORT" or "DEPT_SUPPORT" or "STUDENT_SUPPORT" => "Bạn được mời tham dự",
        "DEPARTMENT_TASK_OWNER" => "Bạn được giao nhiệm vụ",
        _ => "Chỉ theo dõi",
    };
}
