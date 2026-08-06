using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Queries.GetVisitProcessDetail;

public sealed class GetVisitProcessDetailQueryHandler
    : IRequestHandler<GetVisitProcessDetailQuery, VisitProcessDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitFormReadService _formReadService;

    public GetVisitProcessDetailQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
    }

    public async Task<VisitProcessDetailDto> Handle(
        GetVisitProcessDetailQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest).ThenInclude(v => v.CampusInstances)
            .Include(c => c.VisitRequest).ThenInclude(v => v.GuestMembers)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId
                                      && c.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var visit = instance.VisitRequest;

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
            throw new ForbiddenException("Bạn không có quyền truy cập trang thông tin này.");

        var relation = isHost ? "HOST"
            : isStaffLeaderOfCampus ? "STAFF_LEADER"
            : isHo ? "HO"
            : isGuestSide ? VisitInstanceAccess.OperationalContact
            : acceptedParticipantRole switch
            {
                ParticipantRoles.IcSupport => "IC_SUPPORT",
                ParticipantRoles.DeptSupport => "DEPT_SUPPORT",
                ParticipantRoles.Student => "STUDENT",
                _ => "NONE",
            };

        bool isLive = instance.Status != VisitInstanceStatus.Closed
            && instance.Status != VisitInstanceStatus.Cancelled;
        // BEFORE_VISIT only. At ASSIGNED the Host owns the campus but has not started preparing, so
        // the tab renders read-only until they do (see StartVisitPreparationCommand).
        bool canEditBefore = isHost && isLive
            && instance.Status == VisitInstanceStatus.BeforeVisit;

        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        // ── Host (read-only) ── current_host_user_id is the single source of truth; this screen never
        // changes the host. Enrich with email/phone/department for the read-only host card.
        string? hostName = null;
        VisitProcessHostDto? hostDto = null;
        if (instance.CurrentHostUserId.HasValue)
        {
            var host = await (
                from u in _db.Users
                where u.UserId == instance.CurrentHostUserId.Value
                select new { u.UserId, u.FullName, u.Email, u.Phone, u.DepartmentId })
                .FirstOrDefaultAsync(cancellationToken);
            if (host != null)
            {
                hostName = host.FullName;
                string? hostDeptName = host.DepartmentId.HasValue
                    ? await _db.Departments.Where(d => d.DepartmentId == host.DepartmentId.Value)
                        .Select(d => d.Name).FirstOrDefaultAsync(cancellationToken)
                    : null;
                hostDto = new VisitProcessHostDto
                {
                    UserId = host.UserId,
                    FullName = host.FullName,
                    Email = host.Email,
                    Phone = host.Phone,
                    DepartmentName = hostDeptName,
                    StatusLabel = "Đã được phân công",
                };
            }
        }

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
                ResponsibleName = a.ResponsibleName,
            })
            .ToListAsync(cancellationToken);

        // Enrich in-memory (avoid correlated subqueries on optional FKs — Pomelo translation pitfall):
        //   source_template_item_id        → agenda_template_items.responsible_role_label (suggested role)
        var sourceTemplateItemIds = agenda
            .Where(a => a.SourceTemplateItemId.HasValue)
            .Select(a => a.SourceTemplateItemId!.Value).Distinct().ToList();
        if (sourceTemplateItemIds.Count > 0)
        {
            var labelByItemId = (await _db.AgendaTemplateItems
                    .Where(ti => sourceTemplateItemIds.Contains(ti.AgendaTemplateItemId))
                    .Select(ti => new { ti.AgendaTemplateItemId, ti.ResponsibleRoleLabel })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.AgendaTemplateItemId, x => x.ResponsibleRoleLabel);
            foreach (var a in agenda)
            {
                if (a.SourceTemplateItemId.HasValue
                    && labelByItemId.TryGetValue(a.SourceTemplateItemId.Value, out var label))
                {
                    a.TemplateResponsibleRoleLabel = label;
                }
            }
        }

        // ── INSTANCE-LEVEL form content (this screen is keyed by visit_instance_id, so a MIXED request
        // still returns 200). Pure V2: source ONLY the TARGET instance's detail + its own member links,
        // never a sibling campus — there is no global snapshot left to fall back to. ──
        var content = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, cancellationToken);
        if (!content.TryGetValue(instance.VisitInstanceId, out var d))
            throw new InvalidOperationException("VISIT_FORM_DETAIL_MISSING");

        string delegationName = d.DelegationName;
        string? visitType = d.VisitType, visitTypeOther = d.VisitTypeOther;
        string? purpose = d.Purpose, workingContent = d.WorkingContent;
        string? workingLanguage = d.WorkingLanguage;
        string? mediaConsentStatus = d.MediaConsentStatus, mediaConsentNote = d.MediaConsentNote;
        string? transportationNote = d.TransportationNote;
        List<VisitProcessGuestMemberDto> guestMembers = d.Visitors.Select(MapRow).ToList();
        List<VisitProcessGuestMemberDto> externalSupportMembers = d.SupportMembers.Select(MapRow).ToList();

        // ── Request summary (read-only mirror of the guest's original form) ──
        // Campus names for every campus instance of the request (multi-campus aware).
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

            OperationalContactFullName = d.OperationalContact.FullName,
            OperationalContactOrganization = d.OperationalContact.Organization,
            OperationalContactPhone = d.OperationalContact.Phone,
            OperationalContactEmail = d.OperationalContact.Email,

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

        // Participants: only for internal relations. The guest/visitor owner must not see the
        // internal coordination list (the FE blocks them from this screen anyway).
        var participants = VisitInstanceAccess.CanViewInternal(relation)
            ? await VisitParticipantListBuilder.BuildAsync(_db, instance.VisitInstanceId, cancellationToken)
            : new List<GetVisitInstanceParticipants.VisitParticipantListItemDto>();

        var notifications = new List<VisitorNotificationDto>();
        var publicNews = new List<VisitorPublicNewsListItemDto>();
        
        if (relation == "VISITOR_OWNER")
        {
            var reqId = instance.VisitRequestId;
            var instId = instance.VisitInstanceId;
            
            notifications = await _db.Notifications
                .Where(n => n.RecipientUserId == userId)
                .Where(n =>
                    (n.RelatedType == "VISIT_INSTANCE" && n.RelatedId == instId) ||
                    (n.RelatedType == "VISIT_REQUEST" && n.RelatedId == reqId)
                )
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new VisitorNotificationDto
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    NotificationType = n.NotificationType,
                    IsRead = n.IsRead,
                    ReadAt = n.ReadAt,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var newsItems = await _db.News.AsNoTracking()
                .Where(n => n.VisitInstanceId == instId && n.Status == NewsConstants.Status.Published)
                .OrderByDescending(n => n.PublishedAt ?? n.CreatedAt)
                .Select(n => new {
                    n.NewsId,
                    n.AuthorUserId,
                    n.CoverFileId,
                    n.PublishedAt
                })
                .ToListAsync(cancellationToken);

            if (newsItems.Any())
            {
                var newsIds = newsItems.Select(n => n.NewsId).ToList();
                var authorIds = newsItems.Select(n => n.AuthorUserId).Distinct().ToList();
                var coverFileIds = newsItems.Where(n => n.CoverFileId.HasValue).Select(n => n.CoverFileId!.Value).Distinct().ToList();

                var translations = await _db.NewsTranslations.AsNoTracking()
                    .Where(t => newsIds.Contains(t.NewsId))
                    .ToListAsync(cancellationToken);
                    
                var translationDict = translations
                    .GroupBy(t => t.NewsId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.FirstOrDefault(t => t.LanguageCode == "vi") ?? g.First());

                var users = await _db.Users.AsNoTracking()
                    .Where(u => authorIds.Contains(u.UserId))
                    .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);
                    
                var coverThumbs = new Dictionary<ulong, string?>();
                if (coverFileIds.Any()) {
                    var files = await _db.Files.AsNoTracking()
                        .Where(f => coverFileIds.Contains(f.FileId))
                        .Select(f => new { f.FileId, f.ThumbnailUrl, f.WebViewUrl })
                        .ToListAsync(cancellationToken);
                    foreach (var f in files) {
                        coverThumbs[f.FileId] = f.ThumbnailUrl ?? f.WebViewUrl;
                    }
                }

                foreach (var item in newsItems)
                {
                    translationDict.TryGetValue(item.NewsId, out var trans);
                    users.TryGetValue(item.AuthorUserId, out var authorName);
                    string? thumbUrl = item.CoverFileId.HasValue && coverThumbs.TryGetValue(item.CoverFileId.Value, out var tu) ? tu : null;

                    publicNews.Add(new VisitorPublicNewsListItemDto
                    {
                        NewsId = item.NewsId,
                        Title = trans?.Title ?? string.Empty,
                        Summary = trans?.Summary,
                        Slug = trans?.Slug,
                        PublishedAt = item.PublishedAt,
                        AuthorName = authorName,
                        ThumbnailUrl = thumbUrl
                    });
                }
            }
        }

        return new VisitProcessDetailDto
        {
            VisitRequestId = instance.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            DelegationName = delegationName,
            InstanceStatus = instance.Status,
            PlannedStartAt = instance.PlannedStartAt,
            PlannedEndAt = instance.PlannedEndAt,
            CampusName = campusName,
            HostUserId = instance.CurrentHostUserId,
            HostName = hostName,
            Relation = relation,
            CanEditBefore = canEditBefore,
            PreparationNote = instance.PreparationNote,
            Agenda = agenda,
            RequestSummary = requestSummary,
            Host = hostDto,
            Participants = participants,
            Notifications = notifications,
            PublicNews = publicNews,
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
