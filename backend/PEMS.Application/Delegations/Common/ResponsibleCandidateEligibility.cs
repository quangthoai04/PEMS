using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Who is eligible to be the "responsible person" of an agenda item or a meeting-minutes action
/// item: the instance's current host, plus ACCEPTED participants in a supporting role (IC_SUPPORT /
/// DEPT_SUPPORT / STUDENT). Both roles map to "staff / staff leader / dept / dept leader / student"
/// account types — a guest is never eligible. Shared by
/// <see cref="Queries.GetAgendaResponsibleCandidates.GetAgendaResponsibleCandidatesQueryHandler"/>
/// (candidate list for a picker) and <see cref="Minutes.SaveMinutesCommandHandler"/> (server-side
/// validation of an assignee id) so the eligibility SQL never drifts between the two.
/// </summary>
public static class ResponsibleCandidateEligibility
{
    public static readonly string[] AllowedParticipantRoles =
    {
        ParticipantRoles.IcSupport, ParticipantRoles.DeptSupport, ParticipantRoles.Student,
    };

    public sealed record EligibleUser(ulong UserId, string FullName, string Email, string? ParticipantRole, bool IsMainHost);

    /// <summary>Host (if ACTIVE) listed first, then ACCEPTED supporting participants (ACTIVE users
    /// only), deduped by user id.</summary>
    public static async Task<List<EligibleUser>> GetEligibleUsersAsync(
        IApplicationDbContext db, ulong visitInstanceId, ulong? currentHostUserId, CancellationToken ct)
    {
        var result = new List<EligibleUser>();
        var seen = new HashSet<ulong>();

        if (currentHostUserId.HasValue)
        {
            var host = await db.Users
                .Where(u => u.UserId == currentHostUserId.Value && u.Status == UserStatuses.Active)
                .Select(u => new { u.UserId, u.FullName, u.Email })
                .FirstOrDefaultAsync(ct);
            if (host != null)
            {
                result.Add(new EligibleUser(host.UserId, host.FullName, host.Email, ParticipantRoles.IcHost, true));
                seen.Add(host.UserId);
            }
        }

        var participants = await (
            from p in db.VisitParticipants
            join u in db.Users on p.UserId equals u.UserId
            where p.VisitInstanceId == visitInstanceId
                  && p.Status == ParticipantStatuses.Accepted
                  && AllowedParticipantRoles.Contains(p.ParticipantRole)
                  && u.Status == UserStatuses.Active
            select new { u.UserId, u.FullName, u.Email, p.ParticipantRole }
        ).ToListAsync(ct);

        foreach (var p in participants)
        {
            if (!seen.Add(p.UserId)) continue;
            result.Add(new EligibleUser(p.UserId, p.FullName, p.Email, p.ParticipantRole, false));
        }

        return result;
    }

    /// <summary>Convenience for validation call sites that only need the set of eligible ids.</summary>
    public static async Task<HashSet<ulong>> GetEligibleUserIdsAsync(
        IApplicationDbContext db, ulong visitInstanceId, ulong? currentHostUserId, CancellationToken ct)
    {
        var users = await GetEligibleUsersAsync(db, visitInstanceId, currentHostUserId, ct);
        return users.Select(u => u.UserId).ToHashSet();
    }
}
