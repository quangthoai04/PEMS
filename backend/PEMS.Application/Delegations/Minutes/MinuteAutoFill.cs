using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
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
///  3. Guests — every guest LINKED TO THIS CAMPUS INSTANCE via <c>visit_instance_guest_members</c>
///     (per-campus v2; a sibling instance's guests of the same request are never included — each
///     campus keeps its own independent, copy-on-write member rows).
///  4. Đầu mối đoàn khách — the campus's operational contact, IF none of the above already covers
///     them. See <see cref="AddOperationalContactAsync"/> for why this step has to exist.
/// De-dup is by (user_id) / (guest_member_id) against what already exists, so it is idempotent and
/// append-only: it never resurrects or overwrites rows the Host has edited/checked. Those two ids
/// cannot see ACROSS the two sources, though — one person invited as internal support and also listed
/// among the delegation's members holds a user_id in one list and a guest_member_id in the other, and
/// used to land in the biên bản twice. A normalised name + role + organisation fingerprint
/// (<see cref="PersonIdentity.Key"/>) closes that gap: a guest whose fingerprint already belongs to an
/// internal row is skipped, because a user_id is the stronger identity. Name alone is never enough to
/// merge two people — that rule lives in <see cref="PersonIdentity"/> and is shared with the create /
/// edit services, so the biên bản and the request form can never disagree about who is who.
/// </summary>
internal static class MinuteAutoFill
{
    private const string AttendanceDefault = "ABSENT";

