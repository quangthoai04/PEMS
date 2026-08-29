using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsQueryHandler : IRequestHandler<SearchDocumentsQuery, PaginatedResult<SearchDocumentsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public SearchDocumentsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<SearchDocumentsDto>> Handle(SearchDocumentsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();

        // Must enforce campus scope for every non-HO caller (Staff AND Staff Leader). This used to
        // check isStaffLeader only, so a plain Staff account fell through to the HO-only "optional
        // self-chosen CampusId filter" branch below: unscoped with no CampusId param, or able to pick
        // ANY campus's documents by passing one — exactly what the write side for the same rows
        // (VisitDocumentAccess, PartnerAccess.CanEditPartner) never allows a Staff account to do.
        // Must enforce campus scope for every non-HO caller (Staff AND Staff Leader). This used to
        // check isStaffLeader only, so a plain Staff account fell through to the HO-only "optional
        // self-chosen CampusId filter" branch below: unscoped with no CampusId param, or able to pick
        // ANY campus's documents by passing one — exactly what the write side for the same rows
        // (VisitDocumentAccess, PartnerAccess.CanEditPartner) never allows a Staff account to do.
        var isHo = _currentUser.RoleCode == "HO";
        var campusId = _currentUser.PrimaryCampusId;

        if (!isHo && campusId == null)
            throw new ForbiddenException();

        var query = _context.Documents
            .Include(d => d.File)
            .AsNoTracking()
            .AsQueryable();

        // Enforce campus scope
        if (!isHo)
        {
            query = query.Where(d => d.CampusId == campusId);
        }
        else if (request.CampusId.HasValue)
        {
            // HO: optional self-chosen filter (server never trusts this for Staff/Staff Leader —
            // their scope above always wins regardless of what the client sends).
            query = query.Where(d => d.CampusId == request.CampusId.Value);
        }

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var search = request.Q.Trim().ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(search) 
                || (d.Description != null && d.Description.ToLower().Contains(search))
                || d.File.OriginalFilename.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "All")
            query = query.Where(d => d.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.OwnerType) && request.OwnerType != "All")
            query = query.Where(d => d.OwnerType == request.OwnerType);

        if (!string.IsNullOrWhiteSpace(request.DocumentCategory) && request.DocumentCategory != "All")
            query = query.Where(d => d.DocumentCategory == request.DocumentCategory);

        if (!string.IsNullOrWhiteSpace(request.StorageProvider) && request.StorageProvider != "All")
            query = query.Where(d => d.File.StorageProvider == request.StorageProvider);

        if (!string.IsNullOrWhiteSpace(request.UploadedFrom) && DateTime.TryParse(request.UploadedFrom, out var fromDate))
            query = query.Where(d => d.File.UploadedAt >= fromDate);

        if (!string.IsNullOrWhiteSpace(request.UploadedTo) && DateTime.TryParse(request.UploadedTo, out var toDate))
        {
            toDate = toDate.Date.AddDays(1).AddTicks(-1);
            query = query.Where(d => d.File.UploadedAt <= toDate);
        }

        // Apply sorting
        var isDesc = string.Equals(request.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? false : true;
        
        query = request.SortBy?.ToLower() switch
        {
            "title" => isDesc ? query.OrderByDescending(d => d.Title) : query.OrderBy(d => d.Title),
            "status" => isDesc ? query.OrderByDescending(d => d.Status) : query.OrderBy(d => d.Status),
            "created_at" => isDesc ? query.OrderByDescending(d => d.CreatedAt) : query.OrderBy(d => d.CreatedAt),
            "uploaded_at" => isDesc ? query.OrderByDescending(d => d.File.UploadedAt) : query.OrderBy(d => d.File.UploadedAt),
            _ => isDesc ? query.OrderByDescending(d => d.File.UploadedAt) : query.OrderBy(d => d.File.UploadedAt)
        };

        var totalItems = await query.CountAsync(cancellationToken);

        // DB-PAGE-002: bound page/pageSize the same way the other list queries already do.
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new SearchDocumentsDto
            {
                DocumentId = d.DocumentId,
                Title = d.Title,
                Description = d.Description,
                DocumentCategory = d.DocumentCategory,
                Status = d.Status,
                OwnerType = d.OwnerType,
                OwnerId = d.OwnerId,
                OwnerDisplayName = null, // Can be mapped later if needed
                CampusId = d.CampusId,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                FileId = d.File.FileId,
                OriginalFilename = d.File.OriginalFilename,
                MimeType = d.File.MimeType,
                FileSize = d.File.FileSize,
                StorageProvider = d.File.StorageProvider,
                DownloadUrl = d.File.DownloadUrl,
                WebViewUrl = d.File.WebViewUrl,
                ThumbnailUrl = d.File.ThumbnailUrl
            })
            .ToListAsync(cancellationToken);

        return PaginatedResult<SearchDocumentsDto>.Create(items, page, pageSize, totalItems);
    }
}