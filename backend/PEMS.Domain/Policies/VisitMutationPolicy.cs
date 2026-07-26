using PEMS.Domain.Constants;

namespace PEMS.Domain.Policies;

/// <summary>
/// Every mutation a requester or a campus Staff Leader can perform on a filed visit request.
/// One code per user-visible action, so "what may I do here" is answered in one vocabulary by the
/// read model, by each command handler, and by the UI.
/// </summary>
public enum VisitMutationAction
{
    /// <summary>Edit a fully-pending request in place (request-level, all campuses still waiting).</summary>
    EditPendingRequest,
    /// <summary>Edit and re-send a fully-rejected request (request-level).</summary>
    ResubmitRejectedRequest,
    /// <summary>Apply-now correction to the safe field subset (request-level or instance-level).</summary>
    SubmitSafeEdit,
    /// <summary>Propose a change to one decided campus, for its Staff Leader to approve.</summary>
    SubmitAmendment,
    /// <summary>Approve a campus's pending amendment (campus Staff Leader).</summary>
    ApproveAmendment,
    /// <summary>Hand one campus's Host role to a different eligible user (campus Staff Leader).</summary>
    TransferHost,
}

/// <summary>Who is asking, in business terms rather than role names.</summary>
public static class VisitViewerRelations
{
    /// <summary>Registrant or the ACTIVE primary contact — the requester side.</summary>
    public const string Requester = "REQUESTER";
    /// <summary>Staff Leader of the campus the action targets.</summary>
    public const string CampusLeader = "CAMPUS_LEADER";
    /// <summary>Anyone else (Host, HO, participant, unrelated). Never mutates.</summary>
    public const string Other = "OTHER";
}

/// <summary>Stable reasons an action is refused. The UI maps these to sentences; never parse messages.</summary>
public static class VisitMutationErrorCodes
{
    /// <summary>Inside the required lead time (or already started).</summary>
    public const string CutoffReached = "VISIT_MUTATION_CUTOFF_REACHED";
    /// <summary>The request/campus is not in a state where this action exists.</summary>
    public const string LifecycleNotAllowed = "VISIT_MUTATION_LIFECYCLE_NOT_ALLOWED";
    /// <summary>This viewer is never allowed to perform this action.</summary>
    public const string RelationNotAllowed = "VISIT_MUTATION_RELATION_NOT_ALLOWED";
}

/// <summary>
/// What is being asked, at one point in time. <paramref name="PlannedStartAt"/> and
/// <paramref name="VietnamNow"/> are wall-clock Vietnam time — the whole stack stores and compares
/// wall clock, so no conversion happens here.
/// </summary>
/// <param name="InstanceStatus">
/// The campus this action targets. For a request-level action, pass the status of the campus whose
/// start time governs the window (see <see cref="VisitMutationPolicy.RequestLevelScope"/>).
/// </param>
public sealed record VisitMutationContext(
    VisitMutationAction Action,
    string RequestStatus,
    string InstanceStatus,
    DateTime PlannedStartAt,
    DateTime VietnamNow,
    string ViewerRelation);

/// <summary>
/// The verdict. <paramref name="CutoffAt"/> is filled whenever the action HAS a deadline — allowed or
/// not — so the UI can say "you have until 08:00" before the deadline as well as after it.
/// </summary>
public sealed record VisitMutationDecision(
    bool Allowed,
    string? ErrorCode,
    string? DisabledReason,
    DateTime? CutoffAt,
    int RequiredLeadHours);

/// <summary>
/// The single place that decides whether a visit mutation is still open.
///
/// Before this existed the same rule lived as a bare <c>24</c> in the read model, in the safe-edit
/// service and in the amendment service, and each one had drifted: the read model offered "Sửa nhanh"
/// on a campus that was already under way, and the safe-edit service let a request-level change
/// through when no campus was active. A capability that promises what the handler then refuses is
/// worse than no capability at all, so the read model and the handler now ask THIS, and get the same
/// answer.
///
/// The window is a MINIMUM LEAD TIME, not a countdown: an action is open while the target campus
/// starts at least <see cref="RequiredLeadHours"/> from now. Exactly on the boundary is open — a rule
/// stated as "at least 6 hours before" includes the six-hour mark.
/// </summary>
public static class VisitMutationPolicy
{
    /// <summary>Minimum lead time before a campus starts, shared by every self-service mutation.</summary>
    public const int RequiredLeadHours = 6;

    /// <summary>Campus states that are decided and have not started — where post-approval actions live.</summary>
    private static bool IsDecidedNotStarted(string instanceStatus) =>
        instanceStatus is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit;

    /// <summary>
    /// The campuses whose lifecycle and start time govern a REQUEST-level action (safe edit of the
    /// shared registrant/contact block, pending edit, resubmit).
    ///
    /// Two rules, and the second is the one that used to be missing: the action is refused outright if
    /// ANY campus has moved past the point of no return, and the deadline is taken from the EARLIEST
    /// campus still ahead of us. A request-level field is shared by every campus, so a campus that is
    /// already receiving its delegation must not have the text under it rewritten.
    /// </summary>
    public static bool RequestLevelScope(string instanceStatus) =>
        instanceStatus is VisitInstanceStatuses.WaitingRequestApproval
            or VisitInstanceStatuses.Assigned
            or VisitInstanceStatuses.BeforeVisit;

