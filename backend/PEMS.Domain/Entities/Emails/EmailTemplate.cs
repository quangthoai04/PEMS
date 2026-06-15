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

namespace PEMS.Domain.Entities.Emails;

[Table("email_templates")]
public class EmailTemplate
{
    [Key]
    [Column("template_id")]
    public string TemplateId { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("body")]
    public string? Body { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "InUse";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
