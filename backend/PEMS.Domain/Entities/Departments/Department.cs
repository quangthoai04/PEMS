using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Departments;

[Table("departments")]
public class Department
{
    [Key]
    [Column("department_id")]
    public ulong DepartmentId { get; set; }

    [Column("campus_id")]
    public ulong CampusId { get; set; }

    [Column("department_code")]
    public string DepartmentCode { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("department_type")]
    public string DepartmentType { get; set; } = null!;

    [Column("head_user_id")]
    public ulong? HeadUserId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual Campus Campus { get; set; } = null!;
    public virtual User? HeadUser { get; set; }
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
