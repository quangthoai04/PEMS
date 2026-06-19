using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("role_permissions")]
public class RolePermission
{
    // SQL v8.3: surrogate PK + UNIQUE(role_id, sub_role, permission_id).
    [Key]
    [Column("role_permission_id")]
    public ulong RolePermissionId { get; set; }

    [Column("role_id")]
    public ulong RoleId { get; set; }

    [Column("sub_role")]
    public string SubRole { get; set; } = "NONE";

    [Column("permission_id")]
    public ulong PermissionId { get; set; }

    [Column("permission_level")]
    public string PermissionLevel { get; set; } = null!;

    [Column("granted_at")]
    public DateTime GrantedAt { get; set; }

    [Column("granted_by")]
    public ulong? GrantedBy { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
