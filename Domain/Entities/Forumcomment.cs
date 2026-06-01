using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Phản hồi thảo luận tiến độ công việc giữa các thành viên của đoàn
/// </summary>
public partial class Forumcomment
{
    /// <summary>
    /// UUID bình luận
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Bình luận thuộc bài viết nào
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Người bình luận
    /// </summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>
    /// Nội dung phản hồi
    /// </summary>
    public string CommentContent { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Useraccount AuthorUser { get; set; } = null!;

    public virtual Forumpost Post { get; set; } = null!;
}
