using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.VisitPhotos.Queries.GetMyVisitPhotoFolders;

public sealed class GetMyVisitPhotoFoldersQueryHandler
    : IRequestHandler<GetMyVisitPhotoFoldersQuery, PagedResult<MyVisitPhotoFolderItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IVisitFormReadService _formReadService;

    public GetMyVisitPhotoFoldersQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _formReadService = formReadService;
    }

    public async Task<PagedResult<MyVisitPhotoFolderItemDto>> Handle(
        GetMyVisitPhotoFoldersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not { } userId)
            throw new ForbiddenException();
        if (_currentUser.RoleCode != RoleCodes.Student)
            throw new ForbiddenException("Chỉ Sinh viên tham gia chuyến thăm mới dùng được chức năng ảnh đoàn khách.");

        var isActiveStudent = await _db.Users
            .AnyAsync(u => u.UserId == userId && u.Status == "ACTIVE" && u.Role.RoleCode == RoleCodes.Student,
                cancellationToken);
        if (!isActiveStudent)
            throw new ForbiddenException("Tài khoản không hợp lệ hoặc đã bị khóa.");

        // Only instances the Student is allowed into: ACCEPTED or ASSIGNED STUDENT participation.
        var rows = await _db.VisitParticipants
            .Where(p => p.UserId == userId
                        && p.ParticipantRole == ParticipantRoles.Student
                        && (p.Status == ParticipantStatuses.Accepted || p.Status == ParticipantStatuses.Assigned))
            .Select(p => new
            {
                p.VisitInstance.VisitInstanceId,
                p.VisitInstance.VisitRequestId,
                p.VisitInstance.CampusId,
                p.VisitInstance.Status,
                p.VisitInstance.PlannedStartAt,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return new PagedResult<MyVisitPhotoFolderItemDto>
            { Items = new(), TotalCount = 0, PageIndex = request.Page, PageSize = request.PageSize };

        var requestIds = rows.Select(r => r.VisitRequestId).Distinct().ToList();
        var campusIds = rows.Select(r => r.CampusId).Distinct().ToList();
        var instanceIds = rows.Select(r => r.VisitInstanceId).ToList();

        var campusNames = await _db.Campuses
            .Where(c => campusIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var folders = await _db.VisitPhotoFolders
            .Where(f => requestIds.Contains(f.VisitRequestId))
            .ToDictionaryAsync(f => f.VisitRequestId, f => f.FolderName, cancellationToken);

        var photoCounts = (await _db.VisitPhotos
                .Where(p => instanceIds.Contains(p.VisitInstanceId) && p.Status == "ACTIVE")
                .GroupBy(p => p.VisitInstanceId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Key, x => x.Count);

        // Delegation names via the central dual-read path: a v2 (per-campus/MIXED) request must show
        // each instance's OWN name, never the request-level compatibility projection.
        var requests = await _db.VisitRequests
            .Where(v => requestIds.Contains(v.VisitRequestId))
            .ToListAsync(cancellationToken);
        var nameByInstance = new Dictionary<ulong, string>();
        foreach (var visit in requests)
        {
            var visitInstanceIds = rows
                .Where(r => r.VisitRequestId == visit.VisitRequestId)
                .Select(r => r.VisitInstanceId)
                .ToList();
            if (visit.FormSchemaVersion >= FormSchemaVersions.PerCampus)
            {
                var content = await _formReadService.ResolveCampusFormContentAsync(
                    visit, visitInstanceIds, cancellationToken);
                foreach (var id in visitInstanceIds)
                    nameByInstance[id] = content[id].DelegationName;
            }
            else
            {
                foreach (var id in visitInstanceIds)
                    nameByInstance[id] = visit.DelegationName;
            }
        }

        var items = rows
            .Select(r => new MyVisitPhotoFolderItemDto
            {
                VisitInstanceId = r.VisitInstanceId,
                DelegationName = nameByInstance.TryGetValue(r.VisitInstanceId, out var dn) ? dn : string.Empty,
                CampusName = campusNames.TryGetValue(r.CampusId, out var cn) ? cn : null,
                FolderName = folders.TryGetValue(r.VisitRequestId, out var fn) ? fn : null,
                ActivePhotoCount = photoCounts.TryGetValue(r.VisitInstanceId, out var pc) ? pc : 0,
                InstanceStatus = r.Status,
                PlannedStartAt = r.PlannedStartAt,
            })
            .ToList();

        // Search on the RESOLVED name (v2-aware) — must run after the dual-read, so in-memory.
        var search = request.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            items = items
                .Where(i => i.DelegationName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (request.FromDate.HasValue)
            items = items.Where(i => i.PlannedStartAt.Date >= request.FromDate.Value.Date).ToList();
        
        if (request.ToDate.HasValue)
            items = items.Where(i => i.PlannedStartAt.Date <= request.ToDate.Value.Date).ToList();

        var isAsc = string.Equals(request.SortDirection, "ASC", StringComparison.OrdinalIgnoreCase);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var total = items.Count;
        var pageItems = isAsc 
            ? items.OrderBy(i => i.PlannedStartAt).ThenBy(i => i.VisitInstanceId).Skip((page - 1) * pageSize).Take(pageSize).ToList()
            : items.OrderByDescending(i => i.PlannedStartAt).ThenByDescending(i => i.VisitInstanceId).Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<MyVisitPhotoFolderItemDto>
        {
            Items = pageItems,
            TotalCount = total,
            PageIndex = page,
            PageSize = pageSize,
        };
    }
}
