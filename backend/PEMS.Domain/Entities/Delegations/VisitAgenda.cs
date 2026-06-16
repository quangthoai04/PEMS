using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_agendas")]
public class VisitAgenda
{
    [Key]
    [Column("agenda_id")]
    public string AgendaId { get; set; } = null!;

    [Column("visit_instance_id")]
    public string VisitInstanceId { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("responsible_user_id")]
    public string? ResponsibleUserId { get; set; }

    [Column("sequence_order")]
    public int SequenceOrder { get; set; }

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
