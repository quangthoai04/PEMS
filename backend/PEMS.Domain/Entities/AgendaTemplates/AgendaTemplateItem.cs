using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.AgendaTemplates;

[Table("agenda_template_items")]
public class AgendaTemplateItem
{
    [Key]
    [Column("agenda_template_item_id")]
    public ulong AgendaTemplateItemId { get; set; }

    [Column("agenda_template_id")]
    public ulong AgendaTemplateId { get; set; }

    [Column("display_order")]
    public uint DisplayOrder { get; set; } = 1;

    [Column("start_time")]
    public TimeSpan StartTime { get; set; }

    [Column("end_time")]
    public TimeSpan? EndTime { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual AgendaTemplate AgendaTemplate { get; set; } = null!;
}
