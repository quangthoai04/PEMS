using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Minutes;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Builds the auto-fill snapshot rows for <c>minute_participants</c> when a biên bản is first created
/// (and again for the "đồng bộ người mới" sync). Rules (Phần 1):
///  1. Host chính — from <c>visit_request_campuses.current_host_user_id</c>.
///  2. Internal participants — from <c>visit_participants</c> that are confirmed-to-attend
///     (status ACCEPTED, excluding the host row); ASSIGNED/INVITED/DECLINED/REMOVED are skipped.
///  3. Guests — every guest LINKED TO THIS CAMPUS INSTANCE via <c>visit_instance_guest_members</c>
///     (per-campus v2; a sibling instance's guests of the same request are never included — each
///     campus keeps its own independent, copy-on-write member rows).
/// De-dup is by (user_id) / (guest_member_id) against what already exists, so it is idempotent and
/// append-only: it never resurrects or overwrites rows the Host has edited/checked.
///
/// <para>
/// Guests carry a SECOND de-dup key on top of the id. The same person can legitimately hold two
/// <c>visit_guest_members</c> rows — one as GUEST, one as EXTERNAL_SUPPORT — because member_type
/// belongs to the delegation form, not to the person. Two ids meant one person listed twice in the
/// biên bản, which is wrong on the face of it: a meeting record shows each attendee once. Identity
/// here is the normalised (full name, organization, job title, nationality) tuple and nothing else —
/// no fuzzy matching, no guessing when a field is blank — and when it collides the GUEST row wins.
/// INTERNAL rows are never compared against guests: same name, different source, different person.
/// </para>
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

        // 3. Guests linked to THIS campus instance (per-campus v2 — a sibling instance of the same
        //    multi-campus request keeps its own copy-on-write member rows, so scoping by
        //    visit_request_id alone would double-count a guest that exists on both campuses).
        var guests = await db.VisitInstanceGuestMembers
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .Join(db.VisitGuestMembers, l => l.GuestMemberId, g => g.GuestMemberId,
                (l, g) => new { l.DisplayOrder, Member = g })
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Member.GuestMemberId)
            .ToListAsync(ct);

        // The identities the biên bản already covers. An existing row was written from a guest record,
        // so the person it stands for is that record's identity — resolved from the source rather than
        // from the snapshot, which the Host may have since edited.
        var seenGuestIdentityKeys = seenGuestIds.Count == 0
            ? new HashSet<string>()
            : (await db.VisitGuestMembers
                    .Where(g => seenGuestIds.Contains(g.GuestMemberId))
                    .ToListAsync(ct))
                .Select(GuestIdentityKey)
                .ToHashSet();

        // One row per identity BEFORE anything is added: GUEST outranks EXTERNAL_SUPPORT, then the
        // delegation's own ordering, then the id — so the choice is the same on every sync.
        var canonicalGuests = guests
            .GroupBy(x => GuestIdentityKey(x.Member))
            .Select(g => g
                .OrderBy(x => x.Member.MemberType == GuestMemberType.ExternalSupport ? 1 : 0)
                .ThenBy(x => x.DisplayOrder)
                .ThenBy(x => x.Member.GuestMemberId)
                .First())
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Member.GuestMemberId)
            .ToList();

        foreach (var row in canonicalGuests)
        {
            var g = row.Member;
            if (seenGuestIds.Contains(g.GuestMemberId)) continue;
            var identityKey = GuestIdentityKey(g);
            // Already in the biên bản under the OTHER member_type: adding the second row would list
            // the same person twice, which is exactly what the ids alone cannot see.
            if (seenGuestIdentityKeys.Contains(identityKey)) continue;
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
            seenGuestIdentityKeys.Add(identityKey);
        }

        return result;
    }

    /// <summary>
    /// One field of a guest identity, compared the way a person reading the list would: leading and
    /// trailing space, repeated space between words, and letter case are typing, not identity. Nothing
    /// beyond that — diacritics are NOT stripped, because "Vân" and "Van" are two different names.
    /// </summary>
    private static string NormalizeIdentityPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    /// <summary>
    /// Two guest rows are the same person only when ALL FOUR business fields match. member_type,
    /// display_order and guest_member_id are deliberately absent: they are what differs between the
    /// duplicate rows this is meant to collapse.
    /// </summary>
    private static string GuestIdentityKey(VisitGuestMember guest) => string.Join(
        "|",
        NormalizeIdentityPart(guest.FullName),
        NormalizeIdentityPart(guest.Organization),
        NormalizeIdentityPart(guest.JobTitle),
        NormalizeIdentityPart(guest.Nationality));

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
