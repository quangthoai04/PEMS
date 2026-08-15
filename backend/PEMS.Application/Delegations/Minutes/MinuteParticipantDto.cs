using PEMS.Shared;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// One snapshot row of <c>minute_participants</c> for the meeting-minutes UI.
///
/// <para>Where a row CAME FROM is answered by the real SQL columns:
/// <list type="bullet">
///   <item><c>user_id != null</c> → an internal user (Host / IC / Dept / Student).</item>
///   <item><c>guest_member_id != null</c> → a member of the delegation, of the kind recorded in
///   <c>source_member_type</c>.</item>
///   <item>both null → somebody the Host typed in by hand (pure snapshot).</item>
/// </list></para>
///
/// <para><b>Why <see cref="SourceMemberType"/> has to travel (MIN-02).</b> Every guest-side row used
/// to arrive labelled "Khách", because <c>guest_member_id != null</c> was the only thing the client
/// was told. An interpreter or a travelling coordinator is written down as <c>EXTERNAL_SUPPORT</c>,
/// is eligible to be the campus's contact, and is not a member of the delegation — calling them a
/// guest in the record of the meeting is simply wrong, and no amount of UI could fix it without the
/// field.</para>
///
/// <para><see cref="ParticipantKind"/> is DERIVED for display and never stored;
/// <see cref="IsOperationalContact"/> is an ADDITIONAL role rather than a kind, so a guest who
/// coordinates the visit renders as "Khách · Đầu mối" and stays a guest.</para>
/// </summary>
public sealed class MinuteParticipantDto
{
    public ulong MinuteParticipantId { get; set; }
    public ulong MinutesId { get; set; }
    public ulong? UserId { get; set; }
    public ulong? GuestMemberId { get; set; }

    /// <summary>GUEST | EXTERNAL_SUPPORT for a delegation row; null for internal and manual rows.</summary>
    public string? SourceMemberType { get; set; }

    /// <summary>The campus's operational contact. A badge in addition to the kind, never instead of it.</summary>
    public bool IsOperationalContact { get; set; }

    /// <summary>
    /// True when neither id is set: a person who exists only in this biên bản. The one kind of row
    /// whose name / role / organisation the Host may edit here — everything else is a copy of a
    /// record that lives elsewhere, and editing the copy would only make the two disagree (MIN-04).
    /// </summary>
    public bool IsManual { get; set; }

    /// <summary>ACTIVE | EXCLUDED (MIN-03). An EXCLUDED row is out of the biên bản but remembered.</summary>
    public string SyncState { get; set; } = MinuteParticipantSyncStates.Active;

    public string FullNameSnapshot { get; set; } = string.Empty;
    public string? RoleSnapshot { get; set; }
    public string? OrganizationSnapshot { get; set; }
    public string? EmailSnapshot { get; set; }

    /// <summary>
    /// Not a stored snapshot column — looked up live from visit_guest_members.nationality via
    /// GuestMemberId (when set) so the "create/link partner" modal can default its Country field.
    /// Null for INTERNAL/MANUAL participants (no linked guest).
    /// </summary>
    public string? GuestNationality { get; set; }

    /// <summary>PRESENT | ABSENT | EXCUSED (matches the SQL enum).</summary>
    public string AttendanceStatus { get; set; } = "ABSENT";
    public string? AttendanceNote { get; set; }
    public DateTime? CheckedAt { get; set; }
    public ulong? CheckedBy { get; set; }
    public uint DisplayOrder { get; set; }

    /// <summary>
    /// Derived only — INTERNAL / GUEST / EXTERNAL_SUPPORT / MANUAL — for rendering "Loại nguồn".
    ///
    /// <para>Four values, not three. The old three collapsed <c>EXTERNAL_SUPPORT</c> into
    /// <c>GUEST</c>, and a label that cannot express the difference cannot be corrected downstream.</para>
    /// </summary>
    public string ParticipantKind { get; set; } = "MANUAL";

    /// <param name="isOperationalContact">
    /// Whether this row is the campus's đầu mối. Optional only so a caller that genuinely has no such
    /// row (none exists today) is not forced to invent one; both real callers pass the stored flag.
    /// </param>
    public static string KindOf(
        ulong? userId, ulong? guestMemberId, string? sourceMemberType, bool isOperationalContact = false)
    {
        // The campus's đầu mối is guest-side, whatever ids the row happens to carry.
        //
        // They very often hold a user_id, and it used to be read as "nhân sự nội bộ" — so the person
        // leading the visiting delegation was displayed as "Nội bộ", in a table that also badged them
        // "Đầu mối đoàn khách". The id means only that they CONFIRMED: the invitation provisions or
        // reuses an account so they can answer it, and OperationalContactEligibility refuses the role
        // to an internal account twice over — once for the invited address, once for the account that
        // answers. So a row wearing this badge can never be internal, and the account is evidence of
        // nothing but the confirmation.
        //
        // Which guest-side kind still comes from the member row when there is one; a contact who is
        // not in the delegation has no member row, so GUEST is all that can be said of them.
        if (isOperationalContact)
            return sourceMemberType == GuestMemberType.ExternalSupport
                ? GuestMemberType.ExternalSupport
                : GuestMemberType.Guest;

        if (userId != null) return "INTERNAL";
        if (guestMemberId == null) return "MANUAL";
        // A delegation row whose type was never recorded (a biên bản created before the column
        // existed) reads as GUEST — the old behaviour, and the commoner of the two by far. It is a
        // display default for legacy rows, never a decision written back to the database.
        return sourceMemberType == GuestMemberType.ExternalSupport
            ? GuestMemberType.ExternalSupport
            : GuestMemberType.Guest;
    }
}
