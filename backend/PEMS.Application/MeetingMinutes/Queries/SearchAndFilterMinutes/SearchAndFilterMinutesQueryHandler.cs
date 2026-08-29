using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Minutes;
using PEMS.Shared;
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

        var joined = _db.Minutes
            .Join(_db.VisitRequestCampuses,
                  m => m.VisitInstanceId,
                  vrc => vrc.VisitInstanceId,
                  (m, vrc) => new { m, vrc });

        // HO may optionally self-filter to one campus; every other role's actual visibility is
        // decided below by MinuteAccess.WhereAuthorizedFor, not by campus membership alone — the
        // removed "!isHo && PrimaryCampusId == null => Forbidden" guard used to block a legitimate
        // registrant/operational-contact (guest-side) caller who may have no PrimaryCampusId at all.
        if (isHo && request.CampusId.HasValue)
        {
            joined = joined.Where(x => x.vrc.CampusId == request.CampusId.Value);
        }

        // SEC-11: authorization MUST run before Count/Skip/Take below — applying it after pagination
        // would produce a wrong TotalCount/page and could leak "this many exist" through the summary
        // counts even when individual rows are filtered out.
        var query = MinuteAccess.WhereAuthorizedFor(joined.Select(x => x.m), _db, _currentUser);

        // Summary queries before pagination but after scope filtering.
        // DB-SHAPE-001: these were 4 sequential COUNT(*) round trips over the identical `query` set,
        // differing only in predicate - merged into one GROUP BY query (COUNT(*) plus three
        // conditional counts, translated to SUM(CASE WHEN ...) in SQL) so the summary panel costs one
        // round trip instead of four. GroupBy(m => 1) yields no group at all when `query` matches
        // nothing, so all four default to 0 below - exactly what four independent CountAsync calls
        // already returned for an empty source.
        var now = VietnamTime.Now();
        var summaryCounts = await query
            .GroupBy(m => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Draft = g.Count(m => m.Status == "DRAFT"),
                Saved = g.Count(m => m.Status == "SAVED"),
                Locked = g.Count(m => m.EditLockedBy != null && m.EditLockExpiresAt != null && m.EditLockExpiresAt > now),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalMinutes = summaryCounts?.Total ?? 0;
        var draftCount = summaryCounts?.Draft ?? 0;
        var savedCount = summaryCounts?.Saved ?? 0;
        var lockedCount = summaryCounts?.Locked ?? 0;

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

        // Pagination. DB-PAGE-002: the existing floor left PageSize unbounded above.
        var pageSize = Math.Clamp(request.PageSize > 0 ? request.PageSize : 10, 1, 100);
        var page = request.Page > 0 ? request.Page : 1;
        var minutesPage = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        // ── Batch the page's relations. Each row used to issue nine sequential queries of its own
        //    (instance, campus, host participant, host name, three user names, participants, action
        //    items), so a ten-row page cost ninety round trips. The set of ids is known once the page
        //    is materialised, so every lookup below is one query for the whole page. ──
        var pageInstanceIds = minutesPage.Select(m => m.VisitInstanceId).Distinct().ToList();
        var pageMinutesIds = minutesPage.Select(m => m.MinutesId).Distinct().ToList();

        // Per-campus v2: each row titles from ITS OWN instance detail — never a sibling's, and never a
        // request-level name, which does not exist.
        var instancesById = await _db.VisitRequestCampuses
            .AsNoTracking()
            .Include(v => v.FormDetail)
            .Where(v => pageInstanceIds.Contains(v.VisitInstanceId))
            .ToDictionaryAsync(v => v.VisitInstanceId, cancellationToken);

        var campusNames = await _db.Campuses.AsNoTracking()
            .Where(c => instancesById.Values.Select(v => v.CampusId).Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        var hostByInstance = (await _db.VisitParticipants.AsNoTracking()
                .Where(p => pageInstanceIds.Contains(p.VisitInstanceId) && p.IsHost)
                .Select(p => new { p.VisitInstanceId, p.UserId, p.ParticipantId })
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.VisitInstanceId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.ParticipantId).First().UserId);

        var neededUserIds = hostByInstance.Values
            .Concat(minutesPage.Where(m => m.EditLockedBy.HasValue).Select(m => m.EditLockedBy!.Value))
            .Concat(minutesPage.Where(m => m.CreatedBy.HasValue).Select(m => m.CreatedBy!.Value))
            .Concat(minutesPage.Where(m => m.UpdatedBy.HasValue).Select(m => m.UpdatedBy!.Value))
            .Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => neededUserIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        // A person the Host removed from a biên bản keeps their row so the next sync does not offer
        // them again (MIN-03), but they are no longer in that biên bản — so they must not be counted
        // or listed in a view of it.
        var participantsByMinutes = (await _db.MinuteParticipants.AsNoTracking()
                .Where(p => pageMinutesIds.Contains(p.MinutesId)
                    && p.SyncState != MinuteParticipantSyncStates.Excluded)
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.MinutesId).ToDictionary(g => g.Key, g => g.ToList());
        var actionItemsByMinutes = (await _db.MinuteActionItems.AsNoTracking()
                .Where(ai => pageMinutesIds.Contains(ai.MinutesId)).ToListAsync(cancellationToken))
            .GroupBy(ai => ai.MinutesId).ToDictionary(g => g.Key, g => g.ToList());

        string? NameOf(ulong? userId)
            => userId.HasValue && userNames.TryGetValue(userId.Value, out var n) ? n : null;

        var items = new List<MinutesListItemDto>();
        foreach (var minute in minutesPage)
        {
            instancesById.TryGetValue(minute.VisitInstanceId, out var vrc);

            var campusName = vrc != null && campusNames.TryGetValue(vrc.CampusId, out var cn) ? cn : null;
            var hostName = hostByInstance.TryGetValue(minute.VisitInstanceId, out var hostUserId)
                ? NameOf(hostUserId)
                : null;
            var lockedByName = NameOf(minute.EditLockedBy);
            var createdByName = NameOf(minute.CreatedBy);
            var updatedByName = NameOf(minute.UpdatedBy);

            var participants = participantsByMinutes.TryGetValue(minute.MinutesId, out var ps)
                ? ps : new List<Domain.Entities.Minutes.MinuteParticipant>();
            var actionItems = actionItemsByMinutes.TryGetValue(minute.MinutesId, out var ais)
                ? ais : new List<Domain.Entities.Minutes.MinuteActionItem>();

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
                VisitTitle = vrc?.FormDetail?.DelegationName,
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