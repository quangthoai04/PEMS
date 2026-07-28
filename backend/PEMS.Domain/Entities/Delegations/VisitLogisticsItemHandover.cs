using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Users;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// PEMS v10: borrow/return signatures for a logistics item.
/// At most one BORROW and one RETURN row per logistics_item_id (unique key).
/// </summary>
[Table("visit_logistics_item_handovers")]
public class VisitLogisticsItemHandover
{
    [Key]
    [Column("handover_id")]
    public ulong HandoverId { get; set; }

    [Column("logistics_item_id")]
    public ulong LogisticsItemId { get; set; }

    [Column("handover_type")]
    public string HandoverType { get; set; } = default!; // BORROW | RETURN

    [Column("borrower_signed_by")]
    public ulong? BorrowerSignedBy { get; set; }

    [Column("borrower_signed_at")]
    public DateTime? BorrowerSignedAt { get; set; }

    [Column("provider_signed_by")]
    public ulong? ProviderSignedBy { get; set; }

    [Column("provider_signed_at")]
    public DateTime? ProviderSignedAt { get; set; }

    [Column("item_condition")]
    public string? ItemCondition { get; set; } // GOOD | DAMAGED | MISSING | OTHER

    /// <summary>
    /// Free-text note cho item thường; với item TRANSPORT (xe điện) cột này được dùng để lưu
    /// checklist dạng JSON (mảng {name, qty, giao, nhan}) trên dòng BORROW thay vì note tự do —
    /// tránh thêm cột DB mới. Đọc lại qua RequestDetailDto.ChecklistJson (chỉ set khi ItemType = TRANSPORT).
    /// </summary>
    [Column("condition_note")]
    public string? ConditionNote { get; set; }

    [Column("attachment_file_id")]
    public ulong? AttachmentFileId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    public virtual VisitLogisticsItem LogisticsItem { get; set; } = null!;
    public virtual User? BorrowerSignedByUser { get; set; }
    public virtual User? ProviderSignedByUser { get; set; }
    public virtual UploadedFile? AttachmentFile { get; set; }
    public virtual User? CreatedByUser { get; set; }
}
