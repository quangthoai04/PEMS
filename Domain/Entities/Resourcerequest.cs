using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Quản lý điều phối mượn xe điện, teabreak liên phòng ban chức năng
/// </summary>
public partial class Resourcerequest
{
    /// <summary>
    /// UUID yêu cầu tài nguyên hậu cần
    /// </summary>
    public Guid RequestId { get; set; }

    /// <summary>
    /// Thuộc đoàn khách nào
    /// </summary>
    public Guid DelegationId { get; set; }

    /// <summary>
    /// Phòng ban chịu trách nhiệm phê duyệt mượn (Hành chính/Tuyển sinh)
    /// </summary>
    public Guid DepartmentId { get; set; }

    /// <summary>
    /// Loại tài nguyên mượn (Electric_Car, Meeting_Room, TeaBreak, LED_Welcome)
    /// </summary>
    public string ResourceType { get; set; } = null!;

    /// <summary>
    /// Số lượng tài nguyên yêu cầu
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Mô tả chi tiết yêu cầu, khung giờ mượn cụ thể
    /// </summary>
    public string? UsageDetails { get; set; }

    /// <summary>
    /// Trạng thái phòng ban phản hồi (Pending, Confirmed, Rejected)
    /// </summary>
    public string ConfirmationStatus { get; set; } = null!;

    /// <summary>
    /// Lý do từ chối mượn của phòng ban liên quan
    /// </summary>
    public string? RejectedReason { get; set; }

    /// <summary>
    /// Cán bộ phòng ban xử lý duyệt
    /// </summary>
    public Guid? ConfirmedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Useraccount? ConfirmedByNavigation { get; set; }

    public virtual Delegation Delegation { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;
}
