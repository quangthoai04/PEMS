using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Entities.Delegations;
using PEMS.Shared;

namespace PEMS.Domain.Entities.Minutes;

[Table("minute_participants")]
public class MinuteParticipant
{
    [Key]
    [Column("minute_participant_id")]
    public ulong MinuteParticipantId { get; set; }

    [Column("minutes_id")]
    public ulong MinutesId { get; set; }

    [Column("user_id")]
    public ulong? UserId { get; set; }

    [Column("guest_member_id")]
    public ulong? GuestMemberId { get; set; }

    /// <summary>
    /// Which list the delegation member came from — <c>GUEST</c> or <c>EXTERNAL_SUPPORT</c> — as it
    /// stood when the row entered the biên bản. NULL for internal and manual rows (MIN-02).
    ///
    /// <para>A SNAPSHOT, not a lookup. Reading <c>visit_guest_members.member_type</c> at render time
    /// would let a member reclassified next month rewrite a biên bản signed last month; a biên bản is
    /// a record of what was, and what was is stored here.</para>
    /// </summary>
    [Column("source_member_type")]
    public string? SourceMemberType { get; set; }

    /// <summary>
    /// Whether this person is the campus's operational contact. An ADDITIONAL role, never a kind:
    /// a guest who coordinates the visit is "Khách · Đầu mối", not something other than a guest.
    /// </summary>
    [Column("is_operational_contact")]
    public bool IsOperationalContact { get; set; }

    /// <summary>
    /// <c>ACTIVE</c> or <c>EXCLUDED</c> (MIN-03). Removing a source-linked person from the biên bản
    /// used to DELETE the row, after which the next "đồng bộ người mới" found them still on the
    /// official list and added them straight back — the Host's decision had nowhere to live. An
    /// excluded row stays, out of the biên bản but remembered, and can be restored.
    /// </summary>
    [Column("sync_state")]
    public string SyncState { get; set; } = MinuteParticipantSyncStates.Active;

    [Column("full_name_snapshot")]
    public string FullNameSnapshot { get; set; } = null!;

    [Column("organization_snapshot")]
    public string? OrganizationSnapshot { get; set; }

    [Column("role_snapshot")]
    public string? RoleSnapshot { get; set; }

    [Column("email_snapshot")]
    public string? EmailSnapshot { get; set; }

    [Column("attendance_status")]
    public string AttendanceStatus { get; set; } = "PRESENT";

    [Column("attendance_note")]
    public string? AttendanceNote { get; set; }

    [Column("checked_at")]
    public DateTime? CheckedAt { get; set; }

    [Column("checked_by")]
    public ulong? CheckedBy { get; set; }

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual Minute Minute { get; set; } = null!;
    public virtual User? User { get; set; }
    public virtual User? CheckedByUser { get; set; }
    public virtual VisitGuestMember? GuestMember { get; set; }
}
