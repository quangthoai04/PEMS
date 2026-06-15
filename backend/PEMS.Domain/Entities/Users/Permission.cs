using PEMS.Domain.Entities.AgendaTemplates;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Feedbacks;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Minutes;
using PEMS.Domain.Entities.News;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Reports;
using PEMS.Domain.Entities.Tasks;
using PEMS.Domain.Entities.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Users;

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
