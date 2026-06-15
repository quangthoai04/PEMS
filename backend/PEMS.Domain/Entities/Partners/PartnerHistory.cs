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

namespace PEMS.Domain.Entities.Partners;

[Table("partner_histories")]
public class PartnerHistory
{
    [Key]
    [Column("history_id")]
    public string HistoryId { get; set; } = null!;

    [Column("partner_id")]
    public string PartnerId { get; set; } = null!;

    [Column("event_date")]
    public DateOnly EventDate { get; set; }

    [Column("event")]
    public string Event { get; set; } = null!;

    public virtual Partner Partner { get; set; } = null!;
}
