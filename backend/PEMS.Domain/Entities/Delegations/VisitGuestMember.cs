using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_guest_members")]
public class VisitGuestMember
{
    [Key]
    [Column("guest_member_id")]
    public string GuestMemberId { get; set; } = null!;

    [Column("visit_request_id")]
    public string VisitRequestId { get; set; } = null!;

    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [Column("organization")]
    public string? Organization { get; set; }

    [Column("job_title")]
    public string? JobTitle { get; set; }

    [Column("nationality")]
    public string? Nationality { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("is_representative")]
    public bool IsRepresentative { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
}
