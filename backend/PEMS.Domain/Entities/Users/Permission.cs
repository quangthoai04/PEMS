using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

[Table("permissions")]
public class Permission
{
    [Key]
    [Column("permission_id")]
    public ulong PermissionId { get; set; }

    [Column("permission_code")]
    public string PermissionCode { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("permission_group")]
    public string PermissionGroup { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("is_system")]
    public bool IsSystem { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
