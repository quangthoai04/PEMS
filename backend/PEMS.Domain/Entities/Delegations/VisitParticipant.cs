using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_participants")]
public class VisitParticipant
{
    [Key]
    [Column("participant_id")]
    public string ParticipantId { get; set; } = null!;

    [Column("visit_instance_id")]
    public string VisitInstanceId { get; set; } = null!;

    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("participant_role")]
    public string ParticipantRole { get; set; } = "OTHER";

    [Column("is_host")]
    public bool IsHost { get; set; }

    [Column("status")]
    public string Status { get; set; } = "INVITED";

    [Column("invited_by")]
    public string? InvitedBy { get; set; }

    [Column("invited_at")]
    public DateTime? InvitedAt { get; set; }

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }

    [Column("assigned_by")]
    public string? AssignedBy { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
}
