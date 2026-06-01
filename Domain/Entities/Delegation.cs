using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Bảng trung tâm quản lý thông tin các đoàn khách quốc tế
/// </summary>
public partial class Delegation
{
    /// <summary>
    /// UUID định danh đoàn khách
    /// </summary>
    public Guid DelegationId { get; set; }

    /// <summary>
    /// Đối tác liên kết (NULL nếu là khách vãng lai đăng ký trực tuyến)
    /// </summary>
    public Guid? PartnerId { get; set; }

    /// <summary>
    /// Tên đoàn khách (Ví dụ: Đoàn Đại học Sarawak Malaysia ghé thăm)
    /// </summary>
    public string DelegationName { get; set; } = null!;

    /// <summary>
    /// Hình thức (DIRECT - Trực tiếp, ONLINE - Trực tuyến)
    /// </summary>
    public string VisitType { get; set; } = null!;

    /// <summary>
    /// Cơ sở chịu trách nhiệm đón tiếp chính
    /// </summary>
    public string CampusCode { get; set; } = null!;

    /// <summary>
    /// Cờ nhận diện đoàn đi liên cơ sở/chéo nhiều campus
    /// </summary>
    public bool IsCrossCampus { get; set; }

    /// <summary>
    /// Ngày đón tiếp chính thức
    /// </summary>
    public DateOnly VisitDate { get; set; }

    /// <summary>
    /// Trạng thái đoàn (PendingApproval, Approved, Ongoing, Closed, Cancelled)
    /// </summary>
    public string DelegationStatus { get; set; } = null!;

    /// <summary>
    /// Cờ chốt chặn HO duyệt cho đơn liên cơ sở
    /// </summary>
    public bool IsApprovedByHo { get; set; }

    /// <summary>
    /// Cán bộ HTQT phụ trách chính điều phối đoàn này
    /// </summary>
    public Guid? HostUserId { get; set; }

    /// <summary>
    /// Người tạo đơn
    /// </summary>
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Fptcampus CampusCodeNavigation { get; set; } = null!;

    public virtual Useraccount CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<Delegationagenda> Delegationagenda { get; set; } = new List<Delegationagenda>();

    public virtual ICollection<Delegationmember> Delegationmembers { get; set; } = new List<Delegationmember>();

    public virtual ICollection<Forumpost> Forumposts { get; set; } = new List<Forumpost>();

    public virtual Useraccount? HostUser { get; set; }

    public virtual Meetingminute? Meetingminute { get; set; }

    public virtual ICollection<News> News { get; set; } = new List<News>();

    public virtual Partner? Partner { get; set; }

    public virtual ICollection<Resourcerequest> Resourcerequests { get; set; } = new List<Resourcerequest>();
}
