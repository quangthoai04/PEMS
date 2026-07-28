using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Utils;
using PEMS.Application.Delegations.VisitPhotos;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.Commands.SaveVisitLogisticsHandoverDocument;

public sealed class SaveVisitLogisticsHandoverDocumentCommandHandler
    : IRequestHandler<SaveVisitLogisticsHandoverDocumentCommand, SaveVisitLogisticsHandoverDocumentResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IVisitPhotoFolderService _folderService;
    private readonly IDateTimeService _clock;

    public SaveVisitLogisticsHandoverDocumentCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IVisitPhotoFolderService folderService,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _folderService = folderService;
        _clock = clock;
    }

    public async Task<SaveVisitLogisticsHandoverDocumentResponse> Handle(
        SaveVisitLogisticsHandoverDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } actorId)
            throw new ForbiddenException();

        var handoverType = request.HandoverType?.Trim().ToUpperInvariant();
        if (!LogisticsHandoverTypes.All.Contains(handoverType))
            throw new BusinessRuleException("Loại biên bản không hợp lệ.");

        var item = await _db.VisitLogisticsItems
            .Include(i => i.Handovers)
            .Include(i => i.VisitInstance).ThenInclude(vc => vc.VisitRequest)
            .FirstOrDefaultAsync(i => i.LogisticsItemId == request.LogisticsItemId, cancellationToken)
            ?? throw new NotFoundException("VisitLogisticsItem", request.LogisticsItemId);

        var instance = item.VisitInstance;

        // Same actors who can already view/sign this handover in TaskHandoverModal: the campus
        // instance's Host, or the department staff this hạng mục was assigned to. Admin as fallback.
        var isHost = instance.CurrentHostUserId == actorId;
        var isAssignedStaff = item.AssignedToUserId == actorId;
        var isAdmin = _currentUser.RoleCode == RoleCodes.Admin;
        if (!isHost && !isAssignedStaff && !isAdmin)
            throw new ForbiddenException("Bạn không có quyền lưu biên bản này vào hệ thống.");

        var handover = item.Handovers.FirstOrDefault(h => h.HandoverType == handoverType)
            ?? throw new BusinessRuleException(
                "Biên bản chưa được ký — chưa thể lưu vào hệ thống.", "HANDOVER_NOT_SIGNED");
        if (handover.BorrowerSignedAt is null || handover.ProviderSignedAt is null)
            throw new BusinessRuleException(
                "Biên bản cần đủ chữ ký của cả hai bên trước khi lưu vào hệ thống.", "HANDOVER_NOT_FULLY_SIGNED");

        var campusCode = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.CampusCode)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Campus", instance.CampusId);
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? campusCode;
        var delegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { instance.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(instance.VisitInstanceId) ?? instance.VisitRequest.RequestCode;

        var signerIds = new[] { handover.BorrowerSignedBy, handover.ProviderSignedBy }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var signerNames = await _db.Users
            .Where(u => signerIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName })
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var checklistRowsJson = handoverType == LogisticsHandoverTypes.Borrow
            ? VehicleHandoverChecklistNote.ExtractRowsJson(handover.ConditionNote)
            : null;
        var note = VehicleHandoverChecklistNote.ExtractNote(handover.ConditionNote);

        var pdfBytes = LogisticsHandoverPdfRenderer.Build(new LogisticsHandoverPdfRenderer.Input(
            LogisticsItemId: item.LogisticsItemId,
            HandoverType: handoverType!,
            ItemTitle: item.Title,
            ItemDescription: item.Description,
            Quantity: item.Quantity ?? 1,
            DelegationName: delegationName ?? "N/A",
            CampusName: campusName,
            ProviderSignature: new LogisticsHandoverPdfRenderer.SignatureInfo(
                handover.ProviderSignedBy.HasValue ? signerNames.GetValueOrDefault(handover.ProviderSignedBy.Value) : null,
                handover.ProviderSignedAt),
            BorrowerSignature: new LogisticsHandoverPdfRenderer.SignatureInfo(
                handover.BorrowerSignedBy.HasValue ? signerNames.GetValueOrDefault(handover.BorrowerSignedBy.Value) : null,
                handover.BorrowerSignedAt),
            ItemCondition: handover.ItemCondition,
            Note: note,
            Checklist: LogisticsHandoverPdfRenderer.ParseChecklist(checklistRowsJson)));

        var target = await _folderService.EnsureDocumentUploadTargetAsync(
            instance, campusCode, VisitDocumentSubtypes.HauCan, actorId, cancellationToken);

        await using var stream = new MemoryStream(pdfBytes);
        var fileName = $"bien-ban-{(handoverType == LogisticsHandoverTypes.Return ? "nghiem-thu" : "ban-giao")}-{item.LogisticsItemId}.pdf";
        var uploaded = await _fileUpload.UploadBusinessFileAsync(
            stream, fileName, "application/pdf", pdfBytes.LongLength,
            FilePurpose.LogisticsAttachment, (long)actorId,
            target.DocumentFolderExternalId, cancellationToken);

        var now = _clock.VietnamNow;
        handover.AttachmentFileId = (ulong)uploaded.FileId;

        // Re-saving the SAME handover (e.g. after a note edit) reuses its one document row instead
        // of piling up duplicates — OwnerId=HandoverId is already a natural 1:1 key (at most one
        // BORROW and one RETURN row per logistics item).
        var document = await _db.Documents
            .FirstOrDefaultAsync(d => d.OwnerType == "LOGISTICS" && d.OwnerId == handover.HandoverId, cancellationToken);
        var title = $"Biên bản {(handoverType == LogisticsHandoverTypes.Return ? "nghiệm thu" : "bàn giao")} - {item.Title}";
        if (document is null)
        {
            document = new Document
            {
                FileId = (ulong)uploaded.FileId,
                OwnerType = "LOGISTICS",
                OwnerId = handover.HandoverId,
                CampusId = instance.CampusId,
                Title = title,
                Status = "PUBLISHED",
                CreatedAt = now,
                CreatedBy = actorId,
            };
            _db.Documents.Add(document);
        }
        else
        {
            document.FileId = (ulong)uploaded.FileId;
            document.Title = title;
            document.UpdatedAt = now;
            document.UpdatedBy = actorId;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = instance.CampusId,
            Action = "SAVE_LOGISTICS_HANDOVER_DOCUMENT",
            EntityType = "VisitLogisticsItemHandover",
            EntityId = handover.HandoverId,
            VisitRequestId = instance.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new SaveVisitLogisticsHandoverDocumentResponse
        {
            DocumentId = document.DocumentId,
            FileId = (ulong)uploaded.FileId,
            WebViewUrl = uploaded.WebViewUrl,
            DownloadUrl = uploaded.DownloadUrl,
        };
    }
}
