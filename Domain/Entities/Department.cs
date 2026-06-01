using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Danh mục phòng ban chức năng điều phối nội bộ
/// </summary>
public partial class Department
{
    /// <summary>
    /// UUID định danh phòng ban
    /// </summary>
    public Guid DepartmentId { get; set; }

    /// <summary>
    /// Tên phòng ban phối hợp (Hành chính, Tuyển sinh, HTQT,...)
    /// </summary>
    public string DepartmentName { get; set; } = null!;

    /// <summary>
    /// Liên kết thuộc cơ sở nào
    /// </summary>
    public string CampusCode { get; set; } = null!;

    public virtual Fptcampus CampusCodeNavigation { get; set; } = null!;

    public virtual ICollection<Resourcerequest> Resourcerequests { get; set; } = new List<Resourcerequest>();

    public virtual ICollection<Useraccount> Useraccounts { get; set; } = new List<Useraccount>();
}
