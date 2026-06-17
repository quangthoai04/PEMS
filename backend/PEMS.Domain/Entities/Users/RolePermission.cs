using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("role_permissions")]
public class RolePermission
{
    [Column("role_id")]
    public string RoleId { get; set; } = null!;

    [Column("permission_id")]
    public string PermissionId { get; set; } = null!;

    [Column("sub_role")]
    public string SubRole { get; set; } = null!;

    [Column("permission_level")]
    public string PermissionLevel { get; set; } = null!;

    [Column("granted_at")]
    public DateTime GrantedAt { get; set; }

    [Column("granted_by")]
    public string? GrantedBy { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
