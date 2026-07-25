using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Translation;
using PEMS.Domain.Constants;
using PEMS.Shared;
using QuestPDF.Infrastructure;

namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

public sealed class ExportScheduleReportPdfQueryHandler : IRequestHandler<ExportScheduleReportPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitFormReadService _formReadService;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IContentTranslationService _translator;
    private readonly ILogger<ExportScheduleReportPdfQueryHandler> _logger;

    public ExportScheduleReportPdfQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IVisitFormReadService formReadService,
        IFileStorageService storage,
        IGoogleDriveStorageService drive,
        IContentTranslationService translator,
        ILogger<ExportScheduleReportPdfQueryHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
        _storage = storage;
        _drive = drive;
        _translator = translator;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> Handle(ExportScheduleReportPdfQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        // The request's full guest roster is deliberately NOT loaded: the report's guest side comes from
        // this campus's own visit_instance_guest_members links, and having the request-level list in
        // memory only invites reading it.
        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest).ThenInclude(v => v.Partner)
            .Include(c => c.Agendas)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId
                                      && c.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var visit = instance.VisitRequest;

        // Same scope rule as GetVisitProcessDetailQuery/GetVisitProcessPermissionsQuery — this
        // report is downloaded from the same VisitProcess screen and must never leak beyond it.
        var acceptedParticipantRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                        && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        bool isHost = instance.CurrentHostUserId == userId;
        bool isStaffLeaderOfCampus = roleCode == RoleCodes.Staff
            && string.Equals(subRole, UserSubRoles.Leader, StringComparison.OrdinalIgnoreCase)
            && _currentUser.PrimaryCampusId == instance.CampusId;
        bool isHo = roleCode == RoleCodes.Ho;
        bool isVisitorOwner = roleCode == RoleCodes.Visitor && visit.VisitorUserId == userId;
        bool isAcceptedParticipant = acceptedParticipantRole != null;

        bool inScope = isHost || isStaffLeaderOfCampus || isHo || isVisitorOwner || isAcceptedParticipant;
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền tải báo cáo lịch trình của chuyến thăm này.");

        if (instance.CurrentHostUserId is null)
            throw new ValidationException("Chưa có Host được phân công cho chuyến thăm này — không thể xuất báo cáo lịch trình.");

        var dto = await ScheduleReportDataBuilder.BuildAsync(
            _db, _formReadService, instance, cancellationToken, request.LanguageCode);

        bool isEnglish = string.Equals(request.LanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        if (isEnglish)
            await TranslateToEnglishAsync(dto, instance.VisitInstanceId, cancellationToken);

        byte[]? partnerLogoBytes = dto.PartnerLogoFileId.HasValue
            ? await TryLoadFileBytesAsync(dto.PartnerLogoFileId.Value, cancellationToken)
            : null;

        return ScheduleReportPdfRenderer.Render(dto, ScheduleReportAssets.FptLogoBytes, partnerLogoBytes, request.LanguageCode);
    }

    /// <summary>
    /// Best-effort VI → EN machine translation of the report's free-text content (delegation name,
    /// purpose, organizations, agenda) — batched into one provider call. Role labels are already set
    /// in English by <see cref="ScheduleReportDataBuilder"/> and people's names are never translated.
    /// A provider hiccup (missing config, HTTP error, quota) must never block the export — the report
    /// is simply rendered with its original Vietnamese content instead (same rule as News auto-translate).
    /// </summary>
    private async Task TranslateToEnglishAsync(ScheduleReportDto dto, ulong visitInstanceId, CancellationToken cancellationToken)
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

    private async Task<byte[]?> TryLoadFileBytesAsync(ulong fileId, CancellationToken cancellationToken)
    {
        var file = await _db.Files.FirstOrDefaultAsync(f => f.FileId == fileId, cancellationToken);
        if (file is null) return null;

        var isGoogleDrive = string.Equals(file.StorageProvider, "GOOGLE_DRIVE", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(file.ExternalFileId);

        await using var stream = (isGoogleDrive
            ? await _drive.DownloadAsync(file.ExternalFileId!, cancellationToken)
            : await _storage.OpenReadAsync(file, cancellationToken));
        if (stream is null) return null;

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }
}
