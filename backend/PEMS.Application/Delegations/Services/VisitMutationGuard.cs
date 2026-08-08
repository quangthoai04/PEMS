using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Raised when <see cref="VisitMutationPolicy"/> refuses an action. Carries the facts the user needs to
/// understand the refusal — which campus, by when, and starting when — because "không thể sửa" on its
/// own leaves someone staring at a request whose deadline they cannot see.
/// </summary>
public sealed class VisitMutationRefusedException : BusinessRuleException
{
    public VisitMutationRefusedException(
        string message, string errorCode, string? campusName,
        DateTime? cutoffAt, DateTime? plannedStartAt, int requiredLeadHours)
        : base(message, errorCode)
    {
        CampusName = campusName;
        CutoffAt = cutoffAt;
        PlannedStartAt = plannedStartAt;
        RequiredLeadHours = requiredLeadHours;
    }

    public string? CampusName { get; }
    public DateTime? CutoffAt { get; }
    public DateTime? PlannedStartAt { get; }
    public int RequiredLeadHours { get; }
}

/// <summary>
/// The handler-side half of <see cref="VisitMutationPolicy"/>. The read model uses the policy to decide
/// what to OFFER; a command uses this to decide what to ACCEPT. Both ask the same question about the
/// same campus, so an action the UI showed is an action this admits, and an action reached by calling
/// the API directly is refused just the same.
/// </summary>
public static class VisitMutationGuard
{
    /// <summary>
    /// Request-level actions (pending edit, resubmit, request-scope safe edit). All-or-nothing across
    /// campuses: <paramref name="qualifies"/> must hold for EVERY campus, and the deadline comes from
    /// the earliest one. A campus that disagrees is the one reported, so the message names the campus
    /// that actually blocked the change.
    /// </summary>
    public static void EnsureRequestLevelAllowed(
        VisitMutationAction action,
        VisitRequest request,
        DateTime now,
        Func<VisitRequestCampus, bool> qualifies,
        string lifecycleErrorCode,
        IReadOnlyDictionary<ulong, string>? campusNames = null)
    {
        var instances = request.CampusInstances.ToList();
        if (instances.Count == 0)
            throw new BusinessRuleException("Đơn không có cơ sở nào.", lifecycleErrorCode);

        var dissenting = instances.Where(c => !qualifies(c)).OrderBy(c => c.PlannedStartAt).ToList();
        var governing = dissenting.Count > 0
            ? dissenting.FirstOrDefault(c => VisitMutationPolicy.BlocksRequestLevel(c.Status)) ?? dissenting[0]
            : instances.OrderBy(c => c.PlannedStartAt).First();

        EnsureAllowed(action, request.Status, governing, now,
            VisitViewerRelations.Requester, lifecycleErrorCode, campusNames);
    }

    /// <summary>
    /// Instance-level actions (instance-scope safe edit, amendment, host transfer). Only the target
    /// campus is consulted — a sibling that is under way says nothing about a campus that is still a
    /// week out, and blocking on it is how one campus's schedule froze another's paperwork.
    /// </summary>
    public static void EnsureAllowed(
        VisitMutationAction action,
        string requestStatus,
        VisitRequestCampus instance,
        DateTime now,
        string viewerRelation,
        string lifecycleErrorCode,
        IReadOnlyDictionary<ulong, string>? campusNames = null)
    {
        var decision = VisitMutationPolicy.Evaluate(new VisitMutationContext(
            action, requestStatus, instance.Status, instance.PlannedStartAt, now, viewerRelation));

        // There is no deadline waiver here, for any field. A `skipCutoff` flag used to let a
        // media-consent withdrawal through after the cutoff on privacy grounds, which made "until when
        // may I change this" depend on WHICH field the payload happened to carry — and let a change be
        // written into a campus whose Host had already briefed their team. Withdrawing consent late is
        // a conversation with the Host now, not a silent write.
        if (decision.Allowed)
            return;

        var campusName = campusNames is not null && campusNames.TryGetValue(instance.CampusId, out var n)
            ? n : null;

        if (decision.ErrorCode == VisitMutationErrorCodes.RelationNotAllowed)
            throw new ForbiddenException(decision.DisabledReason ?? "Bạn không có quyền thực hiện thao tác này.");

        // A cutoff refusal keeps its own stable code so the client can render the "hạn cuối / giờ bắt
        // đầu" block; a lifecycle refusal reuses the caller's domain code so existing clients that
        // already match on VISIT_REQUEST_NOT_EDITABLE keep working.
        var errorCode = decision.ErrorCode == VisitMutationErrorCodes.CutoffReached
            ? VisitMutationErrorCodes.CutoffReached
            : lifecycleErrorCode;

        var message = decision.ErrorCode == VisitMutationErrorCodes.CutoffReached && campusName is not null
            ? $"Không thể thực hiện thao tác cho {campusName}. {decision.DisabledReason}"
            : decision.DisabledReason ?? "Không thể thực hiện thao tác này.";

        throw new VisitMutationRefusedException(
            message, errorCode, campusName,
            decision.CutoffAt, instance.PlannedStartAt, decision.RequiredLeadHours);
    }
}
