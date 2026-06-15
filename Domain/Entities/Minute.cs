using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("minutes")]
public class Minute
{
    [Key]
    [Column("minute_id")]
    public string MinuteId { get; set; } = null!;

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("guest_name")]
    public string? GuestName { get; set; }

    [Column("file_url")]
    public string? FileUrl { get; set; }

    [Column("upload_date")]
    public DateOnly? UploadDate { get; set; }

    [Column("is_draft")]
    public bool IsDraft { get; set; } = true;

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
    public virtual ICollection<MinuteParticipant> Participants { get; set; } = new List<MinuteParticipant>();
}
