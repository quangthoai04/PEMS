using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// One field-level proposal inside an amendment (immutable once PENDING_APPROVAL). The old/new
/// values are stored as JSON; <c>change_class</c> is the backend classification
/// (SAFE / APPROVAL_SENSITIVE / STRUCTURAL / PRIVACY_URGENT).
/// </summary>
[Table("visit_instance_amendment_changes")]
public class VisitInstanceAmendmentChange
{
    [Key]
    [Column("amendment_change_id")]
    public ulong AmendmentChangeId { get; set; }

    [Column("amendment_id")]
    public ulong AmendmentId { get; set; }

    [Column("field_path")]
    public string FieldPath { get; set; } = null!;

    [Column("change_class")]
    public string ChangeClass { get; set; } = null!;

    [Column("old_value_json")]
    public string? OldValueJson { get; set; }

    [Column("new_value_json")]
    public string? NewValueJson { get; set; }

    [Column("is_sensitive")]
    public bool IsSensitive { get; set; }

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual VisitInstanceAmendment Amendment { get; set; } = null!;
}
