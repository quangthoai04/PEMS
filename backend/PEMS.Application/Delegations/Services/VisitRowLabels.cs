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
    /// <summary>One resolution: the canonical CODE (see <see cref="EffectiveStatusCodes"/>) and its Vietnamese LABEL, decided together so they can never drift apart — see <see cref="Resolve"/>.</summary>
    private readonly record struct Resolution(string Code, string Label);

    /// <summary>
    /// Process status — code and label decided TOGETHER, in one switch, on purpose: a filter reading
    /// <see cref="EffectiveCode"/> and a badge reading <see cref="Status"/> used to come from two
    /// independently-maintained switches, and the two drifting apart (a row could pass a status
    /// filter while its own badge showed a different status) was a real, shipped bug. One switch
    /// means that class of bug is now structurally impossible, not just fixed for today's inputs.
    ///
    /// The campus instance wins when there is one — a request aggregate of PARTIALLY_APPROVED says
    /// nothing useful about the campus the reader is looking at.
    ///
    /// Vocabulary is shared by Staff/Staff Leader (and Department/Student, which read the same
    /// internal wording — they never reach a role-specific branch of their own): Chờ xác nhận ·
    /// Chờ duyệt · Đã duyệt · Đang chuẩn bị · Đang diễn ra · Chờ đóng · Đã hoàn tất · Từ chối ·
    /// Đã hủy. Visitor and HO each read two words differently — see <paramref name="roleCode"/>:
    ///   - Visitor sees "Chờ đánh giá" instead of "Chờ đóng" for AFTER_VISIT — from their side the
    ///     visit itself is over, so "closing the delegation" (FPT's internal paperwork) reads as
    ///     waiting on the feedback they still owe, not on staff. The underlying STAGE (and so the
    ///     canonical CODE) is the same regardless — only the wording changes.
    ///   - Visitor and HO both read "Đã từ chối" (not "Từ chối") — the extra "đã" reads right for
    ///     someone one step removed from the decision. Code unaffected, same as above.
    ///   - HO additionally reads PARTIALLY_APPROVED as "Chờ duyệt" rather than its own word — from
    ///     HO's monitoring view a multi-campus request with any campus still undecided is simply
    ///     still pending, same bucket as a single campus that hasn't been touched yet. HERE the CODE
    ///     changes too (to <see cref="VisitInstanceStatus.WaitingRequestApproval"/>), not just the
    ///     text — a filter for "Chờ duyệt" must catch this row for HO exactly when the badge does.
    ///
    /// The REQUEST-level fallback (campusStatus null) keeps its own aggregate vocabulary because a
    /// summary row is answering a different question from a campus row: "where is this request as a
    /// whole?", not "where is my campus?". Per-campus movement underneath an aggregate is carried by
    /// the row's ChangeSummary/campus indicators (see AttachChangeSummariesAsync) — a "something
    /// changed here" signal deliberately separate from the status word.
    /// </summary>
    private static Resolution Resolve(string requestStatus, string? campusStatus, string? roleCode) => campusStatus switch
    {
        VisitInstanceStatus.Cancelled => new Resolution(VisitInstanceStatus.Cancelled, "Đã hủy"),
        VisitInstanceStatus.Rejected => new Resolution(VisitInstanceStatus.Rejected, RejectedLabel(roleCode)),
        // Behind the confirmation gate: the campus is waiting for its operational contact to answer,
        // and until every campus has, no Staff Leader may decide any of them. This label is what a
        // leader now reads on such a row — it has to say "waiting", not "waiting for you".
        VisitInstanceStatus.WaitingContactConfirmation => new Resolution(VisitInstanceStatus.WaitingContactConfirmation, "Chờ xác nhận"),
        VisitInstanceStatus.WaitingRequestApproval => new Resolution(VisitInstanceStatus.WaitingRequestApproval, "Chờ duyệt"),
        // Approved with a person named, and that person has not started preparing yet. "Host" is the
        // internal word; the reader gets the Vietnamese one, like every other label here.
        VisitInstanceStatus.Assigned => new Resolution(VisitInstanceStatus.Assigned, "Đã duyệt"),
        VisitInstanceStatus.BeforeVisit => new Resolution(VisitInstanceStatus.BeforeVisit, "Đang chuẩn bị"),
        VisitInstanceStatus.DuringVisit => new Resolution(VisitInstanceStatus.DuringVisit, "Đang diễn ra"),
        VisitInstanceStatus.AfterVisit => new Resolution(VisitInstanceStatus.AfterVisit, roleCode == RoleCodes.Visitor ? "Chờ đánh giá" : "Chờ đóng"),
        VisitInstanceStatus.Closed => new Resolution(VisitInstanceStatus.Closed, "Đã hoàn tất"),
        _ => requestStatus switch
        {
            VisitRequestStatuses.Cancelled => new Resolution(VisitInstanceStatus.Cancelled, "Đã hủy"),
            VisitRequestStatuses.Rejected => new Resolution(VisitInstanceStatus.Rejected, RejectedLabel(roleCode)),
            // The whole request is behind the confirmation gate. Without this the summary row a
            // registrant/HO sees would fall through and print the raw enum.
            VisitRequestStatuses.PendingContactConfirmation => new Resolution(VisitInstanceStatus.WaitingContactConfirmation, "Chờ xác nhận"),
            VisitRequestStatuses.PendingApproval => new Resolution(VisitInstanceStatus.WaitingRequestApproval, "Chờ duyệt"),
            VisitRequestStatuses.PartiallyApproved => roleCode == RoleCodes.Ho
                ? new Resolution(VisitInstanceStatus.WaitingRequestApproval, "Chờ duyệt")
                : new Resolution(EffectiveStatusCodes.PartiallyApproved, "Duyệt một phần"),
            VisitRequestStatuses.Approved => new Resolution(EffectiveStatusCodes.ApprovedNoLiveCampus, "Đã duyệt"),
            _ => new Resolution(requestStatus, requestStatus),
        },
    };

    /// <summary>The Vietnamese label half of <see cref="Resolve"/> — see its doc comment for the full branching.</summary>
    public static string Status(string requestStatus, string? campusStatus, string? roleCode = null) =>
        Resolve(requestStatus, campusStatus, roleCode).Label;

    /// <summary>
    /// The canonical status CODE half of <see cref="Resolve"/> — one of the 9 <see cref="VisitInstanceStatus"/>
    /// values, or <see cref="EffectiveStatusCodes.PartiallyApproved"/>/<see cref="EffectiveStatusCodes.ApprovedNoLiveCampus"/>
    /// for the two request-aggregate-only cases. A filter that matches a row on THIS code is
    /// guaranteed to agree with the badge <see cref="Status"/> renders for the same row — both come
    /// from the same resolution.
    /// </summary>
    public static string EffectiveCode(string requestStatus, string? campusStatus, string? roleCode = null) =>
        Resolve(requestStatus, campusStatus, roleCode).Code;

    private static string RejectedLabel(string? roleCode) =>
        roleCode == RoleCodes.Visitor || roleCode == RoleCodes.Ho ? "Đã từ chối" : "Từ chối";

    // Same order a single campus instance moves through, once decided — used to pick the ONE status a
    // multi-campus SUMMARY row (no single instance of its own) should show for DISPLAY
    // (ResolveMultiCampusProgress/MultiCampusProgress/MultiCampusProgressCode below). Request-level
    // FILTERING no longer uses this ranking at all — ViewGuestDelegationListQueryHandler's
    // ApplyRequestLevelAnyCampusStatusFilter matches "any campus instance carries the requested
    // status" directly, not a single least-progressed-campus bucket (see
    // docs/CanhIter3FixBug/GopYCQuyen/PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md).
    internal static readonly string[] ProgressOrder =
    {
        VisitInstanceStatus.Assigned,
        VisitInstanceStatus.BeforeVisit,
        VisitInstanceStatus.DuringVisit,
        VisitInstanceStatus.AfterVisit,
        VisitInstanceStatus.Closed,
    };

    /// <summary>
    /// The resolution a multi-campus SUMMARY row uses once <see cref="Resolve"/> would otherwise
    /// give the generic aggregate "Đã duyệt" — visit_requests.status only tracks the APPROVAL
    /// aggregate (pending/partially/approved/rejected/cancelled), it is never re-derived as campuses
    /// move through preparing/during/after/closed, so a request left at "Đã duyệt" forever even
    /// after every campus actually finished was reading stale data, not a wording choice.
    ///
    /// Picks whichever campus is LEAST progressed (Rejected/Cancelled instances excluded — they
    /// are a different, already-terminal outcome for that campus, not "still in progress"): the
    /// whole delegation is not "Đã hoàn tất" until every live campus is, mirroring how the single-
    /// campus badge already reads. Null when there is nothing left to rank (e.g. every campus
    /// Rejected/Cancelled — <see cref="Resolve"/>'s own requestStatus branch already covers that,
    /// producing <see cref="EffectiveStatusCodes.ApprovedNoLiveCampus"/>).
    /// </summary>
    private static Resolution? ResolveMultiCampusProgress(IEnumerable<string?> campusInstanceStatuses, string? roleCode)
    {
        var leastProgressed = campusInstanceStatuses
            .Select(s => System.Array.IndexOf(ProgressOrder, s))
            .Where(rank => rank >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        return leastProgressed < 0 ? null : Resolve(VisitRequestStatuses.Approved, ProgressOrder[leastProgressed], roleCode);
    }

    /// <summary>The Vietnamese label half of <see cref="ResolveMultiCampusProgress"/> — see its doc comment.</summary>
    public static string? MultiCampusProgress(IEnumerable<string?> campusInstanceStatuses, string? roleCode = null) =>
        ResolveMultiCampusProgress(campusInstanceStatuses, roleCode)?.Label;

    /// <summary>The canonical CODE half of <see cref="ResolveMultiCampusProgress"/> — pairs with <see cref="MultiCampusProgress"/> the same way <see cref="EffectiveCode"/> pairs with <see cref="Status"/>.</summary>
    public static string? MultiCampusProgressCode(IEnumerable<string?> campusInstanceStatuses, string? roleCode = null) =>
        ResolveMultiCampusProgress(campusInstanceStatuses, roleCode)?.Code;

    /// <summary>
    /// What the signed-in user is to this row. Never an authorization input — the relation is
    /// descriptive, and every action is still decided by the capability verdicts.
    /// </summary>
    public static string Relation(string relation) => relation switch
    {
        "HOST" => "Bạn phụ trách tiếp đón",
        "TEMP_HOST" => "Bạn tạm phụ trách tiếp đón",
        // "đầu mối đoàn khách", never "đầu mối chính": contacts are per campus, so no one is THE
        // primary contact of a request any more.
        "VISITOR_OWNER" => "Bạn là đầu mối đoàn khách",
        "REGISTRANT_VIEWER" => "Bạn là người đăng ký",
        "CAMPUS_APPROVER" => "Bạn có quyền duyệt tại cơ sở",
        "IC_SUPPORT" or "DEPT_SUPPORT" or "STUDENT_SUPPORT" => "Bạn được mời tham dự",
        "DEPARTMENT_TASK_OWNER" => "Bạn được giao nhiệm vụ",
        _ => "Chỉ theo dõi",
    };
}
