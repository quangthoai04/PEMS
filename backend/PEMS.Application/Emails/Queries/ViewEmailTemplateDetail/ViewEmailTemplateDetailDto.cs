using System;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateDetail;

public sealed class ViewEmailTemplateDetailDto
{
    public ulong EmailTemplateId { get; set; }
    public string TemplateCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public ulong? CampusId { get; set; }
    public string? Description { get; set; }
    public string? SubjectVi { get; set; }
    public string? BodyVi { get; set; }
    public string? SubjectEn { get; set; }
    public string? BodyEn { get; set; }
    public string BodyFormat { get; set; } = null!;
    public string? VariablesText { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}