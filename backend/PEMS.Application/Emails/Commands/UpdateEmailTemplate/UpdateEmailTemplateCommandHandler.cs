using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Commands.UpdateEmailTemplate;

public sealed class UpdateEmailTemplateCommandHandler : IRequestHandler<UpdateEmailTemplateCommand, UpdateEmailTemplateResponse>
{
    private readonly IApplicationDbContext _context;

    public UpdateEmailTemplateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UpdateEmailTemplateResponse> Handle(UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.EmailTemplates
            .FirstOrDefaultAsync(t => t.EmailTemplateId == request.EmailTemplateId, cancellationToken);

        if (template == null)
        {
            throw new NotFoundException(nameof(PEMS.Domain.Entities.Emails.EmailTemplate), request.EmailTemplateId);
        }

        template.Name = request.Name;
        template.Purpose = request.Purpose;
        template.CampusId = request.CampusId;
        template.Description = request.Description;
        template.SubjectVi = request.SubjectVi;
        template.BodyVi = request.BodyVi;
        template.SubjectEn = request.SubjectEn;
        template.BodyEn = request.BodyEn;
        template.BodyFormat = Enum.TryParse<EmailBodyFormat>(request.BodyFormat, out var format) ? format : EmailBodyFormat.HTML;
        template.VariablesText = request.VariablesText;
        template.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateEmailTemplateResponse
        {
            EmailTemplateId = template.EmailTemplateId,
            Success = true,
            Message = "Updated successfully."
        };
    }
}