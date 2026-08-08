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
    /// <summary>
    /// Edit ONE still-pending campus in place, leaving every sibling exactly as it was.
    ///
    /// <para>
    /// The request-level <see cref="EditPendingRequest"/> is all-or-nothing: it exists only while EVERY
    /// campus is still waiting. That left a pending campus of a MIXED request — one approved, one
    /// waiting, one refused — with no way to be corrected at all, because the whole-request edit refused
    /// on the approved sibling and safe-edit/amendment refused on "not decided yet". This action is that
    /// missing door, and it targets exactly one campus so no sibling's status, host, decision or revision
    /// can move with it.
    /// </para>
    /// </summary>
    EditPendingCampus,
    /// <summary>Edit and re-send a fully-rejected request (request-level).</summary>
    ResubmitRejectedRequest,
    /// <summary>Apply-now correction to the safe field subset (request-level or instance-level).</summary>
    SubmitSafeEdit,
    /// <summary>Propose a change to one decided campus, for its current Host to approve.</summary>
    SubmitAmendment,
    /// <summary>Approve a campus's pending amendment (its CURRENT Host).</summary>
    ApproveAmendment,
    /// <summary>Hand one campus's Host role to a different eligible user (campus Staff Leader).</summary>
    TransferHost,
}

/// <summary>
/// The one wording for "this start time is too soon", used by create, pending-edit and resubmit so the
/// three paths cannot describe the same rule three ways.
///
/// <para>
/// The sentence names the EARLIEST ALLOWED START rather than only the number of hours: "ít nhất 72 giờ"
/// on its own leaves the reader doing arithmetic against a clock they cannot see, and the answer they
/// need is a date they can type back into the form. <c>BusinessRuleException</c> carries no metadata
/// slot, so both facts travel in the message — the error CODE stays
/// <c>INVALID_VISIT_TIME</c> and the API contract is unchanged.
/// </para>
/// </summary>
public static class VisitScheduleMessages
{
    public static string LeadTimeNotMet(DateTime earliestAllowedStart) =>
        $"Thời gian bắt đầu phải cách thời điểm hiện tại ít nhất {VisitMutationPolicy.MinScheduleLeadHours} giờ " +
        $"(sớm nhất là {earliestAllowedStart:HH:mm dd/MM/yyyy}).";

    /// <summary>
    /// The Staff Leader's version of the same fact. It does not say "invalid", because for this actor it
    /// is not — it is a schedule they are entitled to accept and are being asked to mean deliberately.
    /// </summary>
    public static string LeadTimeOverrideRequired(DateTime earliestAllowedStart) =>
        $"Lịch mới không đáp ứng thời gian đăng ký trước tối thiểu {VisitMutationPolicy.MinScheduleLeadHours} giờ " +
        $"(sớm nhất là {earliestAllowedStart:HH:mm dd/MM/yyyy}). " +
        "Với quyền Staff Leader của cơ sở này, bạn có thể xác nhận để tiếp tục với lịch đã chọn.";
}

/// <summary>
/// The verdict on a PROPOSED start time, before approval. Three outcomes rather than two, because the
/// Staff Leader of the campus is allowed to file a schedule inside the 72-hour registration floor and
/// the middle outcome is what asks them to say so on purpose.
/// </summary>
/// <param name="ConfirmationRequired">
/// True only for an actor who may override and has not confirmed. Never true for the requester side —
/// for them an inside-the-floor schedule is simply refused, and offering a "continue anyway" would be
/// offering something the backend will not honour.
/// </param>
public sealed record VisitScheduleLeadTimeDecision(
    bool Allowed, bool ConfirmationRequired, DateTime EarliestAllowedStart);

/// <summary>Who is asking, in business terms rather than role names.</summary>
public static class VisitViewerRelations
{
    /// <summary>Registrant or the ACTIVE primary contact — the requester side.</summary>
    public const string Requester = "REQUESTER";
    /// <summary>Staff Leader of the campus the action targets.</summary>
    public const string CampusLeader = "CAMPUS_LEADER";
    /// <summary>Anyone else (HO, participant, unrelated). Never mutates.</summary>
    public const string Other = "OTHER";
    /// <summary>The designated Host of this visit instance.</summary>
    public const string Host = "HOST";
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

