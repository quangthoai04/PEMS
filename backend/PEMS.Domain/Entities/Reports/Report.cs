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

namespace PEMS.Domain.Entities.Reports;

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
