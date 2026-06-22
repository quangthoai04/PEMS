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

    [Column("old_value_text")]
    public string? OldValueText { get; set; }

    [Column("new_value_text")]
    public string? NewValueText { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual AuditLog AuditLog { get; set; } = null!;
}
