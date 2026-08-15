using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Entities.Minutes;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// The one rule for who wears "· Đầu mối" in a biên bản.
///
/// <para>
/// The badge used to be decided in three places that each knew a different amount: the auto-fill set
/// it on the rows it created, the save set it on guest rows it created, and nothing at all re-decided
/// it afterwards. Whoever held the role the day a row was written kept the badge for good — so a
/// campus handed to a new đầu mối went on naming the old one, and once the sync brought the new one
/// in with a correct badge of their own, named two. One rule, applied on save AND on read, is what
/// stops the three from drifting apart again.
/// </para>
///
/// <para><b>Decided by id, never by name.</b> A delegation member is identified by
/// <c>guest_member_id</c> and nothing else; a contact who is not a member has no member id to compare
/// and is recognised by account or address instead. Name matching is deliberately absent — merging
/// two people who share a name is the defect this whole area exists to prevent.</para>
/// </summary>
internal static class MinuteContactBadge
{
    /// <summary>
    /// Whether an existing row holds the badge, given who the campus's contact is NOW.
    ///
    /// <para>Three cases, in order. A row carrying a member id is answered by that id alone. A row
    /// with no member id cannot be the contact once the role belongs to a member, so it loses the
    /// badge. Otherwise — the contact is nobody's member row, and neither is this row — there is no
    /// id on either side to compare, so what was already recorded stands: the snapshot row written
    /// for such a contact legitimately wears the badge, and guessing by name would be worse than
    /// keeping it.</para>
    /// </summary>
    public static bool Resolve(ulong? rowGuestMemberId, bool stored, ulong? contactGuestMemberId)
    {
        if (rowGuestMemberId is ulong memberId) return contactGuestMemberId == memberId;
        if (contactGuestMemberId is not null) return false;
        return stored;
    }

    /// <summary>
    /// Whether a row being CREATED without a member id is the campus's contact — the seed value for
    /// the "stored" argument above, which a new row does not have yet.
    ///
    /// <para>Same evidence the auto-fill uses when it decides not to add a second contact row: the
    /// account, then the address. Both are identities the person proved; neither is a name.</para>
    /// </summary>
    public static bool IsContactWithoutMemberId(
        ulong? rowUserId, string? rowEmail, ulong? contactUserId, string? contactEmail)
    {
        if (rowUserId is ulong id && contactUserId == id) return true;
        var email = PersonIdentity.NormalizeEmail(rowEmail);
        return email.Length > 0 && email == PersonIdentity.NormalizeEmail(contactEmail);
    }

    /// <summary>Re-derives the badge on rows already in the biên bản, in place.</summary>
    public static void ApplyTo(IEnumerable<MinuteParticipant> rows, ulong? contactGuestMemberId)
    {
        foreach (var row in rows)
            row.IsOperationalContact =
                Resolve(row.GuestMemberId, row.IsOperationalContact, contactGuestMemberId);
    }

    /// <summary>
    /// Which delegation member holds the contact role for the campus this minutes belongs to, or null
    /// when the contact is not one of them. Read from the instance, never from a client.
    /// </summary>
    public static async Task<ulong?> CurrentContactMemberIdAsync(
        IApplicationDbContext db, ulong minutesId, CancellationToken ct)
        => await db.Minutes
            .Where(m => m.MinutesId == minutesId)
            .Join(db.VisitInstanceFormDetails,
                m => m.VisitInstanceId,
                d => d.VisitInstanceId,
                (m, d) => d.OperationalContactGuestMemberId)
            .FirstOrDefaultAsync(ct);
}
