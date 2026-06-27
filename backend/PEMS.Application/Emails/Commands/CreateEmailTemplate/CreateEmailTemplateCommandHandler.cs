using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.CreateEmailTemplate;

public sealed class CreateEmailTemplateCommandHandler : IRequestHandler<CreateEmailTemplateCommand, CreateEmailTemplateResponse>
{
    private readonly IApplicationDbContext _context;

    public CreateEmailTemplateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CreateEmailTemplateResponse> Handle(CreateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = new EmailTemplate
        {
            TemplateCode = request.TemplateCode,
            Name = request.Name,
            Purpose = request.Purpose,
            CampusId = request.CampusId,
            Description = request.Description,
            SubjectVi = request.SubjectVi,
            BodyVi = request.BodyVi,
            SubjectEn = request.SubjectEn,
            BodyEn = request.BodyEn,
            BodyFormat = Enum.TryParse<EmailBodyFormat>(request.BodyFormat, out var format) ? format : EmailBodyFormat.HTML,
            VariablesText = request.VariablesText,
            Status = request.Status,
        };

        _context.EmailTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateEmailTemplateResponse
        {
            EmailTemplateId = template.EmailTemplateId,
            Success = true,
            Message = "Created successfully."
        };
    }
}