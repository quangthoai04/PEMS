using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("visit_details")]
public class VisitDetail
{
    [Key]
    [Column("detail_id")]
    public string DetailId { get; set; } = null!;

    [Column("visit_id")]
    public string VisitId { get; set; } = null!;

    [Column("campus_id")]
    public string CampusId { get; set; } = null!;

    [Column("visit_date")]
    public DateOnly? VisitDate { get; set; }

    [Column("start_time")]
    public TimeSpan? StartTime { get; set; }

    [Column("end_time")]
    public TimeSpan? EndTime { get; set; }

    [Column("time_zone")]
    public string? TimeZone { get; set; }

    public virtual VisitRequest VisitRequest { get; set; } = null!;
}
