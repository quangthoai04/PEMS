using System.Collections.Generic;
using System.Linq;
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
    ///
    /// Vocabulary shared by every role: Chờ duyệt · Đã duyệt · Đang chuẩn bị · Đang diễn ra ·
    /// Chờ đóng đoàn · Đã hoàn tất · Từ chối · Đã hủy.
    ///
    /// A multi-campus request with SOME campuses decided and others still waiting used to say
    /// "Duyệt một phần" — a distinct word campus staff had to learn. It no longer gets one: the
    /// request-level aggregate stays "Chờ duyệt" until every campus is past waiting, and the
    /// per-campus movement that already happened is carried by the row's ChangeSummary/campus
    /// indicators instead (see AttachChangeSummariesAsync) — a "something changed here" signal
    /// deliberately separate from the status word.
    /// </summary>
    public static string Status(string requestStatus, string? campusStatus) => campusStatus switch
    {
        VisitInstanceStatus.Cancelled => "Đã hủy",
        VisitInstanceStatus.Rejected => "Từ chối",
        VisitInstanceStatus.WaitingRequestApproval => "Chờ duyệt",
        VisitInstanceStatus.Assigned => "Đã duyệt",
        VisitInstanceStatus.BeforeVisit => "Đang chuẩn bị",
        VisitInstanceStatus.DuringVisit => "Đang diễn ra",
        VisitInstanceStatus.AfterVisit => "Chờ đóng đoàn",
        VisitInstanceStatus.Closed => "Đã hoàn tất",
        _ => requestStatus switch
        {
            VisitRequestStatuses.Cancelled => "Đã hủy",
            VisitRequestStatuses.Rejected => "Từ chối",
            VisitRequestStatuses.PendingApproval => "Chờ duyệt",
            VisitRequestStatuses.PartiallyApproved => "Chờ duyệt",
            VisitRequestStatuses.Approved => "Đã duyệt",
            _ => requestStatus,
        },
    };

    // Same order a single campus instance moves through, once decided — used to pick the ONE
    // status a multi-campus SUMMARY row (no single instance of its own) should show.
    private static readonly string[] ProgressOrder =
    {
        VisitInstanceStatus.Assigned,
        VisitInstanceStatus.BeforeVisit,
        VisitInstanceStatus.DuringVisit,
        VisitInstanceStatus.AfterVisit,
        VisitInstanceStatus.Closed,
    };

    /// <summary>
    /// The status a multi-campus SUMMARY row shows once <see cref="Status"/> would otherwise say
    /// the generic aggregate "Đã duyệt" — visit_requests.status only tracks the APPROVAL aggregate
    /// (pending/partially/approved/rejected/cancelled), it is never re-derived as campuses move
    /// through preparing/during/after/closed, so a request left at "Đã duyệt" forever even after
    /// every campus actually finished was reading stale data, not a wording choice.
    ///
    /// Shows whichever campus is LEAST progressed (Rejected/Cancelled instances excluded — they
    /// are a different, already-terminal outcome for that campus, not "still in progress"): the
    /// whole delegation is not "Đã hoàn tất" until every live campus is, mirroring how the single-
    /// campus badge already reads. Null when there is nothing left to rank (e.g. every campus
    /// Rejected/Cancelled — <see cref="Status"/>'s own requestStatus branch already covers that).
    /// </summary>
    public static string? MultiCampusProgress(IEnumerable<string?> campusInstanceStatuses)
    {
        var leastProgressed = campusInstanceStatuses
            .Select(s => System.Array.IndexOf(ProgressOrder, s))
            .Where(rank => rank >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        return leastProgressed < 0 ? null : Status(VisitRequestStatuses.Approved, ProgressOrder[leastProgressed]);
    }

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
