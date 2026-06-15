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

namespace PEMS.Domain.Entities.Minutes;

[Table("minute_participants")]
public class MinuteParticipant
{
    [Key]
    [Column("mp_id")]
    public string MpId { get; set; } = null!;

    [Column("minute_id")]
    public string MinuteId { get; set; } = null!;

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("role_title")]
    public string? RoleTitle { get; set; }

    [Column("organization")]
    public string? Organization { get; set; }

    [Column("is_internal")]
    public bool IsInternal { get; set; }

    [Column("is_partner")]
    public bool IsPartner { get; set; }

    [Column("confirmed")]
    public bool Confirmed { get; set; }

    public virtual Minute Minute { get; set; } = null!;
}
