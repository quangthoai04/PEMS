using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Emails;

[Table("email_templates")]
public class EmailTemplate
{
    [Key]
    [Column("email_template_id")]
    public ulong EmailTemplateId { get; set; }

    [Column("template_code")]
    public string TemplateCode { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("purpose")]
    public string? Purpose { get; set; }

    [Column("translations_json")]
    public string? TranslationsJson { get; set; }

    [Column("variables_json")]
    public string? VariablesJson { get; set; }

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
}
