using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("permissions")]
public class Permission
{
    [Key]
    [Column("permission_id")]
    public string PermissionId { get; set; } = null!;

    [Column("permission_code")]
    public string PermissionCode { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("permission_group")]
    public string PermissionGroup { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
