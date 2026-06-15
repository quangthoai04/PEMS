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

[Table("tasks")]
public class PemsTask
{
    [Key]
    [Column("task_id")]
    public string TaskId { get; set; } = null!;

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("task_type")]
    public string TaskType { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("assigned_to_user_id")]
    public string? AssignedToUserId { get; set; }

    [Column("department_id")]
    public string? DepartmentId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("proposed_time")]
    public string? ProposedTime { get; set; }

    [Column("proposed_content")]
    public string? ProposedContent { get; set; }

    [Column("proposed_by")]
    public string? ProposedBy { get; set; }

    [Column("reject_reason")]
    public string? RejectReason { get; set; }

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

    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual Department? Department { get; set; }
    public virtual ICollection<TaskAction> Actions { get; set; } = new List<TaskAction>();
}
