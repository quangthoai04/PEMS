using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Enums;

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

    /// <summary>
    /// SQL: purpose ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION') NOT NULL. Mandatory, and only
    /// those two values are storable — see <see cref="PEMS.Shared.OtpPurpose"/>.
    /// </summary>
    [Column("purpose")]
    public string Purpose { get; set; } = null!;

    [Column("campus_id")]
    public ulong? CampusId { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("subject_vi")]
    public string? SubjectVi { get; set; }

    [Column("body_vi")]
    public string? BodyVi { get; set; }

    [Column("subject_en")]
    public string? SubjectEn { get; set; }

    [Column("body_en")]
    public string? BodyEn { get; set; }

    /// <summary>SQL: body_format ENUM('PLAIN_TEXT','HTML') NOT NULL DEFAULT 'HTML'.</summary>
    [Column("body_format")]
    public EmailBodyFormat BodyFormat { get; set; } = EmailBodyFormat.HTML;

    [Column("variables_text")]
    public string? VariablesText { get; set; }

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
