using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Bảo mật nhật ký tích hợp API với trang Outbound
/// </summary>
public partial class Partnersynclog
{
    /// <summary>
    /// UUID mã log
    /// </summary>
    public Guid LogId { get; set; }

    /// <summary>
    /// Đối tác được đồng bộ
    /// </summary>
    public Guid PartnerId { get; set; }

    /// <summary>
    /// Cán bộ click đồng bộ
    /// </summary>
    public Guid SyncedBy { get; set; }

    /// <summary>
    /// Hướng đồng bộ (PUSH_TO_OUTBOUND, PULL_PROGRAM_FROM_OUTBOUND)
    /// </summary>
    public string SyncDirection { get; set; } = null!;

    /// <summary>
    /// Trạng thái (SUCCESS, FAILED)
    /// </summary>
    public string SyncStatus { get; set; } = null!;

    /// <summary>
    /// Nội dung phản hồi từ API Outbound hoặc thông báo lỗi
    /// </summary>
    public string? ResponseContent { get; set; }

    public DateTime SyncedAt { get; set; }

    public virtual Partner Partner { get; set; } = null!;

    public virtual Useraccount SyncedByNavigation { get; set; } = null!;
}
