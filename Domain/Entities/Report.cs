using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("reports")]
public class Report
{
    [Key]
    [Column("report_id")]
    public string ReportId { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("period")]
    public string? Period { get; set; }

    [Column("report_type")]
    public string ReportType { get; set; } = "Combined";

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("data_json")]
    public string? DataJson { get; set; }

    [Column("generated_by")]
    public string? GeneratedBy { get; set; }

    [Column("generated_at")]
    public DateTime GeneratedAt { get; set; }
}
