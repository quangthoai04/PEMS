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

        // If not HO, or if requesting for use in composition, only see ACTIVE templates
        if (roleCode != "HO" || string.Equals(request.Mode, "use", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.Status == "ACTIVE");
        }
        else if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(t => t.Status == request.Status);
        }

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            var keyword = request.Keyword.ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(keyword) || t.TemplateCode.ToLower().Contains(keyword) || (t.SubjectVi != null && t.SubjectVi.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrEmpty(request.Purpose))
        {
            query = query.Where(t => t.Purpose == request.Purpose);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var templates = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return new ViewEmailTemplateListDto 
        { 
            Templates = templates,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }
}