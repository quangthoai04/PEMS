using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

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
