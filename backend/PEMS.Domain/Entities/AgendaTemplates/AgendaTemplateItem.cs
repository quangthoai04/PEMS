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
    public uint DisplayOrder { get; set; }

    /// <summary>Minutes from the campus planned_start_at when this item begins.</summary>
    [Column("start_offset_minutes")]
    public uint StartOffsetMinutes { get; set; }

    /// <summary>Length of this item in minutes. Always &gt; 0 (DB CHECK).</summary>
    [Column("duration_minutes")]
    public uint DurationMinutes { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("responsible_role_label")]
    public string? ResponsibleRoleLabel { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual AgendaTemplate AgendaTemplate { get; set; } = null!;
}
