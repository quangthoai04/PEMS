using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Shared;

// Both namespaces declare this name; the notifications written by the visit paths use the
// Application one, so that is what has to be matched here.
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Decides, per row and per reader, what should happen next.
///
/// Two things this deliberately does NOT do. It does not restate the status — a campus at ASSIGNED is
/// one fact, but the Host has preparation to finish there while the Staff Leader who approved it has
/// nothing to do, and one badge cannot say both. And it does not GUESS at completeness: whether the
/// preparation is finished, or whether a visit can be closed, is answered by the very conditions
/// <see cref="Commands.CompleteVisitStage.CompleteVisitStageCommandHandler"/> enforces, so the list can
/// never promise a step the command would then refuse.
///
/// Everything is batched over the page. Per-row queries here would mean six round-trips per row.
/// </summary>
public static class VisitNextTaskBuilder
{
    /// <summary>One row's facts, as the list handler already knows them.</summary>
    public sealed record Row(
        ulong VisitRequestId,
        ulong? VisitInstanceId,
        string RequestStatus,
        string? InstanceStatus,
        DateTime? PlannedStartAt,
        DateTime? PlannedEndAt,
        bool ViewerIsHost,
        bool ViewerLeadsCampus,
        IReadOnlyCollection<string> AllowedActions);

    private static readonly VisitNextTaskDto Nothing = new()
    {
        Code = VisitNextTaskCodes.None,
        Label = "Không có nhiệm vụ cần xử lý",
        RequiresAction = false,
        Scope = VisitActionScopes.Request,
    };

    /// <summary>Verdicts aligned to <paramref name="rows"/> by index.</summary>
    public static async Task<IReadOnlyList<VisitNextTaskDto>> BuildAsync(
        IApplicationDbContext db, ulong userId, IReadOnlyList<Row> rows, DateTime now, CancellationToken ct)
    {
        if (rows.Count == 0) return Array.Empty<VisitNextTaskDto>();

        // ── Which extra facts does THIS page actually need? Only fetch those. ──
        // BEFORE_VISIT only: "is the preparation finished?" is a question about a campus whose
        // preparation has begun. An ASSIGNED campus has nothing to be incomplete about yet.
        var prepInstanceIds = rows
            .Where(r => r.ViewerIsHost && r.VisitInstanceId is not null
                        && r.InstanceStatus == VisitInstanceStatus.BeforeVisit)
            .Select(r => r.VisitInstanceId!.Value).Distinct().ToList();

        var closingInstanceIds = rows
            .Where(r => r.ViewerIsHost && r.VisitInstanceId is not null
                        && r.InstanceStatus == VisitInstanceStatus.AfterVisit)
            .Select(r => r.VisitInstanceId!.Value).Distinct().ToList();

        var hostInstanceIds = rows
            .Where(r => r.ViewerIsHost && r.VisitInstanceId is not null)
            .Select(r => r.VisitInstanceId!.Value).Distinct().ToList();

        var leaderInstanceIds = rows
            .Where(r => r.ViewerLeadsCampus && r.VisitInstanceId is not null)
            .Select(r => r.VisitInstanceId!.Value).Distinct().ToList();

        var prepBlocked = await LoadPreparationBlockedAsync(db, prepInstanceIds, ct);
        var closeBlocked = await LoadCloseBlockedAsync(db, closingInstanceIds, ct);
        var freshHandover = await LoadFreshHandoverAsync(db, userId, hostInstanceIds, ct);
        var pendingAmendment = await LoadPendingAmendmentAsync(db, hostInstanceIds, ct);

        return rows.Select(r => Decide(r, now, prepBlocked, closeBlocked, freshHandover, pendingAmendment)).ToList();
    }

