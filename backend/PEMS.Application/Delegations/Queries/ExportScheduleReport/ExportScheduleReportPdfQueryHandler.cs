using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>
/// Downloads the "Báo cáo Lịch trình" for one campus instance.
///
/// <para>
/// The build/translate/render/archive chain moved to <see cref="IScheduleReportArtifactService"/> when
/// the setup-progress email started needing the same document. What stays here is what is specific to a
/// download: who may ask for one, and the fact that archiving it is best-effort — a Drive or DB hiccup
/// must never cost the user the file they asked for.
/// </para>
/// </summary>
public sealed class ExportScheduleReportPdfQueryHandler : IRequestHandler<ExportScheduleReportPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IScheduleReportArtifactService _reports;
    private readonly ILogger<ExportScheduleReportPdfQueryHandler> _logger;

    public ExportScheduleReportPdfQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IScheduleReportArtifactService reports,
        ILogger<ExportScheduleReportPdfQueryHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _reports = reports;
        _logger = logger;
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
        bool isGuestSide = VisitRequestOwnership.IsGuestSide(visit, instance, userId);
        bool isAcceptedParticipant = acceptedParticipantRole != null;

        bool inScope = isHost || isStaffLeaderOfCampus || isHo || isGuestSide || isAcceptedParticipant;
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền tải báo cáo lịch trình của chuyến thăm này.");

        var artifact = await _reports.RenderAsync(instance, request.LanguageCode, cancellationToken);

        // This report is scoped to ONE delegation instance (unlike the Staff Leader/HO dashboard
        // exports, which aggregate across many) — archive it into that delegation's own
        // Tài liệu/Theo đoàn khách folder instead of the flat "Report" folder. Best-effort: a
        // Drive/DB hiccup must never block the download itself.
        try
        {
            await _reports.StoreAsync(artifact, instance, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to archive schedule report for visit instance {VisitInstanceId}.", instance.VisitInstanceId);
        }

        return artifact.Content;
    }
}
