using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("email_templates")]
public class EmailTemplate
{
    [Key]
    [Column("template_id")]
    public string TemplateId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("body")]
    public string? Body { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "InUse";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
