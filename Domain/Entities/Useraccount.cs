using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Bảng lưu trữ thông tin tài khoản toàn hệ thống
/// </summary>
public partial class Useraccount
{
    /// <summary>
    /// UUID người dùng
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Email đăng nhập (SSO FPT hoặc email cá nhân của Guest)
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Mật khẩu mã hóa (Chỉ dùng cho Guest đăng ký ngoài)
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Họ và tên người dùng
    /// </summary>
    public string FullName { get; set; } = null!;

    /// <summary>
    /// Số điện thoại liên hệ
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Mã quyền tối cao
    /// </summary>
    public string RoleCode { get; set; } = null!;

    /// <summary>
    /// Thuộc phòng ban nào (Guest/Student để NULL)
    /// </summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Thuộc cơ sở quản lý nào (HO để NULL)
    /// </summary>
    public string? CampusCode { get; set; }

    /// <summary>
    /// Trạng thái kích hoạt tài khoản (1: Active, 0: Disabled)
    /// </summary>
    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Actionitem> Actionitems { get; set; } = new List<Actionitem>();

    public virtual Fptcampus? CampusCodeNavigation { get; set; }

    public virtual ICollection<Delegation> DelegationCreatedByNavigations { get; set; } = new List<Delegation>();

    public virtual ICollection<Delegation> DelegationHostUsers { get; set; } = new List<Delegation>();

    public virtual ICollection<Delegationmember> Delegationmembers { get; set; } = new List<Delegationmember>();

    public virtual Department? Department { get; set; }

    public virtual ICollection<Forumcomment> Forumcomments { get; set; } = new List<Forumcomment>();

    public virtual ICollection<Forumpost> Forumposts { get; set; } = new List<Forumpost>();

    public virtual ICollection<News> News { get; set; } = new List<News>();

    public virtual ICollection<Partner> Partners { get; set; } = new List<Partner>();

    public virtual ICollection<Partnersynclog> Partnersynclogs { get; set; } = new List<Partnersynclog>();

    public virtual ICollection<Resourcerequest> Resourcerequests { get; set; } = new List<Resourcerequest>();

    public virtual Userrole RoleCodeNavigation { get; set; } = null!;
}
