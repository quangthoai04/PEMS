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

        // Must enforce campus scope for STAFF LEADER
        var isStaffLeader = _currentUser.RoleCode == "STAFF" && _currentUser.SubRole == "LEADER";
        var campusId = _currentUser.PrimaryCampusId;

        if (isStaffLeader && campusId == null)
            throw new ForbiddenException();

        var query = _context.Documents
            .Include(d => d.File)
            .AsNoTracking()
            .AsQueryable();

        // Enforce campus scope
        if (isStaffLeader)
        {
            query = query.Where(d => d.CampusId == campusId);
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

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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

        return PaginatedResult<SearchDocumentsDto>.Create(items, request.Page, request.PageSize, totalItems);
    }
}