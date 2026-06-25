using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Minutes;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Builds the auto-fill snapshot rows for <c>minute_participants</c> when a biên bản is first created
/// (and again for the "đồng bộ người mới" sync). Rules (Phần 1):
///  1. Host chính — from <c>visit_request_campuses.current_host_user_id</c>.
///  2. Internal participants — from <c>visit_participants</c> that are confirmed-to-attend
///     (status ACCEPTED, excluding the host row); ASSIGNED/INVITED/DECLINED/REMOVED are skipped.
///  3. Guests — every row of <c>visit_guest_members</c> for the request (no per-guest confirm status exists).
/// De-dup is purely by (user_id) / (guest_member_id) against what already exists, so it is idempotent
/// and append-only: it never resurrects or overwrites rows the Host has edited/checked.
/// </summary>
internal static class MinuteAutoFill
{
    private const string AttendanceDefault = "ABSENT";

    /// <summary>
    /// Computes the participant rows that SHOULD exist for this minutes but are not present in
    /// <paramref name="existing"/>. Rows are returned transient (not added to the context); the
    /// caller decides whether to persist them (create) or merely return them as candidates (sync).
    /// </summary>
    public static async Task<List<MinuteParticipant>> ComputeNewRowsAsync(
        IApplicationDbContext db,
        VisitRequestCampus instance,
        IReadOnlyCollection<MinuteParticipant> existing,
        ulong minutesId,
        DateTime now,
        CancellationToken ct)
    {
        var seenUserIds = existing.Where(p => p.UserId != null).Select(p => p.UserId!.Value).ToHashSet();
        var seenGuestIds = existing.Where(p => p.GuestMemberId != null).Select(p => p.GuestMemberId!.Value).ToHashSet();
        uint order = existing.Count == 0 ? 0u : existing.Max(p => p.DisplayOrder);

        var result = new List<MinuteParticipant>();

        // 1. Host chính.
        if (instance.CurrentHostUserId is ulong hostId && !seenUserIds.Contains(hostId))
        {
            var host = await db.Users
                .Include(u => u.Department).Include(u => u.PrimaryCampus)
                .FirstOrDefaultAsync(u => u.UserId == hostId, ct);
            if (host != null)
            {
                result.Add(NewInternal(minutesId, host, "Host", now, ++order));
                seenUserIds.Add(hostId);
            }
        }

        // 2. Internal participants confirmed to attend.
        var parts = await db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                && !p.IsHost
                && p.Status == ParticipantStatuses.Accepted)
            .OrderBy(p => p.ParticipantId)
            .ToListAsync(ct);

        var neededUserIds = parts.Select(p => p.UserId).Where(id => !seenUserIds.Contains(id)).Distinct().ToList();
        var users = neededUserIds.Count == 0
            ? new List<User>()
            : await db.Users.Include(u => u.Department).Include(u => u.PrimaryCampus)
                .Where(u => neededUserIds.Contains(u.UserId)).ToListAsync(ct);
        var userMap = users.ToDictionary(u => u.UserId);

        foreach (var p in parts)
        {
            if (seenUserIds.Contains(p.UserId)) continue;
            if (!userMap.TryGetValue(p.UserId, out var u)) continue;
            result.Add(NewInternal(minutesId, u, RoleLabel(p.ParticipantRole), now, ++order));
            seenUserIds.Add(p.UserId);
        }

        // 3. Guests from the request (official delegation list).
        var guests = await db.VisitGuestMembers
            .Where(g => g.VisitRequestId == instance.VisitRequestId)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.GuestMemberId)
            .ToListAsync(ct);

        foreach (var g in guests)
        {
            if (seenGuestIds.Contains(g.GuestMemberId)) continue;
            result.Add(new MinuteParticipant
            {
                MinutesId = minutesId,
                UserId = null,
                GuestMemberId = g.GuestMemberId,
                FullNameSnapshot = g.FullName,
                RoleSnapshot = g.JobTitle,
                OrganizationSnapshot = g.Organization,
                EmailSnapshot = null,
                AttendanceStatus = AttendanceDefault,
                DisplayOrder = ++order,
                CreatedAt = now,
            });
            seenGuestIds.Add(g.GuestMemberId);
        }

        return result;
    }

    private static MinuteParticipant NewInternal(ulong minutesId, User u, string role, DateTime now, uint order)
        => new()
        {
            MinutesId = minutesId,
            UserId = u.UserId,
            GuestMemberId = null,
            FullNameSnapshot = u.FullName,
            RoleSnapshot = role,
            OrganizationSnapshot = OrgLabel(u),
            EmailSnapshot = u.Email,
            AttendanceStatus = AttendanceDefault,
            DisplayOrder = order,
            CreatedAt = now,
        };

    private static string? OrgLabel(User u) => u.Department?.Name ?? u.PrimaryCampus?.Name;

    private static string RoleLabel(string participantRole) => participantRole switch
    {
        "IC_HOST" => "IC Host",
        "IC_SUPPORT" => "Cán bộ IC",
        "DEPT_SUPPORT" => "Cán bộ phòng ban",
        "STUDENT" => "Sinh viên hỗ trợ",
        _ => participantRole,
    };
}
