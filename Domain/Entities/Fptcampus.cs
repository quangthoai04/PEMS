using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Danh mục 5 cơ sở Đại học FPT toàn quốc
/// </summary>
public partial class Fptcampus
{
    /// <summary>
    /// Mã cơ sở (HL, HCM, DN, qn, CT)
    /// </summary>
    public string CampusCode { get; set; } = null!;

    /// <summary>
    /// Tên cơ sở hiển thị (Hòa Lạc, TP. Hồ Chí Minh...)
    /// </summary>
    public string CampusName { get; set; } = null!;

    public virtual ICollection<Delegation> Delegations { get; set; } = new List<Delegation>();

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<Useraccount> Useraccounts { get; set; } = new List<Useraccount>();
}
