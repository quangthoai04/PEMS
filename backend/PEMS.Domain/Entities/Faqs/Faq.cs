using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Faqs;

[Table("faqs")]
public class Faq
{
    [Key]
    [Column("faq_id")]
    public string FaqId { get; set; } = null!;

    [Column("question")]
    public string Question { get; set; } = null!;

    [Column("answer")]
    public string Answer { get; set; } = null!;

    [Column("category")]
    public string? Category { get; set; }

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}
