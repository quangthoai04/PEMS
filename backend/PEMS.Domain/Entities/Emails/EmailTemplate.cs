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

    /// <summary>
    /// SQL: revision INT UNSIGNED NOT NULL DEFAULT 1. The optimistic-concurrency token for content
    /// writes (UC-44 update, and restore-to-default).
    ///
    /// <para>
    /// Deliberately NOT mapped as an EF concurrency token. EF would put the value it loaded into the
    /// WHERE clause, but the value that has to be checked is the one the OPERATOR was shown — a caller
    /// who read revision 4, sat on the form while someone else saved 5, and then submits must be
    /// refused. So both writers issue an explicit conditional UPDATE carrying the caller's expected
    /// revision and bump this column in the same statement; a rows-affected of 0 IS the conflict.
    /// </para>
    /// <para>
    /// Reading it is safe; assigning it from a handler is not — the increment belongs to SQL, because a
    /// read-modify-write in C# reintroduces exactly the lost update this column exists to prevent.
    /// </para>
    /// </summary>
    [Column("revision")]
    public uint Revision { get; set; } = 1;

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
