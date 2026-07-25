using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Queries.GetVisitProcessDetail;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Shared;

using PEMS.Application.Common;
namespace PEMS.Application.Delegations.Queries.GetVisitInstanceContribution;

public sealed class GetVisitInstanceContributionQueryHandler
    : IRequestHandler<GetVisitInstanceContributionQuery, ContributionPageDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitFormReadService _formReadService;

    public GetVisitInstanceContributionQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
    }

    public async Task<ContributionPageDto> Handle(
        GetVisitInstanceContributionQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var departmentId = _currentUser.DepartmentId;

        // Admin & Visitor never take part in the internal contribution flow (spec §6.1/§6.2, §10.3 deny list).
        if (roleCode == RoleCodes.Admin || roleCode == RoleCodes.Visitor)
            throw new ForbiddenException("Bạn không có quyền truy cập trang đóng góp kết quả chuyến thăm.");

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest).ThenInclude(v => v.CampusInstances)
            .Include(c => c.VisitRequest).ThenInclude(v => v.GuestMembers)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var visit = instance.VisitRequest;

        // ── Contribution access gate (spec §5.3 / §10.3) ──
        //   Host  OR  ACCEPTED/ASSIGNED participant  OR  Department with a real logistics relation.
        bool isHost = instance.CurrentHostUserId == userId;

        var participant = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId
                        && p.UserId == userId
                        && !p.IsHost
                        && (p.Status == ParticipantStatuses.Accepted || p.Status == ParticipantStatuses.Assigned))
            .Select(p => new { p.ParticipantRole, p.Status })
            .FirstOrDefaultAsync(cancellationToken);
        bool isAcceptedParticipant = participant != null;

        bool isDepartmentRelated = false;
        if (!isHost && !isAcceptedParticipant && roleCode == RoleCodes.Department)
        {
            isDepartmentRelated = await _db.VisitLogisticsItems.AnyAsync(
                l => l.VisitInstanceId == instance.VisitInstanceId
                     && ((departmentId != null && l.RequestedToDepartmentId == departmentId)
                         || l.AssignedToUserId == userId),
                cancellationToken);
        }

        bool isHo = roleCode == RoleCodes.Ho;
        if (!(isHost || isAcceptedParticipant || isDepartmentRelated || isHo))
            throw new ForbiddenException("Bạn không có quyền truy cập trang đóng góp kết quả chuyến thăm.");

        // Relation label (display/telemetry only — never an auth input).
        string relation = isHost ? "HOST"
            : isHo ? "HO"
            : participant?.ParticipantRole switch
            {
                ParticipantRoles.IcSupport => "IC_SUPPORT",
                ParticipantRoles.DeptSupport => "DEPARTMENT_RELATED",
                ParticipantRoles.Student => "STUDENT_RELATED",
                _ => "DEPARTMENT_RELATED", // department-via-logistics relation
            };

        bool isClosed = instance.Status == VisitInstanceStatus.Closed;
        bool isCancelled = visit.Status == VisitRequestStatuses.Cancelled
                           || instance.Status == VisitInstanceStatus.Cancelled;
        bool isLive = !isClosed && !isCancelled;

        bool isIcSupport = relation == "IC_SUPPORT";
        bool isStudent = relation == "STUDENT_RELATED";
        bool isDepartment = relation == "DEPARTMENT_RELATED";

        // Minutes: Host or any accepted participant may edit while live (mirrors the process rule).
        bool minutesEditor = (isHost || isAcceptedParticipant) && isLive;
        // Media upload opens in AFTER_VISIT for Host/accepted participant (spec §7.2 B2).
        bool mediaUploader = (isHost || isAcceptedParticipant) && isLive
                             && instance.Status == VisitInstanceStatus.AfterVisit;
        // News: Host, IC support or Student — never a pure Department participant (spec §6.8).
        // Writing window opens at AFTER_VISIT (news is a close condition) and stays open after CLOSED.
        bool newsWritingWindow = !isCancelled
            && (instance.Status == VisitInstanceStatus.AfterVisit || isClosed);
        bool newsCreator = (isHost || isIcSupport || isStudent) && newsWritingWindow;

        var permissions = new ContributionPermissionDto
        {
            CanViewContributionPage = true,
            Relation = relation,
            ParticipantRole = participant?.ParticipantRole,
            ParticipantStatus = participant?.Status,

            CanViewRequestSummary = true,
            CanViewAgendaSummary = true,
            CanViewParticipantSummary = true,

            CanViewLogisticsSummary = true,
            CanViewFullLogisticsSummary = isHost || isIcSupport,
            CanViewRelatedLogisticsOnly = isDepartment || isStudent,

            CanViewMinutes = true,
            // HO: read-only — không sửa biên bản.
            CanEditMinutes = !isHo && minutesEditor,

            CanViewMedia = true,
            // HO: read-only — không upload media.
            CanUploadMedia = !isHo && mediaUploader,

            CanViewNews = true,
            // HO: read-only — không tạo/sửa tin tức.
            CanCreateNews = !isHo && newsCreator,
            CanEditNews = !isHo && newsCreator,

            IsReadOnly = !isLive,
        };

        // ── Summary (read-only). Same projections as the host process-detail screen. ──
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        string? hostName = instance.CurrentHostUserId.HasValue
            ? await _db.Users.Where(u => u.UserId == instance.CurrentHostUserId.Value)
                .Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;

        // Agenda (+ responsible-user name enrichment in-memory — Pomelo correlated-subquery pitfall).
        var agenda = await _db.VisitAgendas
            .Where(a => a.VisitInstanceId == instance.VisitInstanceId)
            .OrderBy(a => a.SequenceOrder).ThenBy(a => a.StartTime)
            .Select(a => new AgendaItemDto
            {
                AgendaId = a.AgendaId,
                Title = a.Title,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Description = a.Description,
                Location = a.Location,
                SourceTemplateItemId = a.SourceTemplateItemId,
                ResponsibleUserId = a.ResponsibleUserId,
            })
            .ToListAsync(cancellationToken);

        var responsibleUserIds = agenda
            .Where(a => a.ResponsibleUserId.HasValue)
            .Select(a => a.ResponsibleUserId!.Value).Distinct().ToList();
        if (responsibleUserIds.Count > 0)
        {
            var userById = (await _db.Users
                    .Where(u => responsibleUserIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.FullName, u.Email })
                    .ToListAsync(cancellationToken))
                .ToDictionary(u => u.UserId);
            foreach (var a in agenda)
            {
                if (a.ResponsibleUserId.HasValue && userById.TryGetValue(a.ResponsibleUserId.Value, out var u))
                {
                    a.ResponsibleUserName = u.FullName;
                    a.ResponsibleUserEmail = u.Email;
                }
            }
        }

        // ── INSTANCE-LEVEL form content (keyed by visit_instance_id → a MIXED request still returns 200).
        // Pure V2: source ONLY the TARGET instance's detail + its own member links, never a sibling
        // campus. A missing detail throws inside the read service instead of degrading silently. ──
        var content = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, cancellationToken);
        var d = content[instance.VisitInstanceId];

        string delegationName = d.DelegationName;
        string? visitType = d.VisitType, visitTypeOther = d.VisitTypeOther;
        string? purpose = d.Purpose, workingContent = d.WorkingContent;
        string? workingLanguage = d.WorkingLanguage;
        string? mediaConsentStatus = d.MediaConsentStatus, mediaConsentNote = d.MediaConsentNote;
        string? transportationNote = d.TransportationNote, noteToFptu = d.NoteToFptu;
        var guestMembers = d.Visitors.Select(MapRow).ToList();
        var externalSupportMembers = d.SupportMembers.Select(MapRow).ToList();

        // Request summary (read-only mirror of the guest's original form).
        var requestCampusIds = visit.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        var campusNamesById = requestCampusIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Campuses
                .Where(c => requestCampusIds.Contains(c.CampusId))
                .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var requestSummary = new VisitProcessRequestSummaryDto
        {
            RegistrantName = visit.RegistrantFullName,
            RegistrantEmail = visit.RegistrantEmail,
            RegistrantPhone = visit.RegistrantPhone,
            RegistrantOrganization = visit.RegistrantOrganization,
            RegistrantJobTitle = visit.RegistrantJobTitle,
            RegistrantNationality = visit.RegistrantNationality,

            DelegationName = delegationName,
            VisitScope = visit.VisitScope,
            VisitType = visitType,
            VisitTypeOther = visitTypeOther,
            Purpose = purpose,
            WorkingContent = workingContent,
            WorkingLanguage = workingLanguage,
            MediaConsentStatus = mediaConsentStatus,
            MediaConsentNote = mediaConsentNote,
            TransportationNote = transportationNote,
            NoteToFptu = noteToFptu,

            ContactPersonFullName = visit.ContactPersonFullName,
            ContactPersonOrganization = visit.ContactPersonOrganization,
            ContactPersonPhone = visit.ContactPersonPhone,
            ContactPersonEmail = visit.ContactPersonEmail,

            Campuses = visit.CampusInstances
                .OrderBy(c => c.PlannedStartAt)
                .Select(c => new VisitProcessCampusDto
                {
                    VisitInstanceId = c.VisitInstanceId,
                    CampusId = c.CampusId,
                    CampusName = campusNamesById.TryGetValue(c.CampusId, out var cn) ? cn : string.Empty,
                    PlannedStartAt = c.PlannedStartAt,
                    PlannedEndAt = c.PlannedEndAt,
                    IsCurrent = c.VisitInstanceId == instance.VisitInstanceId,
                })
                .ToList(),

            GuestMembers = guestMembers,
            ExternalSupportMembers = externalSupportMembers,
        };

        // Participants (internal coordination list) — reuse the shared builder.
        var participants = await VisitParticipantListBuilder.BuildAsync(_db, instance.VisitInstanceId, cancellationToken);

        // Logistics summary — scoped: full for Host/IC, related-only for Department/Student (spec §7.2 A4).
        var logisticsQuery = _db.VisitLogisticsItems.Where(l => l.VisitInstanceId == instance.VisitInstanceId);
        if (!permissions.CanViewFullLogisticsSummary)
        {
            logisticsQuery = logisticsQuery.Where(l =>
                (departmentId != null && l.RequestedToDepartmentId == departmentId)
                || l.AssignedToUserId == userId);
        }

        var logistics = await logisticsQuery
            .OrderByDescending(l => l.LogisticsItemId)
            .Select(l => new ContributionLogisticsItemDto
            {
                LogisticsItemId = l.LogisticsItemId,
                ItemType = l.ItemType,
                Title = l.Title,
                Status = l.Status,
                Priority = l.Priority,
                RequestedToDepartmentId = l.RequestedToDepartmentId,
                AssignedToUserId = l.AssignedToUserId,
            })
            .ToListAsync(cancellationToken);

        // Enrich department + assignee names in-memory (avoid correlated subqueries — Pomelo pitfall).
        if (logistics.Count > 0)
        {
            var logiDeptIds = logistics.Where(i => i.RequestedToDepartmentId.HasValue)
                .Select(i => i.RequestedToDepartmentId!.Value).Distinct().ToList();
            var logiDeptNames = logiDeptIds.Count == 0
                ? new Dictionary<ulong, string>()
                : await _db.Departments.Where(d => logiDeptIds.Contains(d.DepartmentId))
                    .ToDictionaryAsync(d => d.DepartmentId, d => d.Name, cancellationToken);

            var logiUserIds = logistics.Where(i => i.AssignedToUserId.HasValue)
                .Select(i => i.AssignedToUserId!.Value).Distinct().ToList();
            var logiUserNames = logiUserIds.Count == 0
                ? new Dictionary<ulong, string>()
                : await _db.Users.Where(u => logiUserIds.Contains(u.UserId))
                    .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

            foreach (var i in logistics)
            {
                if (i.RequestedToDepartmentId.HasValue && logiDeptNames.TryGetValue(i.RequestedToDepartmentId.Value, out var dn))
                    i.DepartmentName = dn;
                if (i.AssignedToUserId.HasValue && logiUserNames.TryGetValue(i.AssignedToUserId.Value, out var un))
                    i.AssignedToName = un;
            }
        }

        var summary = new VisitContributionSummaryDto
        {
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = delegationName,
            RequestStatus = visit.Status,
            InstanceStatus = instance.Status,
            PlannedStartAt = instance.PlannedStartAt,
            PlannedEndAt = instance.PlannedEndAt,
            CampusName = campusName,
            HostUserId = instance.CurrentHostUserId,
            HostName = hostName,
            GuestCount = guestMembers.Count,
            Request = requestSummary,
            Agenda = agenda,
            Participants = participants,
            Logistics = logistics,
        };

        var minutes = await _db.Minutes.FirstOrDefaultAsync(m => m.VisitInstanceId == instance.VisitInstanceId, cancellationToken);
        var mediaDocs = await _db.Documents
            .Include(d => d.File)
            .Where(d => d.OwnerType == "VISIT_INSTANCE_MEDIA" && d.OwnerId == instance.VisitInstanceId)
            .ToListAsync(cancellationToken);
        var news = await _db.News
            .Include(n => n.Translations)
            .FirstOrDefaultAsync(n => n.VisitInstanceId == instance.VisitInstanceId, cancellationToken);

        var userIdsToFetch = new HashSet<ulong>();
        if (minutes?.EditLockedBy != null) userIdsToFetch.Add(minutes.EditLockedBy.Value);
        if (news?.CreatedBy != null) userIdsToFetch.Add(news.CreatedBy.Value);
        foreach (var doc in mediaDocs)
        {
            if (doc.CreatedBy != null) userIdsToFetch.Add(doc.CreatedBy.Value);
        }

        var usersDict = await _db.Users
            .Where(u => userIdsToFetch.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var workspace = new ContributionWorkspaceStatusDto
        {
            Minutes = new MinutesContributionDto
            {
                HasMinutes = minutes != null,
                Status = minutes?.Status ?? "NOT_STARTED",
                Content = minutes?.Content,
                LockedByUserId = minutes?.EditLockedBy,
                LockedByName = minutes?.EditLockedBy != null && usersDict.TryGetValue(minutes.EditLockedBy.Value, out var mName) ? mName : null,
                LockedUntil = minutes?.EditLockExpiresAt,
                UpdatedAt = minutes?.UpdatedAt ?? minutes?.CreatedAt,
                CanCurrentUserTakeLock = permissions.CanEditMinutes && (minutes?.EditLockedBy == null || minutes.EditLockedBy == userId || minutes.EditLockExpiresAt < VietnamTime.Now()),
                CanCurrentUserEdit = permissions.CanEditMinutes
            },
            Media = new MediaContributionDto
            {
                Items = mediaDocs.Select(d => new ContributionMediaItemDto
                {
                    MediaId = d.DocumentId,
                    FileName = d.File.OriginalFilename,
                    FileType = d.File.MimeType ?? "application/octet-stream",
                    Url = d.File.WebViewUrl ?? d.File.DownloadUrl ?? "",
                    ThumbnailUrl = d.File.ThumbnailUrl,
                    UploadedByUserId = d.CreatedBy ?? 0,
                    UploadedByName = d.CreatedBy != null && usersDict.TryGetValue(d.CreatedBy.Value, out var dName) ? dName : "Unknown",
                    UploadedAt = d.CreatedAt,
                    Description = d.Description,
                    IsPrimary = d.Status == "PRIMARY"
                }).ToList(),
                RequiredMinimumCount = 1,
                UploadedCount = mediaDocs.Count,
                IsRequirementSatisfied = mediaDocs.Count >= 1,
                CanCurrentUserUpload = permissions.CanUploadMedia
            },
            News = new NewsContributionDto
            {
                HasNews = news != null,
                NewsId = news?.NewsId,
                Status = news?.Status ?? "NOT_STARTED",
                Title = news?.Translations.FirstOrDefault()?.Title ?? "Bài tin tức",
                Description = news?.Translations.FirstOrDefault()?.Summary,
                CreatedByName = news?.CreatedBy != null && usersDict.TryGetValue(news.CreatedBy.Value, out var nName) ? nName : null,
                UpdatedAt = news?.UpdatedAt ?? news?.CreatedAt,
                // HO: không trả rejectionReason nội bộ.
                RejectionReason = isHo ? null : news?.ReviewNote,
                NewsNotRequired = instance.NewsNotRequired,
                // Per-campus consent: THIS instance's detail (already resolved above), not request-wide.
                MediaConsentAllowed = mediaConsentStatus == PEMS.Shared.MediaConsentStatus.Agreed,
                CanCurrentUserCreate = permissions.CanCreateNews,
                CanCurrentUserEdit = permissions.CanEditNews
            }
        };

        return new ContributionPageDto
        {
            Permissions = permissions,
            Summary = summary,
            Workspace = workspace,
        };
    }

    // Maps a v2 per-campus member row (resolved via IVisitFormReadService for the TARGET instance).
    private static VisitProcessGuestMemberDto MapRow(VisitFormMemberRow r) => new()
    {
        GuestMemberId = (ulong)r.GuestMemberId,
        MemberType = r.MemberType,
        FullName = r.FullName,
        Organization = r.Organization,
        JobTitle = r.JobTitle,
        Nationality = r.Nationality,
        DisplayOrder = r.DisplayOrder,
    };
}
