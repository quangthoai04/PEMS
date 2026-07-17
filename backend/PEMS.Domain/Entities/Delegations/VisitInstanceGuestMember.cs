using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Delegations;

/// <summary>
/// Per-campus link between a guest/support member and a campus instance (per-campus form v2).
/// Composite PK <c>(visit_instance_id, guest_member_id)</c>. The two composite FKs both carry
/// <c>visit_request_id</c> so the DB guarantees member and instance belong to the SAME request
/// (blocks linking a member of request A into an instance of request B). Copy-on-write: when a
/// campus that shares a legacy member is first edited, the member row is cloned and the link
/// repointed, so editing campus A never changes campus B.
/// </summary>
[Table("visit_instance_guest_members")]
public class VisitInstanceGuestMember
{
    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong VisitInstanceId { get; set; }

    [Column("guest_member_id")]
    public ulong GuestMemberId { get; set; }

    [Column("display_order")]
    public uint DisplayOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    public virtual VisitRequestCampus VisitInstance { get; set; } = null!;
    public virtual VisitGuestMember GuestMember { get; set; } = null!;
}
