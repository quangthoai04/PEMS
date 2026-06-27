using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.EmailActions;

public static class EmailTokenInvalidationHelper
{
    /// <summary>
    /// Invalidates all pending email action tokens for a specific target, e.g. when the participant is removed
    /// or a logistics item is cancelled, to prevent old emails from modifying the state.
    /// </summary>
    public static async Task InvalidatePendingEmailActionTokensAsync(
        IApplicationDbContext db,
        string targetType,
        ulong targetId,
        string reason,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var pendingTokens = await db.EmailActionTokens
            .Where(t => t.TargetType == targetType
                        && t.TargetId == targetId
                        && t.ResultStatus == EmailActionResultStatuses.Pending
                        && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        if (!pendingTokens.Any()) return;

        foreach (var t in pendingTokens)
        {
            t.ResultStatus = EmailActionResultStatuses.Invalid;
            t.UsedAt = now;
            t.ResultMessage = reason;
        }

        // We do not save changes here so that the caller can include it in their own transaction/SaveAsync.
    }

    /// <summary>
    /// Invalidates all pending email action tokens for an entire visit instance (participants and logistics items).
    /// </summary>
    public static async Task InvalidateTokensForVisitInstanceAsync(
        IApplicationDbContext db,
        ulong visitInstanceId,
        string reason,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var participantIds = await db.VisitParticipants
            .Where(p => p.VisitInstanceId == visitInstanceId)
            .Select(p => p.ParticipantId)
            .ToListAsync(cancellationToken);

        var logisticsIds = await db.VisitLogisticsItems
            .Where(l => l.VisitInstanceId == visitInstanceId)
            .Select(l => l.LogisticsItemId)
            .ToListAsync(cancellationToken);

        var pendingTokens = await db.EmailActionTokens
            .Where(t => t.ResultStatus == EmailActionResultStatuses.Pending && t.UsedAt == null
                        && ((t.TargetType == EmailActionTargetTypes.VisitParticipant && participantIds.Contains(t.TargetId))
                            || (t.TargetType == EmailActionTargetTypes.LogisticsItem && logisticsIds.Contains(t.TargetId))))
            .ToListAsync(cancellationToken);

        foreach (var t in pendingTokens)
        {
            t.ResultStatus = EmailActionResultStatuses.Invalid;
            t.UsedAt = now;
            t.ResultMessage = reason;
        }
    }
}
