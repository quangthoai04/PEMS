using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

[Table("visit_guest_members")]
public class VisitGuestMember
{
    [Key]
    [Column("guest_member_id")]
    public ulong GuestMemberId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("member_type")]
    public string MemberType { get; set; } = "GUEST";

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = null!;

    /// <summary>
    /// Display SNAPSHOT of the organization exactly as the registrant submitted it. Never rewritten
    /// when the partner is later renamed — an old request must keep reading the way it was filed.
    /// </summary>
    [Column("organization")]
    public string Organization { get; set; } = null!;

    /// <summary>
    /// The partner profile the registrant actually PICKED for this member, or null for free text /
    /// not yet determined. This — not a string comparison against <see cref="Organization"/> — is how
    /// the system knows which organization a member belongs to (PART-01).
    /// </summary>
    [Column("organization_partner_id")]
    public ulong? OrganizationPartnerId { get; set; }

    [Column("job_title")]
    public string JobTitle { get; set; } = null!;

    [Column("nationality")]
    public string Nationality { get; set; } = null!;


    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;

    // Per-campus form v2: the campus instances this member is linked to (copy-on-write on edit).
    public virtual ICollection<VisitInstanceGuestMember> InstanceLinks { get; set; } = new List<VisitInstanceGuestMember>();
}
