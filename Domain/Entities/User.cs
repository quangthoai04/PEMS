using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("users")]
public class User
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    [Column("role_id")]
    public string RoleId { get; set; } = null!;

    [Column("sub_role")]
    public string? SubRole { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("department_id")]
    public string? DepartmentId { get; set; }

    [Column("gender")]
    public string? Gender { get; set; }

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("status")]
    public string Status { get; set; } = "PendingApproval";

    [Column("login_status")]
    public string LoginStatus { get; set; } = "NeverLoggedIn";

    [Column("student_code")]
    public string? StudentCode { get; set; }

    [Column("major")]
    public string? Major { get; set; }

    [Column("nationality")]
    public string? Nationality { get; set; }

    [Column("organization")]
    public string? Organization { get; set; }

    [Column("manage_scope")]
    public string? ManageScope { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual Campus? Campus { get; set; }
    public virtual Department? Department { get; set; }
}
