using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("audit_log_changes")]
public class AuditLogChange
{
    [Key]
    [Column("audit_log_change_id")]
    public ulong AuditLogChangeId { get; set; }

    [Column("audit_log_id")]
    public ulong AuditLogId { get; set; }

    [Column("field_name")]
    public string FieldName { get; set; } = null!;

    // Per-campus form v2 audit metadata (additive).
    [Column("change_category")]
    public string? ChangeCategory { get; set; }

    [Column("value_format")]
    public string ValueFormat { get; set; } = "TEXT";

    [Column("is_sensitive")]
    public bool IsSensitive { get; set; }

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("old_value_text")]
    public string? OldValueText { get; set; }

    [Column("new_value_text")]
    public string? NewValueText { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual AuditLog AuditLog { get; set; } = null!;
}
