using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("agenda_template_items")]
public class AgendaTemplateItem
{
    [Key]
    [Column("item_id")]
    public string ItemId { get; set; } = null!;

    [Column("template_id")]
    public string TemplateId { get; set; } = null!;

    [Column("start_time")]
    public TimeSpan? StartTime { get; set; }

    [Column("end_time")]
    public TimeSpan? EndTime { get; set; }

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("sequence_order")]
    public int SequenceOrder { get; set; }

    public virtual AgendaTemplate Template { get; set; } = null!;
}
