using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Delegations.VisitPhotos;
using PEMS.Application.Translation;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Documents;
using PEMS.Shared;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>See <see cref="IScheduleReportArtifactService"/>.</summary>
public sealed class ScheduleReportArtifactService : IScheduleReportArtifactService
{
    private readonly IApplicationDbContext _db;
    private readonly IVisitFormReadService _formReadService;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IContentTranslationService _translator;
    private readonly IFileUploadService _fileUpload;
    private readonly IVisitPhotoFolderService _folderService;
    private readonly ILogger<ScheduleReportArtifactService> _logger;

    public ScheduleReportArtifactService(
        IApplicationDbContext db,
        IVisitFormReadService formReadService,
        IFileStorageService storage,
        IGoogleDriveStorageService drive,
        IContentTranslationService translator,
        IFileUploadService fileUpload,
        IVisitPhotoFolderService folderService,
        ILogger<ScheduleReportArtifactService> logger)
    {
        _db = db;
        _formReadService = formReadService;
        _storage = storage;
        _drive = drive;
        _translator = translator;
        _fileUpload = fileUpload;
        _folderService = folderService;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ScheduleReportArtifact> RenderAsync(
        VisitRequestCampus instance, string? languageCode, CancellationToken cancellationToken)
    {
        if (instance.CurrentHostUserId is null)
            throw new ValidationException(
                "Chưa có Host được phân công cho chuyến thăm này — không thể xuất báo cáo lịch trình.");

        var language = string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
        var isEnglish = language == "en";

        var dto = await ScheduleReportDataBuilder.BuildAsync(
            _db, _formReadService, instance, cancellationToken, language);

        if (isEnglish)
            await TranslateToEnglishAsync(dto, instance.VisitInstanceId, cancellationToken);

        byte[]? partnerLogoBytes = dto.PartnerLogoFileId.HasValue
            ? await TryLoadFileBytesAsync(dto.PartnerLogoFileId.Value, cancellationToken)
            : null;

        var pdfBytes = ScheduleReportPdfRenderer.Render(
            dto, ScheduleReportAssets.FptLogoBytes, partnerLogoBytes, language);

        var generatedAt = VietnamTime.Now();
        // The EN suffix matters here in a way it did not for a pure download: both languages can be
        // archived into the same delegation folder, and two files whose names differ only by their
        // minute stamp are indistinguishable to whoever opens the folder later.
        var suffix = isEnglish ? "_EN" : "";
        var fileName =
            $"PEMS_Schedule_Report_{instance.VisitRequest.RequestCode}_{generatedAt:yyyyMMdd_HHmm}{suffix}.pdf";

        // Data rides along so a caller rendering the same facts a second way (the setup-progress
        // email's HTML tables) uses this read rather than issuing its own.
        return new ScheduleReportArtifact(pdfBytes, fileName, language, generatedAt) { Data = dto };
    }

    public async Task<ulong> StoreAsync(
        ScheduleReportArtifact artifact,
        VisitRequestCampus instance,
        ulong actorUserId,
        CancellationToken cancellationToken)
    {
        var campusCode = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.CampusCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(campusCode))
            throw new ValidationException(
                "Không xác định được cơ sở của chuyến thăm — không thể lưu báo cáo lịch trình.");

        var target = await _folderService.EnsureDocumentUploadTargetAsync(
            instance, campusCode, VisitDocumentSubtypes.TheoDoanKhach, actorUserId, cancellationToken);

        // Upload FIRST, record SECOND, and never the other way round. A documents row written before the
        // bytes land would survive a failed upload as a permanent pointer to nothing: every later reader
        // would find a Schedule Report listed for this delegation and fail when opening it, with no way
        // to tell that from a file somebody deleted afterwards. UploadBusinessFileAsync throws on any
        // upload failure (and deletes its own orphan if the files insert fails), so reaching the next
        // line means the bytes are stored and addressable.
        await using var stream = new MemoryStream(artifact.Content);
        var uploaded = await _fileUpload.UploadBusinessFileAsync(
            stream, artifact.FileName, "application/pdf", artifact.Content.LongLength,
            FilePurpose.VisitRequestAttachment, (long)actorUserId, target.DocumentFolderExternalId,
            cancellationToken);

        if (uploaded.FileId <= 0)
            throw new ValidationException(
                "Không lưu được Báo cáo Lịch trình vào kho tệp — vui lòng thử lại.");

        _db.Documents.Add(new Document
        {
            FileId = (ulong)uploaded.FileId,
            OwnerType = "VISIT",
            OwnerId = instance.VisitRequestId,
            CampusId = instance.CampusId,
            Title = artifact.FileName,
            DocumentCategory = "SCHEDULE_REPORT",
            Status = "PUBLISHED",
            CreatedAt = VietnamTime.Now(),
            CreatedBy = actorUserId,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return (ulong)uploaded.FileId;
    }

    /// <summary>
    /// Best-effort VI → EN machine translation of the report's free-text content (delegation name,
    /// purpose, organizations, agenda) — batched into one provider call. Role labels are already set
    /// in English by <see cref="ScheduleReportDataBuilder"/> and people's names are never translated.
    /// A provider hiccup (missing config, HTTP error, quota) must never block the export — the report
    /// is simply rendered with its original Vietnamese content instead (same rule as News auto-translate).
    /// </summary>
    private async Task TranslateToEnglishAsync(
        ScheduleReportDto dto, ulong visitInstanceId, CancellationToken cancellationToken)
    {
        var texts = new List<string>();
        var setters = new List<Action<string>>();

        void Track(string? value, Action<string> setter)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            texts.Add(value);
            setters.Add(setter);
        }

        Track(dto.DelegationName, v => dto.DelegationName = v);
        Track(dto.Purpose, v => dto.Purpose = v);
        foreach (var p in dto.GuestSide) Track(p.Organization, v => p.Organization = v);
        foreach (var p in dto.FptSide) Track(p.Organization, v => p.Organization = v);
        foreach (var a in dto.Agenda)
        {
            Track(a.Title, v => a.Title = v);
            Track(a.Description, v => a.Description = v);
            Track(a.Venue, v => a.Venue = v);
        }

        if (texts.Count == 0) return;

        IReadOnlyList<string>? translated;
        try
        {
            translated = await _translator.TranslateTextAsync(texts, "vi", "en", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Schedule report translation failed for visit instance {VisitInstanceId}; falling back to Vietnamese content.",
                visitInstanceId);
            return;
        }

        if (translated.Count != texts.Count)
        {
            _logger.LogWarning(
                "Schedule report translation returned {ResultCount} results for {SourceCount} sources; falling back to Vietnamese content.",
                translated.Count, texts.Count);
            return;
        }

        for (var i = 0; i < setters.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(translated[i])) setters[i](translated[i]);
        }
    }

