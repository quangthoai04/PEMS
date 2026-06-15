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

[Table("visit_requests")]
public class VisitRequest
{
    [Key]
    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("guest_org")]
    public string? GuestOrg { get; set; }

    [Column("guest_name")]
    public string? GuestName { get; set; }

    [Column("visit_mode")]
    public string VisitMode { get; set; } = "single";

    [Column("visit_types")]
    public string? VisitTypes { get; set; }

    [Column("purpose")]
    public string? Purpose { get; set; }

    [Column("work_content")]
    public string? WorkContent { get; set; }

    [Column("pax")]
    public int? Pax { get; set; }

    [Column("campus_id")]
    public string CampusId { get; set; } = null!;

    [Column("partner_id")]
    public string? PartnerId { get; set; }

    [Column("host_user_id")]
    public string? HostUserId { get; set; }

    [Column("sender_user_id")]
    public string? SenderUserId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Cho duyet";

    [Column("reject_reason")]
    public string? RejectReason { get; set; }

    [Column("scheduled_time")]
    public DateTime? ScheduledTime { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    public virtual Campus Campus { get; set; } = null!;
    public virtual Partner? Partner { get; set; }
    public virtual ICollection<VisitDetail> Details { get; set; } = new List<VisitDetail>();
    public virtual ICollection<VisitParticipant> Participants { get; set; } = new List<VisitParticipant>();
    public virtual ICollection<VisitAgenda> Agendas { get; set; } = new List<VisitAgenda>();
    public virtual ICollection<PemsTask> Tasks { get; set; } = new List<PemsTask>();
    public virtual ICollection<Minute> Minutes { get; set; } = new List<Minute>();
    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
