using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.ViewMyVisitInvitations;

/// <summary>Flat row materialised from the invitation query before in-memory enrichment.</summary>
public sealed class VisitInvitationFlat
{
    public ulong ParticipantId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public ulong VisitRequestId { get; set; }
    public ulong CampusId { get; set; }
    public string ParticipantRole { get; set; } = default!;
    public string Status { get; set; } = default!;
    public ulong? InvitedByUserId { get; set; }
    public DateTime? InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Note { get; set; }
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public string? RequestCode { get; set; }
    public string? DelegationName { get; set; }
    public string? OrganizationName { get; set; }
    public string? Purpose { get; set; }
    public string? WorkingContent { get; set; }
}

/// <summary>Shared mapping + batched enrichment for invitation DTOs (list and detail).</summary>
public static class VisitInvitationProjection
{
    public static VisitInvitationDto ToDto(VisitInvitationFlat r)
    {
        var actions = new List<string> { "VIEW_DETAIL" };
        if (r.Status == ParticipantStatuses.Invited)
        {
            actions.Add("ACCEPT_INVITATION");
            actions.Add("DECLINE_INVITATION");
        }

        return new VisitInvitationDto
        {
            ParticipantId = r.ParticipantId,
            VisitRequestId = r.VisitRequestId,
            VisitInstanceId = r.VisitInstanceId,
            RequestCode = r.RequestCode,
            DelegationName = r.DelegationName,
            OrganizationName = r.OrganizationName,
            CampusId = r.CampusId,
            ParticipantRole = r.ParticipantRole,
            Status = r.Status,
            PlannedStartAt = r.PlannedStartAt,
            PlannedEndAt = r.PlannedEndAt,
            Purpose = r.Purpose,
            WorkingContent = r.WorkingContent,
            InvitedByUserId = r.InvitedByUserId,
            InvitedAt = r.InvitedAt,
            RespondedAt = r.RespondedAt,
            Note = r.Note,
            AllowedActions = actions,
        };
    }

    /// <summary>Resolves campus + inviter display names in batched lookups (no per-row subqueries).</summary>
    public static async Task EnrichAsync(
        IApplicationDbContext db, List<VisitInvitationDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return;

        var campusIds = items.Select(i => i.CampusId).Distinct().ToList();
        var inviterIds = items.Where(i => i.InvitedByUserId.HasValue)
            .Select(i => i.InvitedByUserId!.Value).Distinct().ToList();

        var campusNames = await db.Campuses
            .Where(c => campusIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, ct);
        var inviterNames = inviterIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await db.Users.Where(u => inviterIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        foreach (var i in items)
        {
            if (campusNames.TryGetValue(i.CampusId, out var cn)) i.CampusName = cn;
            if (i.InvitedByUserId.HasValue && inviterNames.TryGetValue(i.InvitedByUserId.Value, out var inv))
                i.InvitedByName = inv;
        }
    }
}
