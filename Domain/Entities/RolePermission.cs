using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("role_permissions")]
public class RolePermission
{
    [Column("role_id")]
    public string RoleId { get; set; } = null!;

    [Column("permission_id")]
    public string PermissionId { get; set; } = null!;

    [Column("granted_at")]
    public DateTime GrantedAt { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
