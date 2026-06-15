using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("minute_participants")]
public class MinuteParticipant
{
    [Key]
    [Column("mp_id")]
    public string MpId { get; set; } = null!;

    [Column("minute_id")]
    public string MinuteId { get; set; } = null!;

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("role_title")]
    public string? RoleTitle { get; set; }

    [Column("organization")]
    public string? Organization { get; set; }

    [Column("is_internal")]
    public bool IsInternal { get; set; }

    [Column("is_partner")]
    public bool IsPartner { get; set; }

    [Column("confirmed")]
    public bool Confirmed { get; set; }

    public virtual Minute Minute { get; set; } = null!;
}
