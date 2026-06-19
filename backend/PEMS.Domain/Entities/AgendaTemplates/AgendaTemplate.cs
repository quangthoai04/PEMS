using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.AgendaTemplates;

[Table("agenda_templates")]
public class AgendaTemplate
{
    [Key]
    [Column("agenda_template_id")]
    public ulong AgendaTemplateId { get; set; }

    [Column("campus_id")]
    public ulong? CampusId { get; set; }

    [Column("campus_scope_key")]
    public string CampusScopeKey { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("items_json")]
    public string? ItemsJson { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public ulong? DeletedBy { get; set; }
}