    /// <summary>
    /// The partner logo, or null when it cannot be fetched.
    ///
    /// <para>
    /// It really does try, which is the whole point of the name. This method used to let a storage
    /// failure escape, and the consequence was out of all proportion to the cause: a partner whose
    /// <c>logo_file_id</c> pointed at a Drive file that had been deleted — or, on any database seeded
    /// with placeholder ids, at a file that never existed — made the entire Schedule Report unbuildable.
    /// The Host saw "Không tìm thấy tệp đính kèm trên Google Drive" and had no way to connect that to a
    /// decorative image, because the message named an attachment and the failing file was a logo.
    /// </para>
    /// <para>
    /// A missing logo is not a reason to withhold the report:
    /// <see cref="ScheduleReportPdfRenderer"/> already draws a centred FPT logo when there is no partner
    /// one, which is exactly the layout used for a delegation with no partner at all. Cancellation still
    /// propagates — a cancelled request is not a missing file.
    /// </para>
    /// </summary>
    private async Task<byte[]?> TryLoadFileBytesAsync(ulong fileId, CancellationToken cancellationToken)
    {
        var file = await _db.Files.FirstOrDefaultAsync(f => f.FileId == fileId, cancellationToken);
        if (file is null)
        {
            _logger.LogWarning(
                "Partner logo file {FileId} is referenced but has no files row; rendering without it.", fileId);
            return null;
        }

        var isGoogleDrive = string.Equals(file.StorageProvider, "GOOGLE_DRIVE", StringComparison.OrdinalIgnoreCase);
        if (isGoogleDrive && string.IsNullOrWhiteSpace(file.ExternalFileId))
        {
            _logger.LogWarning(
                "Partner logo file {FileId} is stored as GOOGLE_DRIVE but carries no external id ({Code}); rendering without it.",
                fileId, StorageErrorCodes.FileReferenceInvalid);
            return null;
        }

        try
        {
            await using var stream = (isGoogleDrive
                ? await _drive.DownloadAsync(file.ExternalFileId!, cancellationToken)
                : await _storage.OpenReadAsync(file, cancellationToken));
            if (stream is null)
            {
                _logger.LogWarning(
                    "Partner logo file {FileId} could not be opened from {Provider}; rendering without it.",
                    fileId, file.StorageProvider);
                return null;
            }

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            return ms.ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BusinessRuleException ex)
        {
            // The code is logged rather than swallowed silently: which of "gone" / "refused" / "network"
            // it was decides who fixes the partner record, and nothing else in this flow will report it.
            _logger.LogWarning(ex,
                "Partner logo file {FileId} is unreadable ({Code}); rendering the report without it.",
                fileId, ex.ErrorCode ?? StorageErrorCodes.Unavailable);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Partner logo file {FileId} could not be fetched; rendering the report without it.", fileId);
            return null;
        }
    }
}
