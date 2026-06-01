using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Bảng bắc cầu quản lý danh sách thành viên nội bộ và khách tham gia đoàn
/// </summary>
public partial class Delegationmember
{
    /// <summary>
    /// Mã đoàn
    /// </summary>
    public Guid DelegationId { get; set; }

    /// <summary>
    /// Mã User được add (Cán bộ ban khác, Sinh viên Buddy, Media, Khách ngoài)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Vai trò chi tiết được gán trong đoàn (Buddy, Media, Attendee)
    /// </summary>
    public string? SpecificRole { get; set; }

    public virtual Delegation Delegation { get; set; } = null!;

    public virtual Useraccount User { get; set; } = null!;
}
