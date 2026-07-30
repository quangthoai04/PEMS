using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateDetail;

public sealed class ViewEmailTemplateDetailQueryHandler : IRequestHandler<ViewEmailTemplateDetailQuery, ViewEmailTemplateDetailDto>
{
    private readonly IApplicationDbContext _context;

    public ViewEmailTemplateDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ViewEmailTemplateDetailDto> Handle(ViewEmailTemplateDetailQuery request, CancellationToken cancellationToken)
    {
        var template = await _context.EmailTemplates
            .FirstOrDefaultAsync(t => t.EmailTemplateId == request.EmailTemplateId, cancellationToken);

        if (template == null)
        {
            throw new NotFoundException(nameof(PEMS.Domain.Entities.Emails.EmailTemplate), request.EmailTemplateId);
        }

        return new ViewEmailTemplateDetailDto
        {
            EmailTemplateId = template.EmailTemplateId,
            TemplateCode = template.TemplateCode,
            Name = template.Name,
            Purpose = template.Purpose,
            CampusId = template.CampusId,
            Description = template.Description,
            SubjectVi = template.SubjectVi,
            BodyVi = template.BodyVi,
            SubjectEn = template.SubjectEn,
            BodyEn = template.BodyEn,
            BodyFormat = template.BodyFormat.ToString(),
            VariablesText = template.VariablesText,
            Status = template.Status,
            Revision = template.Revision,
            HasShippedDefault = Emails.Common.EmailTemplateDefaults.For(template.TemplateCode) is not null,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }
}