    /// <summary>
    /// The actor MAY file this schedule, but it is inside the 72-hour registration floor and they have
    /// not said they mean it yet. Only ever returned to the Staff Leader of the campus being edited —
    /// for anyone else a schedule inside the floor is a plain <c>INVALID_VISIT_TIME</c> refusal.
    ///
    /// <para>
    /// It is a 409, not a validation error: nothing is wrong with the payload, the caller simply has to
    /// re-send it with <c>overrideLeadTimeConfirmed</c>. The client shows the confirmation dialog on
    /// THIS code and on no other, so a genuine refusal can never be turned into a dialog that offers to
    /// proceed anyway.
    /// </para>
    /// </summary>
    public const string LeadTimeOverrideConfirmationRequired = "LEAD_TIME_OVERRIDE_CONFIRMATION_REQUIRED";

    /// <summary>The actor is not the campus's CURRENT Host, and this action belongs to whoever is.</summary>
    public const string NotCurrentHost = "NOT_CURRENT_HOST";
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
    /// <summary>
    /// Minimum lead time before a campus starts, shared by every self-service mutation. Also known as
    /// the MUTATION CUTOFF: it answers "is this action still open", never "may the visit be scheduled
    /// here" — that is <see cref="MinScheduleLeadHours"/>, and the two must never be conflated.
    ///
    /// <para>
    /// It applies uniformly. There is no per-field exception: a media-consent withdrawal used to be
    /// allowed through it on privacy grounds, which meant one class of change had a different deadline
    /// from every other and the answer to "until when may I edit this" depended on which field the user
    /// happened to touch. Withdrawing consent late is now a conversation with the Host, not a silent
    /// write into a campus that has already printed its list.
    /// </para>
    /// </summary>
    public const int RequiredLeadHours = 6;

    /// <summary>The cutoff under the name the business rules use. Same number, same meaning.</summary>
    public const int MutationCutoffHours = RequiredLeadHours;

    /// <summary>
    /// Minimum lead time a schedule may be SET TO. Distinct from <see cref="RequiredLeadHours"/> on
    /// purpose, and the two answer different questions:
    ///
    /// <list type="bullet">
    /// <item><description><see cref="RequiredLeadHours"/> — "is this action still open?" (may I touch a
    /// request whose campus starts in 8 hours). A cutoff on the EXISTING schedule.</description></item>
    /// <item><description><see cref="MinScheduleLeadHours"/> — "may the visit be scheduled for this
    /// moment?" A floor on the PROPOSED schedule, so a Staff Leader always has three days' notice on
    /// anything they are asked to approve.</description></item>
    /// </list>
    ///
    /// Every path that files a schedule for approval measures against this one: create, pending-edit and
    /// resubmit. It is deliberately measured from the moment of the ACTION, never from when the request
    /// was first filed — a request that was valid on the 1st and resubmitted on the 9th gets the same
    /// three days of notice as a brand-new one.
    /// </summary>
    public const int MinScheduleLeadHours = 72;

    /// <summary>Campus states that are decided and have not started — where post-approval actions live.
    /// Both ASSIGNED (Host named, preparation not started) and BEFORE_VISIT (Host preparing) qualify:
    /// the actions this governs — a requester-side amendment, a host handover — depend on the campus
    /// having an owner and a future date, not on preparation being underway. Setup mutations are gated
    /// separately, on BEFORE_VISIT alone.</summary>
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
        instanceStatus is VisitInstanceStatuses.WaitingContactConfirmation
            or VisitInstanceStatuses.WaitingRequestApproval
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
            // Once a campus has been approved it has an OWNER, and that owner is the Host the Staff
            // Leader named in the same breath as the approval. Deciding a proposal about that campus is
            // the Host's job from then on: they are the person who has to make the changed visit happen,
            // who knows what the room and the schedule can still absorb, and who the requester is
            // already talking to. It used to be the campus Staff Leader, which meant the person running
            // the visit had to route every "can we move it an hour later" through someone who had handed
            // the campus over days ago. The Leader keeps approval of the campus itself, and the handover
            // of the Host role — not of the visit's day-to-day content.
            VisitMutationAction.ApproveAmendment
                => context.ViewerRelation == VisitViewerRelations.Host,
            VisitMutationAction.TransferHost
                => context.ViewerRelation == VisitViewerRelations.CampusLeader,
            // Editing a still-pending campus is the requester side's, and ALSO the Staff Leader's of
            // that campus: they are the approval authority for it, and the ordinary way a schedule gets
            // fixed before approval is the leader adjusting it with the guest rather than refusing the
            // whole request. Which of the two is asking still matters further down — the 72-hour
            // registration floor is overridable by the leader and by nobody else.
            VisitMutationAction.EditPendingCampus
                => context.ViewerRelation is VisitViewerRelations.Requester or VisitViewerRelations.CampusLeader,
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

