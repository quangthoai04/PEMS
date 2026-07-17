using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;
using System;

using PEMS.Application.Common;
namespace PEMS.Application.MeetingMinutes.Queries.SearchAndFilterMinutes;

public sealed class SearchAndFilterMinutesQueryHandler : IRequestHandler<SearchAndFilterMinutesQuery, SearchAndFilterMinutesDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SearchAndFilterMinutesQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SearchAndFilterMinutesDto> Handle(SearchAndFilterMinutesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new ForbiddenException();
        }

        var isHo = _currentUser.RoleCode == "HO";
        // HO manages every campus (no fixed PrimaryCampusId) — everyone else is scoped to
        // exactly one campus and must have it set, otherwise they see nothing meaningful here.
        if (!isHo && _currentUser.PrimaryCampusId is null)
        {
            throw new ForbiddenException();
        }

        var campusId = _currentUser.PrimaryCampusId;

        var joined = _db.Minutes
            .Join(_db.VisitRequestCampuses,
                  m => m.VisitInstanceId,
                  vrc => vrc.VisitInstanceId,
                  (m, vrc) => new { m, vrc });

        // Base query: non-HO roles are always scoped to their own campus (server never trusts
        // the client for this). HO instead may optionally self-filter via request.CampusId.
        if (!isHo)
        {
            joined = joined.Where(x => x.vrc.CampusId == campusId);
        }
        else if (request.CampusId.HasValue)
        {
            joined = joined.Where(x => x.vrc.CampusId == request.CampusId.Value);
        }

        var query = joined.Select(x => x.m).AsQueryable();

        // Summary queries before pagination but after scope filtering
        var totalMinutes = await query.CountAsync(cancellationToken);
        var draftCount = await query.CountAsync(m => m.Status == "DRAFT", cancellationToken);
        var savedCount = await query.CountAsync(m => m.Status == "SAVED", cancellationToken);
        
        var now = VietnamTime.Now();
        var lockedCount = await query.CountAsync(m => m.EditLockedBy != null && m.EditLockExpiresAt != null && m.EditLockExpiresAt > now, cancellationToken);
        
        var openActionItemCountQuery = from m in query
                                       join ai in _db.MinuteActionItems on m.MinutesId equals ai.MinutesId
                                       where ai.Status == "TODO" || ai.Status == "IN_PROGRESS"
                                       select ai;
        var openActionItemCount = await openActionItemCountQuery.CountAsync(cancellationToken);
        
        var latestUpdatedMinute = await query.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var latestUpdatedAt = latestUpdatedMinute?.UpdatedAt ?? latestUpdatedMinute?.CreatedAt;

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim().ToLower();
            query = query.Where(m => 
                m.Title.ToLower().Contains(q) || 
                (m.Content != null && m.Content.ToLower().Contains(q)) ||
                m.MinutesId.ToString() == q ||
                m.VisitInstanceId.ToString() == q
            );
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(m => m.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.LockState))
        {
            if (request.LockState == "LOCKED")
                query = query.Where(m => m.EditLockedBy != null && m.EditLockExpiresAt > now);
            else if (request.LockState == "UNLOCKED")
                query = query.Where(m => m.EditLockedBy == null || m.EditLockExpiresAt == null || m.EditLockExpiresAt <= now);
        }

        if (!string.IsNullOrWhiteSpace(request.DateType))
        {
            DateTime? fromDate = null;
            DateTime? toDate = null;
            if (DateTime.TryParse(request.FromDate, out var fd)) fromDate = fd.Date;
            if (DateTime.TryParse(request.ToDate, out var td)) toDate = td.Date.AddDays(1).AddTicks(-1);

            if (request.DateType == "created_at")
            {
                if (fromDate.HasValue) query = query.Where(m => m.CreatedAt >= fromDate.Value);
                if (toDate.HasValue) query = query.Where(m => m.CreatedAt <= toDate.Value);
            }
            else if (request.DateType == "updated_at")
            {
                if (fromDate.HasValue) query = query.Where(m => m.UpdatedAt >= fromDate.Value);
                if (toDate.HasValue) query = query.Where(m => m.UpdatedAt <= toDate.Value);
            }
        }

        // Apply Sorting
        query = (request.SortBy?.ToLower(), request.SortDir?.ToLower()) switch
        {
            ("created_at", "asc") => query.OrderBy(m => m.CreatedAt),
            ("created_at", "desc") => query.OrderByDescending(m => m.CreatedAt),
            ("updated_at", "asc") => query.OrderBy(m => m.UpdatedAt ?? m.CreatedAt),
            ("updated_at", "desc") => query.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt),
            ("title", "asc") => query.OrderBy(m => m.Title),
            ("title", "desc") => query.OrderByDescending(m => m.Title),
            _ => query.OrderByDescending(m => m.CreatedAt) // Default
        };

        var filteredCount = await query.CountAsync(cancellationToken);

        // Pagination
        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        var page = request.Page > 0 ? request.Page : 1;
        var minutesPage = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        // Map to Dto (loading extra relations)
        var items = new List<MinutesListItemDto>();
        foreach (var minute in minutesPage)
        {
            var vrc = await _db.VisitRequestCampuses
                .Include(v => v.VisitRequest)
                .Include(v => v.FormDetail) // per-campus v2: mixed rows title from THIS instance's detail
                .FirstOrDefaultAsync(v => v.VisitInstanceId == minute.VisitInstanceId, cancellationToken);
                
            var campusName = vrc != null ? await _db.Campuses.Where(c => c.CampusId == vrc.CampusId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken) : null;
                
            var host = await _db.VisitParticipants
                .Where(p => p.VisitInstanceId == minute.VisitInstanceId && p.IsHost)
                .FirstOrDefaultAsync(cancellationToken);
                
            var hostName = host != null ? await _db.Users.Where(u => u.UserId == host.UserId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken) : null;

            var lockedByName = minute.EditLockedBy.HasValue 
                ? await _db.Users.Where(u => u.UserId == minute.EditLockedBy.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
                : null;
            var createdByName = minute.CreatedBy.HasValue
                ? await _db.Users.Where(u => u.UserId == minute.CreatedBy.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
                : null;
            var updatedByName = minute.UpdatedBy.HasValue
                ? await _db.Users.Where(u => u.UserId == minute.UpdatedBy.Value).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
                : null;

            var participants = await _db.MinuteParticipants.Where(p => p.MinutesId == minute.MinutesId).ToListAsync(cancellationToken);
            var actionItems = await _db.MinuteActionItems.Where(ai => ai.MinutesId == minute.MinutesId).ToListAsync(cancellationToken);

            items.Add(new MinutesListItemDto
            {
                MinutesId = minute.MinutesId,
                VisitInstanceId = minute.VisitInstanceId,
                Title = minute.Title,
                ContentPreview = minute.Content?.Length > 100 ? minute.Content.Substring(0, 100) + "..." : minute.Content,
                Status = minute.Status,
                EditLockedBy = minute.EditLockedBy,
                EditLockedByName = lockedByName,
                EditLockedAt = minute.EditLockedAt.HasValue ? DateTime.SpecifyKind(minute.EditLockedAt.Value, DateTimeKind.Utc).ToString("O") : null,
                EditLockExpiresAt = minute.EditLockExpiresAt.HasValue ? DateTime.SpecifyKind(minute.EditLockExpiresAt.Value, DateTimeKind.Utc).ToString("O") : null,
                LockState = (minute.EditLockedBy != null && minute.EditLockExpiresAt > now) ? "LOCKED" : "UNLOCKED",
                RowVersion = minute.RowVersion,
                CreatedAt = DateTime.SpecifyKind(minute.CreatedAt, DateTimeKind.Utc).ToString("O"),
                CreatedByName = createdByName,
                UpdatedAt = minute.UpdatedAt.HasValue ? DateTime.SpecifyKind(minute.UpdatedAt.Value, DateTimeKind.Utc).ToString("O") : null,
                UpdatedByName = updatedByName,
                VisitTitle = vrc?.VisitRequest is { } vvr
                             && vvr.FormSchemaVersion >= FormSchemaVersions.PerCampus && vvr.HasMixedCampusDetails
                    ? vrc.FormDetail?.DelegationName
                    : vrc?.VisitRequest?.DelegationName,
                VisitRequestId = vrc?.VisitRequestId,
                CampusName = campusName,
                HostName = hostName,
                PlannedStartAt = vrc != null ? DateTime.SpecifyKind(vrc.PlannedStartAt, DateTimeKind.Utc).ToString("O") : null,
                PlannedEndAt = vrc != null ? DateTime.SpecifyKind(vrc.PlannedEndAt, DateTimeKind.Utc).ToString("O") : null,
                ParticipantTotal = participants.Count,
                ParticipantPresentCount = participants.Count(p => p.AttendanceStatus == "PRESENT"),
                ParticipantAbsentCount = participants.Count(p => p.AttendanceStatus == "ABSENT"),
                ParticipantExcusedCount = participants.Count(p => p.AttendanceStatus == "EXCUSED"),
                ActionItemTotal = actionItems.Count,
                ActionItemTodoCount = actionItems.Count(ai => ai.Status == "TODO"),
                ActionItemInProgressCount = actionItems.Count(ai => ai.Status == "IN_PROGRESS"),
                ActionItemDoneCount = actionItems.Count(ai => ai.Status == "DONE"),
                ActionItemCancelledCount = actionItems.Count(ai => ai.Status == "CANCELLED"),
                ActionItemOverdueCount = actionItems.Count(ai => ai.Status != "DONE" && ai.Status != "CANCELLED" && ai.DueDate.HasValue && ai.DueDate.Value < now)
            });
        }

        return new SearchAndFilterMinutesDto
        {
            Items = items,
            TotalCount = filteredCount,
            Summary = new MinutesSummaryDto
            {
                TotalMinutes = totalMinutes,
                DraftCount = draftCount,
                SavedCount = savedCount,
                LockedCount = lockedCount,
                OpenActionItemCount = openActionItemCount,
                LatestUpdatedAt = latestUpdatedAt.HasValue ? DateTime.SpecifyKind(latestUpdatedAt.Value, DateTimeKind.Utc).ToString("O") : null
            }
        };
    }
}