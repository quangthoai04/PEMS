using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Minutes;

namespace PEMS.Domain.Entities.Partners;

/// <summary>
/// Links a visit guest member and/or minute participant to a partner profile so
/// meeting/minutes screens can show a partner badge and partners accumulate a
/// cooperation history. At least one of GuestMemberId / MinuteParticipantId must
/// be set (DB CHECK is not possible with SET NULL FKs — enforced by validators).
/// </summary>
[Table("visit_guest_partner_links")]
public class VisitGuestPartnerLink
{
    [Key]
    [Column("link_id")]
    public ulong LinkId { get; set; }

    [Column("visit_request_id")]
    public ulong VisitRequestId { get; set; }

    [Column("visit_instance_id")]
    public ulong? VisitInstanceId { get; set; }

    [Column("guest_member_id")]
    public ulong? GuestMemberId { get; set; }

    [Column("minute_participant_id")]
    public ulong? MinuteParticipantId { get; set; }

    [Column("partner_id")]
    public ulong PartnerId { get; set; }

    [Column("partner_contact_id")]
    public ulong? PartnerContactId { get; set; }

    /// <summary>AUTO_NAME | AUTO_EMAIL_DOMAIN | MANUAL | CREATED_FROM_GUEST | BUSINESS_CARD_OCR</summary>
    [Column("match_source")]
    public string MatchSource { get; set; } = "MANUAL";

    /// <summary>SUGGESTED | CONFIRMED | REJECTED</summary>
    [Column("match_status")]
    public string MatchStatus { get; set; } = "CONFIRMED";

    [Column("confidence_score")]
    public decimal? ConfidenceScore { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public virtual Partner Partner { get; set; } = null!;
    public virtual PartnerContact? PartnerContact { get; set; }
    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual VisitRequestCampus? VisitInstance { get; set; }
    public virtual VisitGuestMember? GuestMember { get; set; }
    public virtual MinuteParticipant? MinuteParticipant { get; set; }
}
