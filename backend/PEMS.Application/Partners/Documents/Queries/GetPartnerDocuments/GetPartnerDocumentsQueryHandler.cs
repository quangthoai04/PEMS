using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Documents.Queries.GetPartnerDocuments;

public sealed class GetPartnerDocumentsQueryHandler
    : IRequestHandler<GetPartnerDocumentsQuery, List<PartnerDocumentDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPartnerDocumentsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<PartnerDocumentDto>> Handle(
        GetPartnerDocumentsQuery request, CancellationToken cancellationToken)
    {
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartnerId == request.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", request.PartnerId);

        if (!PartnerAccess.CanViewPartner(_currentUser, partner))
            throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                "Bạn không có quyền xem tài liệu của đối tác này.", 403);

        var docs = await _db.Documents.AsNoTracking()
            .Where(d => d.OwnerType == "PARTNER" && d.OwnerId == request.PartnerId)
            .OrderByDescending(d => d.DocumentId)
            .Select(d => new
            {
                d.DocumentId, d.FileId, d.Title, d.Description, d.DocumentCategory,
                d.Status, d.CreatedAt, d.CreatedBy,
            })
            .ToListAsync(cancellationToken);
        if (docs.Count == 0) return new List<PartnerDocumentDto>();

        var fileIds = docs.Select(d => d.FileId).Distinct().ToList();
        var files = await _db.Files.AsNoTracking()
            .Where(f => fileIds.Contains(f.FileId))
            .Select(f => new { f.FileId, f.OriginalFilename, f.MimeType, f.FileSize, f.DownloadUrl })
            .ToListAsync(cancellationToken);
        var filesById = files.ToDictionary(f => f.FileId);

        var creatorIds = docs.Where(d => d.CreatedBy != null).Select(d => d.CreatedBy!.Value).Distinct().ToList();
        var creators = creatorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => creatorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        return docs.Select(d => new PartnerDocumentDto
        {
            DocumentId = d.DocumentId,
            FileId = d.FileId,
            Title = d.Title,
            Description = d.Description,
            DocumentCategory = d.DocumentCategory,
            Status = d.Status,
            OriginalFilename = filesById.TryGetValue(d.FileId, out var f) ? f.OriginalFilename : null,
            MimeType = filesById.TryGetValue(d.FileId, out var f2) ? f2.MimeType : null,
            FileSize = filesById.TryGetValue(d.FileId, out var f3) ? (long?)f3.FileSize : null,
            DownloadUrl = filesById.TryGetValue(d.FileId, out var f4) ? f4.DownloadUrl : null,
            CreatedAt = d.CreatedAt,
            CreatorName = d.CreatedBy != null && creators.TryGetValue(d.CreatedBy.Value, out var n) ? n : null,
        }).ToList();
    }
}
