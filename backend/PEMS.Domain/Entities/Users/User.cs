using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

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

    [Column("primary_campus_id")]
    public string? PrimaryCampusId { get; set; }

    [Column("department_id")]
    public string? DepartmentId { get; set; }

    [Column("gender")]
    public string? Gender { get; set; }

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("student_code")]
    public string? StudentCode { get; set; }

    [Column("fe_id")]
    public string? FeId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "ACTIVE";

    [Column("email_verified_at")]
    public DateTime? EmailVerifiedAt { get; set; }

    [Column("nationality")]
    public string? Nationality { get; set; }

    [Column("failed_login_count")]
    public int FailedLoginCount { get; set; }

    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    [Column("created_via")]
    public string CreatedVia { get; set; } = "MANUAL_CREATED";

    [Column("first_login_at")]
    public DateTime? FirstLoginAt { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual Campus? PrimaryCampus { get; set; }
    public virtual Department? Department { get; set; }
    public virtual ICollection<UserAuthProvider> AuthProviders { get; set; } = new List<UserAuthProvider>();
    public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}
