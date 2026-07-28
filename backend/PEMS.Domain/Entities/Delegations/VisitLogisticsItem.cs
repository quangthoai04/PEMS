using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_logistics_items")]
public class VisitLogisticsItem
{
    [Key]
    [Column("logistics_item_id")]
    public ulong LogisticsItemId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("item_type")]
    public string ItemType { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("quantity")]
    public int? Quantity { get; set; }

    [Column("usage_start_at")]
    public DateTime? UsageStartAt { get; set; }

    [Column("usage_end_at")]
    public DateTime? UsageEndAt { get; set; }

    [Column("status")]
    public string Status { get; set; } = "REQUESTED";

    // PEMS v11: how this request is coordinated.
    //   SYSTEM_REQUEST     = sent to a department through the system (email/notification + workflow).
    //   OFFLINE_COORDINATED = discussed/handled outside the system, recorded for traceability only
    //                         (no email/notification; usually created with status DONE).
    [Column("coordination_mode")]
    public string CoordinationMode { get; set; } = "SYSTEM_REQUEST";

    [Column("offline_coordination_note")]
    public string? OfflineCoordinationNote { get; set; }

    [Column("requested_by")]
    public ulong? RequestedBy { get; set; }

    [Column("requested_to_department_id")]
    public ulong? RequestedToDepartmentId { get; set; }

    [Column("requested_at")]
    public DateTime? RequestedAt { get; set; }

    [Column("received_by")]
    public ulong? ReceivedBy { get; set; }

    [Column("received_at")]
    public DateTime? ReceivedAt { get; set; }

    [Column("assigned_to_user_id")]
    public ulong? AssignedToUserId { get; set; }

    [Column("assigned_by")]
    public ulong? AssignedBy { get; set; }

    [Column("assigned_at")]
    public DateTime? AssignedAt { get; set; }

    [Column("assignee_accepted_at")]
    public DateTime? AssigneeAcceptedAt { get; set; }

    [Column("assignee_response_note")]
    public string? AssigneeResponseNote { get; set; }

    [Column("due_at")]
    public DateTime? DueAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("proposed_by")]
    public ulong? ProposedBy { get; set; }

    [Column("proposed_at")]
    public DateTime? ProposedAt { get; set; }

    [Column("proposed_quantity")]
    public int? ProposedQuantity { get; set; }

    [Column("proposed_usage_start_at")]
    public DateTime? ProposedUsageStartAt { get; set; }

    [Column("proposed_usage_end_at")]
    public DateTime? ProposedUsageEndAt { get; set; }

    [Column("proposed_description")]
    public string? ProposedDescription { get; set; }

    [Column("proposal_note")]
    public string? ProposalNote { get; set; }

    [Column("proposal_responded_by")]
    public ulong? ProposalRespondedBy { get; set; }

    [Column("proposal_responded_at")]
    public DateTime? ProposalRespondedAt { get; set; }

    [Column("proposal_response")]
    public string? ProposalResponse { get; set; }

    [Column("proposal_response_note")]
    public string? ProposalResponseNote { get; set; }

    [Column("decision_note")]
    public string? DecisionNote { get; set; }

    [Column("row_version")]
    public int RowVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;

    // PEMS v10: borrow/return signatures live in visit_logistics_item_handovers.
    public virtual ICollection<VisitLogisticsItemHandover> Handovers { get; set; } = new List<VisitLogisticsItemHandover>();
}
