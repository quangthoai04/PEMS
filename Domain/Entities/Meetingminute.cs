using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Biên bản ghi nhận nội dung cuộc họp chính thức của đoàn khách
/// </summary>
public partial class Meetingminute
{
    /// <summary>
    /// UUID biên bản cuộc họp
    /// </summary>
    public Guid MinutesId { get; set; }

    /// <summary>
    /// Thuộc đoàn khách nào
    /// </summary>
    public Guid DelegationId { get; set; }

    /// <summary>
    /// Nội dung chi tiết các vấn đề đã thảo luận ghi lại
    /// </summary>
    public string DiscussionContent { get; set; } = null!;

    /// <summary>
    /// Cờ lưu trạng thái (1: Bản nháp Staff đang viết, 0: Biên bản chính thức)
    /// </summary>
    public bool? IsDraft { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Actionitem> Actionitems { get; set; } = new List<Actionitem>();

    public virtual Delegation Delegation { get; set; } = null!;
}