            // Deliberately says NOTHING about the request status. A request whose siblings are already
            // approved sits at PARTIALLY_APPROVED, and that aggregate is precisely what must not decide
            // whether the campus still waiting for its own answer can be corrected. The only question is
            // the target campus's own state.
            VisitMutationAction.EditPendingCampus =>
                context.InstanceStatus == VisitInstanceStatuses.WaitingRequestApproval,

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

    /// <summary>
    /// The 72-hour registration floor for a schedule being FILED for approval — create, pending edit
    /// (request or campus) and resubmit all measure against this one, from the moment of the action.
    ///
    /// <para>
    /// Only reached when the schedule actually CHANGES. Time passing must not turn a request that was
    /// valid when it was filed into one that cannot be corrected: a guest fixing a typo three days
    /// before their visit is not proposing a new date, and refusing them because the old date is now
    /// inside the floor would leave the request frozen with the typo in it.
    /// </para>
    /// <para>
    /// The Staff Leader of the target campus may pass it deliberately. They are the person the floor
    /// protects — it exists so nobody is asked to approve a visit they have no time to prepare — so
    /// their informed "yes" is the rule being satisfied, not bypassed. Anyone else gets a plain refusal,
    /// including a Staff Leader of a DIFFERENT campus, and including a caller who simply sets the
    /// confirmation flag by hand: <paramref name="actorMayOverride"/> is decided by the handler from the
    /// actor's relation to THIS campus, never from the payload.
    /// </para>
    /// </summary>
    public static VisitScheduleLeadTimeDecision EvaluateScheduleLeadTime(
        DateTime proposedStart, DateTime now, bool actorMayOverride, bool overrideConfirmed)
    {
        var earliest = now.AddHours(MinScheduleLeadHours);
        if (proposedStart >= earliest)
            return new VisitScheduleLeadTimeDecision(true, false, earliest);
        if (!actorMayOverride)
            return new VisitScheduleLeadTimeDecision(false, false, earliest);
        return overrideConfirmed
            ? new VisitScheduleLeadTimeDecision(true, false, earliest)
            : new VisitScheduleLeadTimeDecision(false, true, earliest);
    }

    private static VisitMutationDecision Refused(DateTime cutoffAt, string reason) =>
        new(false, VisitMutationErrorCodes.LifecycleNotAllowed, reason, cutoffAt, RequiredLeadHours);

    private static string LifecycleReason(VisitMutationContext context) => context.InstanceStatus switch
    {
        // The mixed request. Whole-request editing is refused because a campus has already been decided,
        // and the reader needs to be told where the door actually is rather than only that this one is
        // shut — the campus still waiting IS editable, one card at a time.
        VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit
            when context.Action == VisitMutationAction.EditPendingRequest =>
            "Đơn đã có cơ sở được duyệt nên không thể sửa toàn đơn; hãy sửa riêng từng cơ sở đang chờ duyệt.",
        VisitInstanceStatuses.DuringVisit => "Chuyến thăm tại cơ sở này đang diễn ra nên không thể thay đổi.",
        VisitInstanceStatuses.AfterVisit or VisitInstanceStatuses.Closed =>
            "Chuyến thăm tại cơ sở này đã kết thúc nên không thể thay đổi.",
        VisitInstanceStatuses.Cancelled => "Cơ sở này đã bị hủy nên không thể thay đổi.",
        VisitInstanceStatuses.Rejected when context.Action != VisitMutationAction.ResubmitRejectedRequest =>
            "Cơ sở này đã bị từ chối; hãy dùng chức năng sửa và gửi lại đơn.",
        VisitInstanceStatuses.WaitingRequestApproval
            when context.Action is not (VisitMutationAction.EditPendingRequest or VisitMutationAction.EditPendingCampus) =>
            "Cơ sở này chưa được duyệt; hãy dùng chức năng sửa thông tin cơ sở đang chờ duyệt.",
        _ => "Trạng thái hiện tại không cho phép thực hiện thao tác này.",
    };
}
