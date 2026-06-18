using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Campuses;

[Table("campuses")]
public class Campus
{
    [Key]
    [Column("campus_id")]
    public string CampusId { get; set; } = null!;

    [Column("campus_code")]
    public string CampusCode { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("city")]
    public string? City { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("ic_head_user_id")]
    public string? IcHeadUserId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public virtual User? IcHeadUser { get; set; }
    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
