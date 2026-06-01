using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Quản lý tin tức sự kiện truyền thông công cộng (Hỗ trợ nạp API tự động)
/// </summary>
public partial class News
{
    /// <summary>
    /// UUID bài viết tin tức
    /// </summary>
    public Guid NewsId { get; set; }

    /// <summary>
    /// Bài viết liên kết với đoàn khách cụ thể nào
    /// </summary>
    public Guid? DelegationId { get; set; }

    /// <summary>
    /// Tiêu đề bài báo truyền thông sự kiện
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Nội dung bài viết (HTML format)
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Ảnh đại diện bài viết
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Trạng thái phê duyệt (Draft, PendingApproval, Published)
    /// </summary>
    public string NewsStatus { get; set; } = null!;

    /// <summary>
    /// Cờ nhận diện (1: Bài nạp tự động từ trang Outbound về, 0: Bài tự viết)
    /// </summary>
    public bool IsFromOutbound { get; set; }

    /// <summary>
    /// Người soạn thảo
    /// </summary>
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Useraccount CreatedByNavigation { get; set; } = null!;

    public virtual Delegation? Delegation { get; set; }
}
