using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Chi tiết Agenda lịch trình hoạt động trong ngày của đoàn khách
/// </summary>
public partial class Delegationagenda
{
    /// <summary>
    /// UUID lịch trình nhỏ
    /// </summary>
    public Guid AgendaId { get; set; }

    /// <summary>
    /// Thuộc đoàn khách nào
    /// </summary>
    public Guid DelegationId { get; set; }

    /// <summary>
    /// Mốc thời gian (Ví dụ: 09:00:00)
    /// </summary>
    public TimeOnly TimeSlot { get; set; }

    /// <summary>
    /// Mô tả chi tiết hoạt động (Ví dụ: Tham quan Tượng Thinker, Họp ký kết)
    /// </summary>
    public string ActivityDescription { get; set; } = null!;

    /// <summary>
    /// Địa điểm diễn ra (Ví dụ: Phòng họp 202 Alpha)
    /// </summary>
    public string Location { get; set; } = null!;

    public virtual Delegation Delegation { get; set; } = null!;
}
