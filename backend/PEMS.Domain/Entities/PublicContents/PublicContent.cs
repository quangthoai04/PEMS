using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.PublicContents;

[Table("public_contents")]
public class PublicContent
{
    [Key]
    [Column("public_content_id")]
    public string PublicContentId { get; set; } = null!;

    [Column("block_key")]
    public string BlockKey { get; set; } = null!;

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("campus_scope_key")]
    public string CampusScopeKey { get; set; } = "GLOBAL";

    [Column("block_type")]
    public string BlockType { get; set; } = "CUSTOM";

    [Column("status")]
    public string Status { get; set; } = "DRAFT";

    [Column("display_order")]
    public int DisplayOrder { get; set; }

    [Column("translations_json")]
    public string TranslationsJson { get; set; } = null!;

    [Column("metadata_json")]
    public string? MetadataJson { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}
