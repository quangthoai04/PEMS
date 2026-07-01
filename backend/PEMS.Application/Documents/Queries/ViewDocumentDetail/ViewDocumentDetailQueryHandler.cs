using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Documents.Queries.ViewDocumentDetail;

public sealed class ViewDocumentDetailQueryHandler : IRequestHandler<ViewDocumentDetailQuery, ViewDocumentDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ViewDocumentDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ViewDocumentDetailDto> Handle(ViewDocumentDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();

        var document = await _context.Documents
            .Include(d => d.File)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == request.DocumentId, cancellationToken);

        if (document == null)
            throw new NotFoundException("Document", request.DocumentId);

        // Check scope
        var isStaffLeader = _currentUser.RoleCode == "STAFF" && _currentUser.SubRole == "LEADER";
        if (isStaffLeader)
        {
            if (_currentUser.PrimaryCampusId == null || document.CampusId != _currentUser.PrimaryCampusId)
            {
                throw new ForbiddenException();
            }
        }

        var campus = document.CampusId.HasValue
            ? await _context.Campuses.AsNoTracking().FirstOrDefaultAsync(c => c.CampusId == document.CampusId, cancellationToken)
            : null;

        var userIds = new[] { document.CreatedBy, document.UpdatedBy, document.File?.UploadedBy }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var users = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => new UserSummaryDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl
            }, cancellationToken);

        object? ownerContext = null;

        if (document.OwnerId.HasValue)
        {
            switch (document.OwnerType)
            {
                case "VISIT":
                    var visitRequest = await _context.VisitRequests.Include(v => v.CampusInstances).AsNoTracking().FirstOrDefaultAsync(v => v.VisitRequestId == document.OwnerId, cancellationToken);
                    var visitInstance = visitRequest?.CampusInstances.FirstOrDefault();

                    if (visitRequest == null)
                    {
                        visitInstance = await _context.VisitRequestCampuses.Include(i => i.VisitRequest).AsNoTracking().FirstOrDefaultAsync(i => i.VisitInstanceId == document.OwnerId, cancellationToken);
                        if (visitInstance != null) visitRequest = visitInstance.VisitRequest;
                    }

                    if (visitRequest != null)
                    {
                        ownerContext = new 
                        {
                            VisitTitle = visitRequest.DelegationName,
                            VisitRequestId = visitRequest.VisitRequestId,
                            ExpectedStartDate = visitInstance?.PlannedStartAt,
                            ExpectedEndDate = visitInstance?.PlannedEndAt,
                            RequestStatus = visitRequest.Status,
                            HostName = "N/A"
                        };
                    }
                    break;
                case "PARTNER":
                    var partner = await _context.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.PartnerId == document.OwnerId, cancellationToken);
                    if (partner != null)
                    {
                        ownerContext = new 
                        {
                            PartnerId = partner.PartnerId,
                            PartnerName = partner.Name,
                            Country = partner.Country ?? "N/A",
                            Website = partner.WebsiteUrl,
                            Status = partner.ProfileStatus,
                            ProfileSummary = partner.Description
                        };
                    }
                    break;
                case "NEWS":
                    var news = await _context.News.AsNoTracking().FirstOrDefaultAsync(n => n.NewsId == document.OwnerId, cancellationToken);
                    if (news != null)
                    {
                        ownerContext = new 
                        {
                            NewsId = news.NewsId,
                            Title = "Bản tin " + news.NewsId,
                            Summary = "",
                            Status = news.Status,
                            CreatedAt = news.CreatedAt,
                            PublishedAt = news.PublishedAt
                        };
                    }
                    break;
                case "MINUTES":
                    var minute = await _context.Minutes.AsNoTracking().FirstOrDefaultAsync(m => m.MinutesId == document.OwnerId, cancellationToken);
                    if (minute != null)
                    {
                        var mInst = await _context.VisitRequestCampuses.Include(i => i.VisitRequest).AsNoTracking().FirstOrDefaultAsync(i => i.VisitInstanceId == minute.VisitInstanceId, cancellationToken);
                        ownerContext = new 
                        {
                            MinuteId = minute.MinutesId,
                            MinuteTitle = minute.Title,
                            Status = minute.Status,
                            VisitTitle = mInst?.VisitRequest?.DelegationName,
                            VisitRequestId = mInst?.VisitRequestId
                        };
                    }
                    break;
                // Add logic for LOGISTICS, REPORT later if needed.
                default:
                    ownerContext = new { Message = "Context details not loaded yet for this owner type." };
                    break;
            }
        }

        return new ViewDocumentDetailDto
        {
            Document = new DocumentDto
            {
                DocumentId = document.DocumentId,
                FileId = document.FileId,
                OwnerType = document.OwnerType,
                OwnerId = document.OwnerId,
                CampusId = document.CampusId,
                Title = document.Title,
                Description = document.Description,
                DocumentCategory = document.DocumentCategory,
                Status = document.Status,
                CreatedAt = document.CreatedAt,
                CreatedBy = document.CreatedBy,
                UpdatedAt = document.UpdatedAt,
                UpdatedBy = document.UpdatedBy
            },
            File = new FileDto
            {
                FileId = document.File.FileId,
                StorageProvider = document.File.StorageProvider,
                BucketName = document.File.BucketName,
                ObjectKey = document.File.ObjectKey,
                OriginalFilename = document.File.OriginalFilename,
                MimeType = document.File.MimeType,
                FileSize = document.File.FileSize,
                ChecksumSha256 = document.File.ChecksumSha256,
                UploadedBy = document.File.UploadedBy,
                UploadedAt = document.File.UploadedAt,
                ExternalFileId = document.File.ExternalFileId,
                WebViewUrl = document.File.WebViewUrl,
                DownloadUrl = document.File.DownloadUrl,
                ThumbnailUrl = document.File.ThumbnailUrl,
                FilePurpose = document.File.FilePurpose
            },
            Campus = campus != null ? new CampusDto { CampusId = campus.CampusId, CampusCode = campus.CampusCode, Name = campus.Name } : null,
            CreatedByUser = document.CreatedBy.HasValue && users.TryGetValue(document.CreatedBy.Value, out var cUser) ? cUser : null,
            UpdatedByUser = document.UpdatedBy.HasValue && users.TryGetValue(document.UpdatedBy.Value, out var uUser) ? uUser : null,
            UploadedByUser = document.File.UploadedBy.HasValue && users.TryGetValue(document.File.UploadedBy.Value, out var ulUser) ? ulUser : null,
            OwnerContext = ownerContext ?? new { Message = "Unknown context or owner record not found." }
        };
    }
}
