using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Bảng phân quyền vai trò hệ thống cố định
/// </summary>
public partial class Userrole
{
    /// <summary>
    /// Mã vai trò (HO, Admin, Staff, Student, Guest)
    /// </summary>
    public string RoleCode { get; set; } = null!;

    /// <summary>
    /// Tên hiển thị vai trò tiếng Anh
    /// </summary>
    public string RoleName { get; set; } = null!;

    public virtual ICollection<Useraccount> Useraccounts { get; set; } = new List<Useraccount>();
}
