using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos;
using PEMS.Application.Partners.Commands.CreatePartner;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.VisitDocuments.Commands.UploadVisitDocument;

public sealed class UploadVisitDocumentCommandHandler
    : IRequestHandler<UploadVisitDocumentCommand, UploadVisitDocumentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;
    private readonly IFileUploadService _fileUpload;
    private readonly IFileValidationPolicy _validationPolicy;
    private readonly IVisitPhotoFolderService _folderService;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IDateTimeService _clock;
    private readonly ILogger<UploadVisitDocumentCommandHandler> _logger;

    public UploadVisitDocumentCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IMediator mediator,
        IFileUploadService fileUpload,
        IFileValidationPolicy validationPolicy,
        IVisitPhotoFolderService folderService,
        IGoogleDriveStorageService drive,
        IDateTimeService clock,
        ILogger<UploadVisitDocumentCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _mediator = mediator;
        _fileUpload = fileUpload;
        _validationPolicy = validationPolicy;
        _folderService = folderService;
        _drive = drive;
        _clock = clock;
        _logger = logger;
    }

    public async Task<UploadVisitDocumentResponse> Handle(
        UploadVisitDocumentCommand request, CancellationToken cancellationToken)
    {
        var scope = await VisitInstanceMediaAccessScope.ResolveAsync(
            _db, _currentUser, request.VisitInstanceId, cancellationToken);

        if (!scope.CanUpload)
            throw new BusinessRuleException(
                "Chuyến tiếp khách đã bị hủy — không thể tải tài liệu lên.", "VISIT_DOCUMENT_INSTANCE_CANCELLED");

        var rule = _validationPolicy.GetRule(FilePurpose.VisitRequestAttachment);
        FileContentValidator.Validate(request.FileName, request.ContentType, request.Content, rule);

        // Resolve/create every tagged partner BEFORE touching Drive — a partner-creation or
        // authorization failure must never leave an orphaned uploaded file behind.
        var partnerNames = (request.PartnerNames ?? new List<string>())
            .Select(n => n?.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var partners = new List<Partner>();
        foreach (var name in partnerNames)
        {
            var partner = await ResolveOrCreatePartnerAsync(name!, cancellationToken);
            if (!PartnerAccess.CanManagePartnerChildren(_currentUser, partner))
                throw new AuthBusinessException(PartnerErrorCodes.Forbidden,
                    $"Bạn không có quyền gắn tài liệu cho đối tác \"{partner.Name}\".", 403);
            partners.Add(partner);
        }

        var documentSubtype = partners.Count > 0
            ? VisitDocumentSubtypes.DoiTac
            : VisitDocumentSubtypes.TheoDoanKhach;
        var target = await _folderService.EnsureDocumentUploadTargetAsync(
            scope.Instance, scope.CampusCode, documentSubtype, scope.UserId, cancellationToken);

        await using var stream = new MemoryStream(request.Content);
        var uploaded = await _fileUpload.UploadBusinessFileAsync(
            stream, request.FileName, request.ContentType, request.Content.LongLength,
            FilePurpose.VisitRequestAttachment, (long)scope.UserId,
            target.DocumentFolderExternalId, cancellationToken);

        var now = _clock.VietnamNow;
        var title = request.Title.Trim();
        var partnerDtos = new List<UploadedVisitDocumentPartnerDto>();
        ulong? visitDocumentId = null;

        try
        {
            if (partners.Count > 0)
            {
                foreach (var partner in partners)
                {
                    var doc = new Document
                    {
                        FileId = (ulong)uploaded.FileId,
                        OwnerType = "PARTNER",
                        OwnerId = partner.PartnerId,
                        CampusId = scope.Instance.CampusId,
                        Title = title,
                        Status = "PUBLISHED",
                        CreatedAt = now,
                        CreatedBy = scope.UserId,
                    };
                    _db.Documents.Add(doc);
                    await _db.SaveChangesAsync(cancellationToken); // populates doc.DocumentId
                    partnerDtos.Add(new UploadedVisitDocumentPartnerDto
                    {
                        DocumentId = doc.DocumentId,
                        PartnerId = partner.PartnerId,
                        PartnerName = partner.Name,
                    });
                }
            }
            else
            {
                var doc = new Document
                {
                    FileId = (ulong)uploaded.FileId,
                    OwnerType = "VISIT",
                    OwnerId = scope.Instance.VisitRequestId,
                    CampusId = scope.Instance.CampusId,
                    Title = title,
                    Status = "PUBLISHED",
                    CreatedAt = now,
                    CreatedBy = scope.UserId,
                };
                _db.Documents.Add(doc);
                await _db.SaveChangesAsync(cancellationToken);
                visitDocumentId = doc.DocumentId;
            }
        }
        catch
        {
            await CleanUpOrphanedUploadAsync((ulong)uploaded.FileId, uploaded.ExternalFileId);
            throw;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = scope.UserId,
            CampusId = scope.Instance.CampusId,
            Action = "UPLOAD_VISIT_DOCUMENT",
            EntityType = "VisitRequestCampus",
            EntityId = scope.Instance.VisitInstanceId,
            VisitRequestId = scope.Instance.VisitRequestId,
            VisitInstanceId = scope.Instance.VisitInstanceId,
            Reason = partners.Count > 0
                ? $"partners={string.Join(',', partners.Select(p => p.PartnerId))}"
                : "ownerType=VISIT",
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new UploadVisitDocumentResponse
        {
            FileId = (ulong)uploaded.FileId,
            FileName = request.FileName,
            Url = uploaded.FileUrl,
            Partners = partnerDtos,
            VisitDocumentId = visitDocumentId,
        };
    }

    /// <summary>
    /// Same resolve-or-create rule the frontend used to run itself before this endpoint existed:
    /// match an existing partner by normalized name/alias, else create one (source MANUAL) via the
    /// real <see cref="CreatePartnerCommand"/> so translations/aliases/notifications stay identical
    /// to any other partner-creation path.
    /// </summary>
    private async Task<Partner> ResolveOrCreatePartnerAsync(string name, CancellationToken cancellationToken)
    {
        var nameKey = PartnerNormalization.NormalizeKey(name);
        var existingId = await _db.PartnerAliases
            .Where(a => a.AliasNameKey == nameKey && a.Status == "ACTIVE")
            .Select(a => a.PartnerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingId != 0)
        {
            var existing = await _db.Partners
                .FirstOrDefaultAsync(p => p.PartnerId == existingId, cancellationToken);
            if (existing != null)
                return existing;
        }

        var created = await _mediator.Send(
            new CreatePartnerCommand { Name = name, Source = "MANUAL" }, cancellationToken);
        return await _db.Partners.FirstOrDefaultAsync(p => p.PartnerId == created.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner", created.PartnerId);
    }

    private async Task CleanUpOrphanedUploadAsync(ulong fileId, string? externalFileId)
    {
        try
        {
            var fileRow = await _db.Files.FirstOrDefaultAsync(f => f.FileId == fileId, CancellationToken.None);
            if (fileRow != null)
            {
                _db.Files.Remove(fileRow);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to remove orphaned files row {FileId} after visit document link failure.", fileId);
        }

        if (!string.IsNullOrWhiteSpace(externalFileId))
        {
            try { await _drive.DeleteAsync(externalFileId!, CancellationToken.None); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete orphaned Drive file {ExternalFileId} after visit document link failure.",
                    externalFileId);
            }
        }
    }
}