    /// <summary>Role label for a contact who is not otherwise in the delegation or the support list.</summary>
    private const string OperationalContactRole = "Đầu mối đoàn khách";

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
        // Fingerprints of the INTERNAL rows only (existing + the ones added below). Guests are matched
        // against these so a person present in both lists is filled in once, as the internal row; guest
        // rows never de-duplicate each other, so two genuinely different members of a delegation who
        // happen to share a name/role/organisation both stay.
        var internalIdentityKeys = existing
            .Where(p => p.UserId != null)
            .Select(p => PersonIdentity.Key(p.FullNameSnapshot, p.RoleSnapshot, p.OrganizationSnapshot))
            .Where(k => k.Length > 0)
            .ToHashSet();
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
                var row = NewInternal(minutesId, host, "Host", now, ++order);
                result.Add(row);
                seenUserIds.Add(hostId);
                Remember(internalIdentityKeys, row);
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
            var row = NewInternal(minutesId, u, RoleLabel(p.ParticipantRole), now, ++order);
            result.Add(row);
            seenUserIds.Add(p.UserId);
            Remember(internalIdentityKeys, row);
        }

        // 3. Guests linked to THIS campus instance (per-campus v2 — a sibling instance of the same
        //    multi-campus request keeps its own copy-on-write member rows, so scoping by
        //    visit_request_id alone would double-count a guest that exists on both campuses).
        var guests = await db.VisitInstanceGuestMembers
            .Where(l => l.VisitInstanceId == instance.VisitInstanceId)
            .Join(db.VisitGuestMembers, l => l.GuestMemberId, g => g.GuestMemberId,
                (l, g) => new { l.DisplayOrder, Member = g })
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Member.GuestMemberId)
            .Select(x => x.Member)
            .ToListAsync(ct);

        foreach (var g in guests)
        {
            if (seenGuestIds.Contains(g.GuestMemberId)) continue;
            // Already filled in as an internal participant (same name + role + organisation) — one
            // person, one row. The internal row is the one that stays: it carries a user_id.
            var identityKey = PersonIdentity.Key(g.FullName, g.JobTitle, g.Organization);
            if (identityKey.Length > 0 && internalIdentityKeys.Contains(identityKey)) continue;
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

        // 4. Đầu mối đoàn khách.
        await AddOperationalContactAsync(
            db, instance, existing, result, seenUserIds, seenGuestIds, minutesId, now, () => ++order, ct);

        return result;
    }

    /// <summary>
    /// Makes sure the campus's operational contact is in the biên bản EXACTLY once (NP-03).
    ///
    /// <para>
    /// Before this step existed, whether they appeared at all was an accident of how the form had been
    /// filled in. A contact who was also typed into the delegation list arrived through step 3, as a
    /// guest. A contact who was named only in the "Đầu mối" block arrived through nothing — the three
    /// sources above simply do not include them — so the person who ran the visit was missing from the
    /// record of it. And when the same human was in both places, the only thing standing between the
    /// biên bản and a duplicate was a string comparison.
    /// </para>
    /// <para>
    /// Four checks, strongest evidence first, mirroring <see cref="PersonIdentity"/>:
    /// </para>
    /// <list type="number">
    ///   <item><c>operational_contact_guest_member_id</c> — the stable link. If they are a member of
    ///   this delegation, step 3 has already added that exact row; nothing more to do.</item>
    ///   <item><c>operational_contact_user_id</c> — a confirmed contact holds an account, and the Host
    ///   or an internal participant may be that same account.</item>
    ///   <item>The contact's email against an internal row's email snapshot.</item>
    ///   <item>Name + job title + organisation, the weakest — enough to skip an auto-filled duplicate,
    ///   never enough to merge anything a user can see.</item>
    /// </list>
    /// <para>
    /// Only if all four say "not here" is a snapshot row added, and it carries no ids at all: the
    /// contact is a person the delegation named, not necessarily an account and not necessarily a
    /// member. That is exactly what <c>minute_participants</c>' nullable id columns are for.
    /// </para>
    /// </summary>
    private static async Task AddOperationalContactAsync(
        IApplicationDbContext db,
        VisitRequestCampus instance,
        IReadOnlyCollection<MinuteParticipant> existing,
        List<MinuteParticipant> result,
        HashSet<ulong> seenUserIds,
        HashSet<ulong> seenGuestIds,
        ulong minutesId,
        DateTime now,
        Func<uint> nextOrder,
        CancellationToken ct)
    {
        var detail = instance.FormDetail
            ?? await db.VisitInstanceFormDetails
                .FirstOrDefaultAsync(d => d.VisitInstanceId == instance.VisitInstanceId, ct);
        if (detail is null) return;

        // 1. Linked to a delegation member → step 3 (or a pre-existing row) already covers them.
        if (detail.OperationalContactGuestMemberId is ulong linkedGuestId
            && seenGuestIds.Contains(linkedGuestId))
            return;

        // 2. Same account as somebody already in the list.
        if (instance.OperationalContactUserId is ulong contactUserId && seenUserIds.Contains(contactUserId))
            return;

        // 3. Same email as an internal row (the Host, or an accepted participant).
        var contactEmail = PersonIdentity.NormalizeEmail(detail.OperationalContactEmail);
        if (contactEmail.Length > 0
            && existing.Concat(result).Any(p =>
                PersonIdentity.NormalizeEmail(p.EmailSnapshot) == contactEmail))
            return;

        // 4. Same name + role + organisation as ANY row already present.
        var contactKey = PersonIdentity.Key(
            detail.OperationalContactFullName,
            detail.OperationalContactJobTitle,
            detail.OperationalContactOrganization);
        if (contactKey.Length > 0
            && existing.Concat(result).Any(p =>
                PersonIdentity.Key(p.FullNameSnapshot, p.RoleSnapshot, p.OrganizationSnapshot) == contactKey))
            return;

        if (string.IsNullOrWhiteSpace(detail.OperationalContactFullName)) return;

        result.Add(new MinuteParticipant
        {
            MinutesId = minutesId,
            // The linked member id is carried when there IS one — the contact may be a member of the
            // delegation whose row this run has not seen (a legacy minutes created before step 3
            // covered them). Recording it keeps the next run idempotent by id instead of by name.
            UserId = instance.OperationalContactUserId,
            GuestMemberId = detail.OperationalContactGuestMemberId,
            FullNameSnapshot = detail.OperationalContactFullName,
            RoleSnapshot = string.IsNullOrWhiteSpace(detail.OperationalContactJobTitle)
                ? OperationalContactRole
                : $"{detail.OperationalContactJobTitle} ({OperationalContactRole})",
            OrganizationSnapshot = detail.OperationalContactOrganization,
            EmailSnapshot = detail.OperationalContactEmail,
            AttendanceStatus = AttendanceDefault,
            DisplayOrder = nextOrder(),
            CreatedAt = now,
        });
    }

    private static void Remember(HashSet<string> keys, MinuteParticipant row)
    {
        var key = PersonIdentity.Key(row.FullNameSnapshot, row.RoleSnapshot, row.OrganizationSnapshot);
        if (key.Length > 0) keys.Add(key);
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
