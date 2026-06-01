using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Danh sách đầu mối liên lạc con thuộc Hồ sơ Đối tác
/// </summary>
public partial class Partnercontact
{
    /// <summary>
    /// UUID đầu mối liên hệ
    /// </summary>
    public Guid ContactId { get; set; }

    /// <summary>
    /// Liên kết thuộc đối tác nào
    /// </summary>
    public Guid PartnerId { get; set; }

    /// <summary>
    /// Họ tên cán bộ đầu mối phía đối tác
    /// </summary>
    public string ContactName { get; set; } = null!;

    /// <summary>
    /// Chức vụ, chức danh làm việc
    /// </summary>
    public string? Designation { get; set; }

    /// <summary>
    /// Email làm việc trực tiếp
    /// </summary>
    public string ContactEmail { get; set; } = null!;

    /// <summary>
    /// Số điện thoại/Whatsapp
    /// </summary>
    public string? ContactPhone { get; set; }

    public virtual Partner Partner { get; set; } = null!;
}
