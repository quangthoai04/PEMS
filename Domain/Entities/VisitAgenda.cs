using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("visit_agendas")]
public class VisitAgenda
{
    [Key]
    [Column("agenda_id")]
    public string AgendaId { get; set; } = null!;

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("start_time")]
    public TimeSpan? StartTime { get; set; }

    [Column("end_time")]
    public TimeSpan? EndTime { get; set; }

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("sequence_order")]
    public int SequenceOrder { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
}
