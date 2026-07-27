using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The second way a person may reach a sent message: not because they were on it, but because they are
/// responsible for the thing it is about.
///
/// <para>
/// The rule is deliberately narrow and it is borrowed, not invented. A message attached to a visit is
/// readable by exactly the people who may already open that visit's process screen
/// (<see cref="VisitReminderAccess.CanView"/>: its host, the campus Staff Leader, HO) — the same
/// condition the visit-linked history endpoint has always applied. Nothing here reads a role directly,
/// so there is no path by which "is an HO" alone opens a message.
/// </para>
/// <para>
/// A manual message — <c>GENERAL</c> or <c>REPLY</c> — has no business object, so this grants nothing:
/// personal correspondence is readable by its correspondents and by nobody else, however senior.
/// </para>
/// </summary>
public interface ISentEmailObjectScope
{
    Task<bool> CanViewLinkedObjectAsync(
        string? relatedType, ulong? relatedId, CancellationToken cancellationToken = default);
}

public sealed class SentEmailObjectScope : ISentEmailObjectScope
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SentEmailObjectScope(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<bool> CanViewLinkedObjectAsync(
        string? relatedType, ulong? relatedId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is null) return false;
        if (relatedId is not { } id || string.IsNullOrWhiteSpace(relatedType)) return false;

        var instanceId = relatedType.Trim().ToUpperInvariant() switch
        {
            EmailActionTargetTypes.VisitParticipant => await _db.VisitParticipants
                .Where(p => p.ParticipantId == id)
                .Select(p => (ulong?)p.VisitInstanceId)
                .FirstOrDefaultAsync(cancellationToken),

            EmailActionTargetTypes.LogisticsItem => await _db.VisitLogisticsItems
                .Where(l => l.LogisticsItemId == id)
                .Select(l => (ulong?)l.VisitInstanceId)
                .FirstOrDefaultAsync(cancellationToken),

            VisitInstanceRelatedType => id,

            // GENERAL, REPLY and anything else: no business object, therefore no object scope.
            _ => null,
        };

        if (instanceId is not { } visitInstanceId) return false;

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == visitInstanceId, cancellationToken);

        return instance is not null && VisitReminderAccess.CanView(_currentUser, instance);
    }

    /// <summary>Matches the literal the reminder job writes on its messages.</summary>
    private const string VisitInstanceRelatedType = "VISIT_INSTANCE";
}
