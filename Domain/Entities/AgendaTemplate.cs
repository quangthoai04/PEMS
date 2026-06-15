using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("agenda_templates")]
public class AgendaTemplate
{
    [Key]
    [Column("template_id")]
    public string TemplateId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AgendaTemplateItem> Items { get; set; } = new List<AgendaTemplateItem>();
}
