using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

/// <summary>
/// One resolved per-campus processing decision for the authenticated v2 create.
/// <see cref="CampusCode"/> is UPPER-cased; <see cref="Mode"/> is a <see cref="CampusSubmissionModes"/> value.
/// </summary>
public sealed record V2CampusProcessingPlan(
    string CampusCode, string Mode, ulong? HostUserId, bool ConfirmedHostConflict);

/// <summary>
/// Actor facts (revalidated from the authenticated session + DB) needed to authorize a processing plan,
/// captured as plain data so the role × mode × campus matrix is unit-testable without a database.
/// </summary>
public sealed record V2ProcessingActor(
    bool IsVisitor,
    bool IsRegularStaff,
    bool IsStaffLeader,
    string? OwnCampusCode,
    bool OwnDepartmentIsIc,
    ulong ActorUserId);

/// <summary>
/// The server-derived persistence effect of one authorized plan: exactly what the handler writes onto the
/// campus instance. Never influenced by client-sent decision/audit fields.
/// </summary>
public sealed record V2CampusDecision(
    ulong HostUserId,
    string DecisionActorRole,
    string DecisionSource,
    bool IsLeaderAssignOther);

/// <summary>
/// Per-campus processing authorization for the AUTHENTICATED per-campus v2 create — the same narrow
/// "submit may process my OWN campus" exception the v1 authenticated create allows
/// (<c>CreateAuthenticatedVisitRequestCommandHandler</c>), lifted into a pure, testable form:
///   • Visitor / any non-internal actor  → every campus SEND_FOR_REVIEW; any direct mode/host is rejected.
///   • Regular IC Staff                  → may SELF_HOST their OWN primary campus only.
///   • Staff Leader                      → may SELF_HOST or ASSIGN_HOST (same-campus IC Staff) on their OWN campus.
/// Every other campus always routes to that campus's Staff Leader. The DB-only "is this a valid host
/// candidate" check is done by the handler; everything else here is pure and throws the SAME domain
/// exceptions/error codes as the v1 flow so both report identically.
/// </summary>
public static class V2CampusProcessingRules
{
    /// <summary>
    /// Extracts the DIRECT-processing plans from the create form. A campus whose <c>Processing</c> is null,
    /// blank, or SEND_FOR_REVIEW with no host is treated as "route to the campus Staff Leader" and is NOT
    /// returned. Every processing entry must reference one of the selected campuses.
    /// </summary>
    public static IReadOnlyList<V2CampusProcessingPlan> BuildPlans(VisitRequestFormDataV2 form)
    {
        var selected = form.CampusVisits
            .Select(c => c.CampusId?.Trim().ToUpperInvariant() ?? string.Empty)
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var plans = new List<V2CampusProcessingPlan>();
        foreach (var cv in form.CampusVisits)
        {
            var p = cv.Processing;
            if (p is null) continue;

            var code = cv.CampusId?.Trim().ToUpperInvariant() ?? string.Empty;
            var mode = string.IsNullOrWhiteSpace(p.Mode)
                ? CampusSubmissionModes.SendForReview
                : p.Mode.Trim().ToUpperInvariant();

            // "Send for review / process later" with no host is the default routing — nothing to apply.
            if (mode == CampusSubmissionModes.SendForReview && p.HostUserId is null) continue;

            if (!selected.Contains(code))
                throw new BusinessRuleException(
                    "Lựa chọn xử lý tham chiếu cơ sở không nằm trong danh sách cơ sở đã chọn.",
                    VisitRequestErrorCodes.DirectModeCampusNotSelected);

            plans.Add(new V2CampusProcessingPlan(code, mode, p.HostUserId, p.ConfirmedHostConflict));
        }
        return plans;
    }

    /// <summary>
    /// Pure role × mode × campus authorization for the direct-processing plans. Throws the same domain
    /// exceptions as the v1 authenticated create. The handler still validates the ASSIGN_HOST candidate
    /// against the DB (active, same-campus, IC Staff) after this passes.
    /// </summary>
    public static void ValidateShape(V2ProcessingActor actor, IReadOnlyList<V2CampusProcessingPlan> plans)
    {
        if (plans.Count == 0) return;

        // A Visitor (or any non-internal role) can never direct-process nor carry a host.
        if (actor.IsVisitor || (!actor.IsRegularStaff && !actor.IsStaffLeader))
            throw new BusinessRuleException(
                "Đơn của Visitor luôn chờ Staff Leader từng cơ sở duyệt — không thể tự duyệt hoặc gán host.",
                VisitRequestErrorCodes.InvalidCampusSubmissionMode);

        var ownCode = actor.OwnCampusCode?.Trim().ToUpperInvariant();

        foreach (var plan in plans)
        {
            var isOwnCampus = ownCode != null
                && string.Equals(plan.CampusCode, ownCode, StringComparison.OrdinalIgnoreCase);
            if (!isOwnCampus)
                throw new ForbiddenException(
                    "Bạn chỉ được xử lý trực tiếp cơ sở của chính mình; cơ sở khác luôn chờ Staff Leader cơ sở đó duyệt.");

            switch (plan.Mode)
            {
                case CampusSubmissionModes.AssignHost:
                    if (!actor.IsStaffLeader)
                        throw new ForbiddenException("Chỉ Staff Leader mới được gán host cho người khác.");
                    if (plan.HostUserId is null)
                        throw new BusinessRuleException(
                            "Chế độ gán host phải chọn host cụ thể.", VisitRequestErrorCodes.InvalidHostCandidate);
                    break;

                case CampusSubmissionModes.SelfHost:
                    if (plan.HostUserId.HasValue && plan.HostUserId.Value != actor.ActorUserId)
                        throw new ForbiddenException("Bạn không được gán host là người khác trong chế độ tự nhận host.");
                    if (actor.IsRegularStaff && !actor.OwnDepartmentIsIc)
                        throw new BusinessRuleException(
                            "Chỉ IC Staff thuộc phòng IC của cơ sở mới được tự nhận host.",
                            VisitRequestErrorCodes.SelfHostNotEligible);
                    break;

                default:
                    throw new BusinessRuleException(
                        "Chế độ xử lý không hợp lệ.", VisitRequestErrorCodes.InvalidCampusSubmissionMode);
            }
        }
    }

    /// <summary>
    /// Derives the persisted decision for an already-authorized plan. A Leader who picks THEMSELF in
    /// ASSIGN_HOST is equivalent to SELF_HOST (same effect as the v1 flow), so the source stays
    /// INTERNAL_SELF_HOST and no host-assignment notification is raised.
    /// </summary>
    public static V2CampusDecision Derive(V2ProcessingActor actor, V2CampusProcessingPlan plan)
    {
        var isLeaderAssignOther = plan.Mode == CampusSubmissionModes.AssignHost
            && plan.HostUserId.HasValue && plan.HostUserId.Value != actor.ActorUserId;

        return new V2CampusDecision(
            HostUserId: isLeaderAssignOther ? plan.HostUserId!.Value : actor.ActorUserId,
            DecisionActorRole: actor.IsStaffLeader ? DecisionActorRole.StaffLeader : DecisionActorRole.Staff,
            DecisionSource: isLeaderAssignOther
                ? DecisionSources.InternalLeaderAssign
                : DecisionSources.InternalSelfHost,
            IsLeaderAssignOther: isLeaderAssignOther);
    }
}
