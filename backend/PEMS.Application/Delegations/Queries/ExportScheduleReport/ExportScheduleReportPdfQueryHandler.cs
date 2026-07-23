using Microsoft.EntityFrameworkCore;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
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

    public ExportScheduleReportPdfQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IVisitFormReadService formReadService,
        IFileStorageService storage,
        IGoogleDriveStorageService drive)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
        _storage = storage;
        _drive = drive;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> Handle(ExportScheduleReportPdfQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest).ThenInclude(v => v.GuestMembers)
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

        var dto = await ScheduleReportDataBuilder.BuildAsync(_db, _formReadService, instance, cancellationToken);

        byte[]? partnerLogoBytes = dto.PartnerLogoFileId.HasValue
            ? await TryLoadFileBytesAsync(dto.PartnerLogoFileId.Value, cancellationToken)
            : null;

        return ScheduleReportPdfRenderer.Render(dto, ScheduleReportAssets.FptLogoBytes, partnerLogoBytes);
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