    /// <summary>Campus states that block every request-level action while any campus is in them.</summary>
    public static bool BlocksRequestLevel(string instanceStatus) =>
        instanceStatus is VisitInstanceStatuses.DuringVisit
            or VisitInstanceStatuses.AfterVisit
            or VisitInstanceStatuses.Closed;

    public static VisitMutationDecision Evaluate(VisitMutationContext context)
    {
        var cutoffAt = context.PlannedStartAt.AddHours(-RequiredLeadHours);

        // ── 1. Relation. Checked first so a Host never sees a reason that implies "come back earlier". ──
        var relationOk = context.Action switch
        {
            VisitMutationAction.ApproveAmendment or VisitMutationAction.TransferHost
                => context.ViewerRelation == VisitViewerRelations.CampusLeader,
            _ => context.ViewerRelation == VisitViewerRelations.Requester,
        };
        if (!relationOk)
            return new VisitMutationDecision(
                false, VisitMutationErrorCodes.RelationNotAllowed,
                "Bạn không có quyền thực hiện thao tác này.", cutoffAt, RequiredLeadHours);

        // ── 2. Lifecycle. A cancelled request has no actions at all, whatever its campuses say. ──
        if (context.RequestStatus == VisitRequestStatuses.Cancelled)
            return Refused(cutoffAt, "Đơn đã bị hủy nên không thể thay đổi.");

        var lifecycleOk = context.Action switch
        {
            VisitMutationAction.EditPendingRequest =>
                context.RequestStatus == VisitRequestStatuses.PendingApproval
                && context.InstanceStatus == VisitInstanceStatuses.WaitingRequestApproval,

            VisitMutationAction.ResubmitRejectedRequest =>
                context.RequestStatus == VisitRequestStatuses.Rejected
                && context.InstanceStatus == VisitInstanceStatuses.Rejected,

            // Safe edit is a post-decision correction. A still-pending campus belongs to pending-edit,
            // which can change everything — offering the narrow tool there only confuses the choice.
            VisitMutationAction.SubmitSafeEdit
                or VisitMutationAction.SubmitAmendment
                or VisitMutationAction.ApproveAmendment
                or VisitMutationAction.TransferHost => IsDecidedNotStarted(context.InstanceStatus),

            _ => false,
        };
        if (!lifecycleOk)
            return Refused(cutoffAt, LifecycleReason(context));

        // ── 3. Cutoff. "At least N hours before start", so the boundary itself is inside the window. ──
        //    Comparing start-minus-lead against now (rather than now-plus-lead against start) keeps the
        //    boundary exact: both sides are wall-clock, and equality means allowed.
        //
        //    Resubmit is the one action measured against a schedule that does not exist yet. A rejected
        //    request usually sits until well after its original date, so testing the OLD start would
        //    refuse every resubmit that matters — the point of resubmitting is to propose a NEW date.
        //    Its lead time is enforced where that date is known: VisitRequestV2EditService.ApplyResubmit
        //    checks every proposed start against the same RequiredLeadHours.
        if (context.Action == VisitMutationAction.ResubmitRejectedRequest)
            return new VisitMutationDecision(true, null, null, null, RequiredLeadHours);

        if (context.VietnamNow > cutoffAt)
            return new VisitMutationDecision(
                false, VisitMutationErrorCodes.CutoffReached,
                $"Thao tác này chỉ được thực hiện ít nhất {RequiredLeadHours} giờ trước khi chuyến thăm bắt đầu.",
                cutoffAt, RequiredLeadHours);

        return new VisitMutationDecision(true, null, null, cutoffAt, RequiredLeadHours);
    }

    private static VisitMutationDecision Refused(DateTime cutoffAt, string reason) =>
        new(false, VisitMutationErrorCodes.LifecycleNotAllowed, reason, cutoffAt, RequiredLeadHours);

    private static string LifecycleReason(VisitMutationContext context) => context.InstanceStatus switch
    {
        VisitInstanceStatuses.DuringVisit => "Chuyến thăm tại cơ sở này đang diễn ra nên không thể thay đổi.",
        VisitInstanceStatuses.AfterVisit or VisitInstanceStatuses.Closed =>
            "Chuyến thăm tại cơ sở này đã kết thúc nên không thể thay đổi.",
        VisitInstanceStatuses.Cancelled => "Cơ sở này đã bị hủy nên không thể thay đổi.",
        VisitInstanceStatuses.Rejected when context.Action != VisitMutationAction.ResubmitRejectedRequest =>
            "Cơ sở này đã bị từ chối; hãy dùng chức năng sửa và gửi lại đơn.",
        VisitInstanceStatuses.WaitingRequestApproval when context.Action != VisitMutationAction.EditPendingRequest =>
            "Cơ sở này chưa được duyệt; hãy dùng chức năng sửa đơn đang chờ duyệt.",
        _ => "Trạng thái hiện tại không cho phép thực hiện thao tác này.",
    };
}
