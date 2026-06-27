using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Queries.ViewEmailTemplateList;

public sealed class ViewEmailTemplateListQueryHandler : IRequestHandler<ViewEmailTemplateListQuery, ViewEmailTemplateListDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ViewEmailTemplateListQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ViewEmailTemplateListDto> Handle(ViewEmailTemplateListQuery request, CancellationToken cancellationToken)
    {
        var roleCode = _currentUserService.RoleCode;
        var query = _context.EmailTemplates.AsQueryable();

        // If not HO, only see ACTIVE templates
        if (roleCode != "HO")
        {
            query = query.Where(t => t.Status == "ACTIVE");
        }

        var templates = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new EmailTemplateListItemDto
            {
                EmailTemplateId = t.EmailTemplateId,
                TemplateCode = t.TemplateCode,
                Name = t.Name,
                Purpose = t.Purpose,
                Description = t.Description,
                Status = t.Status,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new ViewEmailTemplateListDto { Templates = templates };
    }
}