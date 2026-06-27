using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Emails.Commands.ToggleEmailTemplateStatus;

public sealed class ToggleEmailTemplateStatusCommandHandler : IRequestHandler<ToggleEmailTemplateStatusCommand, ToggleEmailTemplateStatusResponse>
{
    private readonly IApplicationDbContext _context;

    public ToggleEmailTemplateStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ToggleEmailTemplateStatusResponse> Handle(ToggleEmailTemplateStatusCommand request, CancellationToken cancellationToken)
    {
        var template = await _context.EmailTemplates
            .FirstOrDefaultAsync(t => t.EmailTemplateId == request.EmailTemplateId, cancellationToken);

        if (template == null)
        {
            throw new NotFoundException(nameof(PEMS.Domain.Entities.Emails.EmailTemplate), request.EmailTemplateId);
        }

        template.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

        return new ToggleEmailTemplateStatusResponse
        {
            EmailTemplateId = template.EmailTemplateId,
            Success = true,
            Message = "Status updated successfully."
        };
    }
}
