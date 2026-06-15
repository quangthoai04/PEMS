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

namespace PEMS.Domain.Entities.Tasks;

[Table("task_actions")]
public class TaskAction
{
    [Key]
    [Column("action_id")]
    public string ActionId { get; set; } = null!;

    [Column("task_id")]
    public string TaskId { get; set; } = null!;

    [Column("action_type")]
    public string ActionType { get; set; } = null!;

    [Column("approved_by")]
    public string? ApprovedBy { get; set; }

    [Column("signature_date")]
    public DateTime? SignatureDate { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    public virtual PemsTask Task { get; set; } = null!;
}
