namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// One snapshot row of <c>minute_participants</c> for the meeting-minutes UI.
/// <para>The source of a row is distinguished purely by the real SQL columns (no extra DB fields):
/// <list type="bullet">
///   <item><c>user_id != null, guest_member_id = null</c> → internal user (Host / IC / Dept / Student).</item>
///   <item><c>guest_member_id != null</c> → a guest taken from <c>visit_guest_members</c>.</item>
///   <item>both null → a person the Host typed in manually (pure snapshot).</item>
/// </list>
/// <see cref="ParticipantKind"/> is a DERIVED display value only; it is never stored.</para>
/// </summary>
public sealed class MinuteParticipantDto
{
    public ulong MinuteParticipantId { get; set; }
    public ulong MinutesId { get; set; }
    public ulong? UserId { get; set; }
    public ulong? GuestMemberId { get; set; }

    public string FullNameSnapshot { get; set; } = string.Empty;
    public string? RoleSnapshot { get; set; }
    public string? OrganizationSnapshot { get; set; }
    public string? EmailSnapshot { get; set; }

    /// <summary>PRESENT | ABSENT | EXCUSED (matches the SQL enum).</summary>
    public string AttendanceStatus { get; set; } = "PRESENT";
    public string? AttendanceNote { get; set; }
    public DateTime? CheckedAt { get; set; }
    public ulong? CheckedBy { get; set; }
    public uint DisplayOrder { get; set; }

    /// <summary>Derived only (INTERNAL / GUEST / MANUAL) — for rendering the "Loại nguồn" column.</summary>
    public string ParticipantKind { get; set; } = "MANUAL";

    public static string KindOf(ulong? userId, ulong? guestMemberId)
        => userId != null ? "INTERNAL" : guestMemberId != null ? "GUEST" : "MANUAL";
}
