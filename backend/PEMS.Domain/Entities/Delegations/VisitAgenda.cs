using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_agendas")]
public class VisitAgenda
{
    [Key]
    [Column("agenda_id")]
    public ulong AgendaId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

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
    public ulong? ResponsibleUserId { get; set; }

    [Column("sequence_order")]
    public int SequenceOrder { get; set; }

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