    private static VisitNextTaskDto Decide(
        Row r,
        DateTime now,
        IReadOnlySet<ulong> preparationIncomplete,
        IReadOnlySet<ulong> cannotCloseYet,
        IReadOnlySet<ulong> freshHandover,
        IReadOnlySet<ulong> pendingAmendment)
    {
        // A cancelled request has nothing left to ask of anyone, whatever its campuses still say.
        if (r.RequestStatus == VisitRequestStatuses.Cancelled) return Nothing;

        var instanceId = r.VisitInstanceId;
        var scope = instanceId is null ? VisitActionScopes.Request : VisitActionScopes.Instance;

        VisitNextTaskDto Task(string code, string label, bool requiresAction, DateTime? dueAt, string? actionCode)
            => new()
            {
                Code = code,
                Label = label,
                RequiresAction = requiresAction,
                Scope = scope,
                VisitInstanceId = instanceId,
                DueAt = dueAt,
                ActionCode = actionCode,
            };

        // 1. An action the backend has already granted outranks anything inferred from state — it is
        //    the one case where somebody is demonstrably being waited on.
        if (r.AllowedActions.Contains(VisitListActions.ApproveAndAssignHost))
            return Task(VisitNextTaskCodes.ReviewAndAssign,
                "Duyệt hoặc từ chối và phân công người phụ trách",
                true, r.PlannedStartAt, VisitListActions.ApproveAndAssignHost);

        // 2. A proposal waiting on this campus's Staff Leader. Ahead of the Host's own preparation
        //    work because a leader who also hosts should decide the change before preparing around it.
        if (instanceId is not null && pendingAmendment.Contains(instanceId.Value))
            return Task(VisitNextTaskCodes.ReviewAmendment,
                "Duyệt đề xuất thay đổi",
                true, r.PlannedStartAt, actionCode: null); // decided on the detail screen (§10)

        if (!r.ViewerIsHost || instanceId is null) return Nothing;

        // 3. The Host role landed here recently and has not been acknowledged.
        if (freshHandover.Contains(instanceId.Value))
            return Task(VisitNextTaskCodes.AcceptHostHandover,
                "Tiếp nhận bàn giao từ người phụ trách cũ",
                true, r.PlannedStartAt, VisitListActions.OpenHostProcess);

        // 4-5. The operational stage this Host is standing in.
        switch (r.InstanceStatus)
        {
            case VisitInstanceStatus.Assigned:
                // The Host has the campus and has not opened it. There is exactly one thing to do
                // here, and it is not "finish the preparation" — nothing can be prepared until they
                // start. Deliberately NOT folded in with BEFORE_VISIT below.
                return Task(VisitNextTaskCodes.StartPreparation,
                    "Bắt đầu chuẩn bị cho chuyến thăm",
                    true, r.PlannedStartAt, VisitListActions.StartPreparation);

            case VisitInstanceStatus.BeforeVisit:
                if (preparationIncomplete.Contains(instanceId.Value))
                    return Task(VisitNextTaskCodes.CompletePreparation,
                        "Hoàn thiện lịch trình và công tác chuẩn bị",
                        true, r.PlannedStartAt, VisitListActions.OpenHostProcess);

                // Preparation is done. Whether the Host is being ASKED to do something now depends on
                // the clock: the transition is refused until T-6h, so a "Xác nhận hoàn thành chuẩn bị"
                // task shown three days early is an action-required star on a row where the only
                // possible action returns 409. The waiting task says the same fact truthfully, and its
                // due date is the moment the window opens.
                // A campus with no planned start cannot have a window to wait for, so it falls through
                // to the ordinary confirm task rather than being frozen by a rule it cannot satisfy.
                var plannedStart = r.PlannedStartAt;
                if (plannedStart.HasValue
                    && !VisitStageTransitionPolicy.CanAdvanceBeforeToDuring(now, plannedStart.Value))
                    return Task(VisitNextTaskCodes.WaitStartVisitWindow,
                        "Chờ đến thời điểm có thể bắt đầu tiếp khách",
                        false, VisitStageTransitionPolicy.StartVisitAvailableAt(plannedStart.Value),
                        VisitListActions.OpenHostProcess);

                return Task(VisitNextTaskCodes.ConfirmPreparation,
                    "Xác nhận hoàn thành chuẩn bị",
                    true, r.PlannedStartAt, VisitListActions.OpenHostProcess);

            case VisitInstanceStatus.DuringVisit:
                return Task(VisitNextTaskCodes.RunReception,
                    "Theo dõi và cập nhật quá trình tiếp đón",
                    true, r.PlannedEndAt, VisitListActions.OpenHostProcess);

            case VisitInstanceStatus.AfterVisit:
                // The planned end has to have passed before closing is even offered — the same first
                // condition the close command checks, and the only one that depends on the clock.
                var tooEarlyToClose = r.PlannedEndAt is { } end && now < end;
                return tooEarlyToClose || cannotCloseYet.Contains(instanceId.Value)
                    ? Task(VisitNextTaskCodes.CompletePostVisit,
                        "Hoàn thiện biên bản và hồ sơ",
                        true, r.PlannedEndAt, VisitListActions.OpenHostProcess)
                    : Task(VisitNextTaskCodes.CloseVisit,
                        "Kiểm tra và đóng đoàn",
                        true, r.PlannedEndAt, VisitListActions.OpenHostProcess);

            default:
                return Nothing;
        }
    }

