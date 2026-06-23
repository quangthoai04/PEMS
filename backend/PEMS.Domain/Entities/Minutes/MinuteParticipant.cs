using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Users;
using PEMS.Domain.Entities.Delegations;

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
