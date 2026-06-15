using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

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

    [Column("location")]
    public string? Location { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("ic_head_user_id")]
    public string? IcHeadUserId { get; set; }

    [Column("capacity")]
    public int? Capacity { get; set; }

    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