    // ── Fact loaders. Each mirrors the guard the matching command enforces. ──────────────────────

    /// <summary>
    /// Campuses whose preparation is demonstrably unfinished — the exact two blockers
    /// <c>CompleteVisitStage(before)</c> rejects on: an unanswered invitation, or an empty agenda.
    /// A campus NOT in this set is one the command would let through, which is what makes
    /// "Xác nhận hoàn thành chuẩn bị" safe to show.
    /// </summary>
    private static async Task<IReadOnlySet<ulong>> LoadPreparationBlockedAsync(
        IApplicationDbContext db, List<ulong> instanceIds, CancellationToken ct)
    {
        var blocked = new HashSet<ulong>();
        if (instanceIds.Count == 0) return blocked;

        var withAgenda = (await db.VisitAgendas.AsNoTracking()
            .Where(a => instanceIds.Contains(a.VisitInstanceId))
            .Select(a => a.VisitInstanceId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        var withPendingInvite = (await db.VisitParticipants.AsNoTracking()
            .Where(p => instanceIds.Contains(p.VisitInstanceId) && p.Status == ParticipantStatuses.Invited)
            .Select(p => p.VisitInstanceId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        foreach (var id in instanceIds)
            if (!withAgenda.Contains(id) || withPendingInvite.Contains(id))
                blocked.Add(id);

        return blocked;
    }

    /// <summary>
    /// Campuses that still fail at least one close condition, mirroring <c>CompleteVisitStage(after)</c>:
    /// the planned end has not passed, logistics are still open, a handover is unsigned, a minute action
    /// item is still outstanding, or there is neither a published article nor a Host waiver.
    /// </summary>
    private static async Task<IReadOnlySet<ulong>> LoadCloseBlockedAsync(
        IApplicationDbContext db, List<ulong> instanceIds, CancellationToken ct)
    {
        var blocked = new HashSet<ulong>();
        if (instanceIds.Count == 0) return blocked;

        var openLogistics = (await db.VisitLogisticsItems.AsNoTracking()
            .Where(l => instanceIds.Contains(l.VisitInstanceId)
                        && l.Status != LogisticsItemStatus.Done
                        && l.Status != LogisticsItemStatus.Rejected
                        && l.Status != LogisticsItemStatus.Declined
                        && l.Status != LogisticsItemStatus.Cancelled)
            .Select(l => l.VisitInstanceId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
        blocked.UnionWith(openLogistics);

        // Handovers hang off the logistics item, so the instance has to be carried across in memory
        // (a correlated subquery on this shape does not translate on Pomelo).
        var logisticsOfInstance = await db.VisitLogisticsItems.AsNoTracking()
            .Where(l => instanceIds.Contains(l.VisitInstanceId))
            .Select(l => new { l.LogisticsItemId, l.VisitInstanceId })
            .ToListAsync(ct);
        if (logisticsOfInstance.Count > 0)
        {
            var logisticsIds = logisticsOfInstance.Select(l => l.LogisticsItemId).ToList();
            var unsigned = await db.VisitLogisticsItemHandovers.AsNoTracking()
                .Where(h => logisticsIds.Contains(h.LogisticsItemId)
                            && (h.BorrowerSignedAt == null || h.ProviderSignedAt == null))
                .Select(h => h.LogisticsItemId)
                .ToListAsync(ct);
            var unsignedSet = unsigned.ToHashSet();
            foreach (var l in logisticsOfInstance)
                if (unsignedSet.Contains(l.LogisticsItemId))
                    blocked.Add(l.VisitInstanceId);
        }

        var minutesOfInstance = await db.Minutes.AsNoTracking()
            .Where(m => instanceIds.Contains(m.VisitInstanceId))
            .Select(m => new { m.MinutesId, m.VisitInstanceId })
            .ToListAsync(ct);
        if (minutesOfInstance.Count > 0)
        {
            var minuteIds = minutesOfInstance.Select(m => m.MinutesId).ToList();
            var openItems = (await db.MinuteActionItems.AsNoTracking()
                .Where(ai => minuteIds.Contains(ai.MinutesId) && ai.Status != "DONE" && ai.Status != "CANCELLED")
                .Select(ai => ai.MinutesId)
                .ToListAsync(ct)).ToHashSet();
            foreach (var m in minutesOfInstance)
                if (openItems.Contains(m.MinutesId))
                    blocked.Add(m.VisitInstanceId);
        }

        var newsWaived = (await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => instanceIds.Contains(c.VisitInstanceId) && c.NewsNotRequired)
            .Select(c => c.VisitInstanceId)
            .ToListAsync(ct)).ToHashSet();
        var newsPublished = (await db.News.AsNoTracking()
            .Where(n => n.VisitInstanceId != null
                        && instanceIds.Contains(n.VisitInstanceId.Value)
                        && n.Status == NewsConstants.Status.Published)
            .Select(n => n.VisitInstanceId!.Value)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
        foreach (var id in instanceIds)
            if (!newsWaived.Contains(id) && !newsPublished.Contains(id))
                blocked.Add(id);

        // The remaining close condition — "the planned end has passed" — is the only clock-dependent
        // one and is applied by Decide, which already holds both the row's end time and now.
        return blocked;
    }

    /// <summary>
    /// Campuses where the Host role arrived recently and the new Host has not read the notice yet.
    /// The transfer command marks that one notification action-required precisely so the recipient
    /// can be told there is a handover to pick up; nothing else in the schema records "this is new to you".
    /// </summary>
    private static async Task<IReadOnlySet<ulong>> LoadFreshHandoverAsync(
        IApplicationDbContext db, ulong userId, List<ulong> instanceIds, CancellationToken ct)
    {
        if (instanceIds.Count == 0) return new HashSet<ulong>();
        return (await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId
                        && n.VisitInstanceId != null
                        && instanceIds.Contains(n.VisitInstanceId.Value)
                        && n.NotificationType == NotificationTypes.HostAssigned
                        && n.IsActionRequired
                        && !n.IsRead
                        && n.ArchivedAt == null)
            .Select(n => n.VisitInstanceId!.Value)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
    }

    private static async Task<IReadOnlySet<ulong>> LoadPendingAmendmentAsync(
        IApplicationDbContext db, List<ulong> instanceIds, CancellationToken ct)
    {
        if (instanceIds.Count == 0) return new HashSet<ulong>();
        return (await db.VisitInstanceAmendments.AsNoTracking()
            .Where(a => instanceIds.Contains(a.VisitInstanceId) && a.Status == AmendmentStatuses.PendingApproval)
            .Select(a => a.VisitInstanceId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
    }
}

/// <summary>
/// Action codes the MANAGEMENT LIST emits. Named here so the next-task builder and the list handler
/// cannot drift apart on a string literal.
/// </summary>
public static class VisitListActions
{
    public const string ViewDetail = "VIEW_DETAIL";
    public const string ApproveAndAssignHost = "APPROVE_AND_ASSIGN_HOST";
    public const string CampusReject = "CAMPUS_REJECT";
    public const string OpenHostProcess = "OPEN_HOST_PROCESS";
    /// <summary>Current Host opens the preparation window on an ASSIGNED campus (ASSIGNED → BEFORE_VISIT).</summary>
    public const string StartPreparation = "START_PREPARATION";
    public const string TransferHost = VisitFormActions.TransferHost;
}
