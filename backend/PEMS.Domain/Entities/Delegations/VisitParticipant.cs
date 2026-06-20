using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_participants")]
public class VisitParticipant
{
    [Key]
    [Column("participant_id")]
    public ulong ParticipantId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("user_id")]
    public ulong UserId { get; set; }

    // visit_participants.participant_role ENUM('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT')
    [Column("participant_role")]
    public string ParticipantRole { get; set; } = "IC_SUPPORT";

    [Column("is_host")]
    public bool IsHost { get; set; }

    [Column("status")]
    public string Status { get; set; } = "INVITED";

    [Column("invited_by")]
    public ulong? InvitedBy { get; set; }

    [Column("invited_at")]
    public DateTime? InvitedAt { get; set; }

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }

    [Column("assigned_by")]
    public ulong? AssignedBy { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
}
