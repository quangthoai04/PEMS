using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("visit_participants")]
public class VisitParticipant
{
    [Key]
    [Column("participant_id")]
    public string ParticipantId { get; set; } = null!;

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("external_name")]
    public string? ExternalName { get; set; }

    [Column("participant_role")]
    public string ParticipantRole { get; set; } = "Supporter";

    [Column("is_host")]
    public bool IsHost { get; set; }

    [Column("confirmed")]
    public bool Confirmed { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual User? User { get; set; }
}
