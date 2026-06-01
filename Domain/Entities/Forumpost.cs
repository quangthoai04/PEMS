using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Bài viết thảo luận nội bộ kín của từng đoàn khách cụ thể
/// </summary>
public partial class Forumpost
{
    /// <summary>
    /// UUID bài đăng thảo luận
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Không gian diễn đàn kín của riêng đoàn khách nào
    /// </summary>
    public Guid DelegationId { get; set; }

    /// <summary>
    /// Người đăng bài (Staff/Buddy/Media/Guest)
    /// </summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>
    /// Nội dung thông báo, trao đổi tiến độ hậu cần
    /// </summary>
    public string PostContent { get; set; } = null!;

    /// <summary>
    /// File tài liệu đính kèm phục vụ công việc
    /// </summary>
    public string? AttachmentUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Useraccount AuthorUser { get; set; } = null!;

    public virtual Delegation Delegation { get; set; } = null!;

    public virtual ICollection<Forumcomment> Forumcomments { get; set; } = new List<Forumcomment>();
}
