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

namespace PEMS.Domain.Entities.Documents;

[Table("documents")]
public class Document
{
    [Key]
    [Column("document_id")]
    public string DocumentId { get; set; } = null!;

    [Column("file_name")]
    public string FileName { get; set; } = null!;

    [Column("file_size")]
    public string? FileSize { get; set; }

    [Column("file_type")]
    public string? FileType { get; set; }

    [Column("file_url")]
    public string FileUrl { get; set; } = null!;

    [Column("category")]
    public string? Category { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("uploaded_by")]
    public string? UploadedBy { get; set; }

    [Column("campus_id")]
    public string? CampusId { get; set; }

    [Column("visit_id")]
    public string? VisitId { get; set; }

    [Column("upload_date")]
    public DateTime UploadDate { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
}
