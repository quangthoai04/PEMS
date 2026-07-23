using MediatR;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.CreateEmailTemplate;

public class CreateEmailTemplateCommand : IRequest<CreateEmailTemplateResponse>
{
    public string TemplateCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    /// <summary>Mandatory: the column is a NOT NULL ENUM of two values (see the validator).</summary>
    public string Purpose { get; set; } = null!;
    public ulong? CampusId { get; set; }
    public string? Description { get; set; }
    public string? SubjectVi { get; set; }
    public string? BodyVi { get; set; }
    public string? SubjectEn { get; set; }
    public string? BodyEn { get; set; }
    public string BodyFormat { get; set; } = "HTML";
    public string? VariablesText { get; set; }
    public string Status { get; set; } = "ACTIVE";
}