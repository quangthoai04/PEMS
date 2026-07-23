using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Partners;

[Table("partner_contacts")]
public class PartnerContact
{
    [Key]
    [Column("contact_id")]
    public ulong ContactId { get; set; }

    [Column("partner_id")]
    public ulong PartnerId { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [Column("email")]
    public string? Email { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("job_title")]
    public string? JobTitle { get; set; }

    [Column("department_name")]
    public string? DepartmentName { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("source_type")]
    public string SourceType { get; set; } = "MANUAL";

    [Column("scanned_card_file_id")]
    public ulong? ScannedCardFileId { get; set; }

    [Column("avatar_file_id")]
    public ulong? AvatarFileId { get; set; }

    [Column("ocr_confidence")]
    public decimal? OcrConfidence { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

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

    public virtual Partner Partner { get; set; } = null!;
}
