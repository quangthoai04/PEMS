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

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_participants")]
public class VisitParticipant
{
    [Key]
    [Column("participant_id")]
    public string ParticipantId { get; set; } = null!;

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("external_name")]
    public string? ExternalName { get; set; }

    [Column("participant_role")]
    public string ParticipantRole { get; set; } = "Supporter";

    [Column("is_host")]
    public bool IsHost { get; set; }

    [Column("confirmed")]
    public bool Confirmed { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual User? User { get; set; }
}
