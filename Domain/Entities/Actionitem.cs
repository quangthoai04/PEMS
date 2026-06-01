using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Danh sách đầu việc phát sinh (chốt chặn bắt buộc hoàn thành trước khi Đóng đoàn)
/// </summary>
public partial class Actionitem
{
    /// <summary>
    /// UUID đầu việc phát sinh
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Sinh ra từ biên bản cuộc họp nào
    /// </summary>
    public Guid MinutesId { get; set; }

    /// <summary>
    /// Nội dung công việc chi tiết cần làm
    /// </summary>
    public string TaskDescription { get; set; } = null!;

    /// <summary>
    /// Cán bộ hoặc Sinh viên hỗ trợ chịu trách nhiệm thực hiện
    /// </summary>
    public Guid AssigneeUserId { get; set; }

    /// <summary>
    /// Hạn chót hoàn thành đầu việc
    /// </summary>
    public DateOnly Deadline { get; set; }

    /// <summary>
    /// Trạng thái (0: Chưa xong, 1: Đã hoàn thành)
    /// </summary>
    public bool IsCompleted { get; set; }

    public virtual Useraccount AssigneeUser { get; set; } = null!;

    public virtual Meetingminute Minutes { get; set; } = null!;
}
