using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Common;

/// <summary>
/// Computes schedule conflicts for a set of candidate users against a target visit window.
/// Mirrors the host-candidate rules (UC-22): overlap = existing.start &lt; target.end AND
/// existing.end &gt; target.start (an event that ends exactly when the visit starts — or starts
/// when it ends — is NOT a conflict). Two sources:
///   A. the user's own calendar_events (private personal events are masked to "Lịch cá nhân"),
///   B. other LIVE visit instances they host or actively participate in.
/// All work is entity-rooted + enriched in-memory (Pomelo translation safety — no correlated
/// subqueries on optional FKs).
/// </summary>
public static class ScheduleConflictResolver
{
    public sealed class Conflict
    {
        public string Source { get; init; } = default!;   // CALENDAR | VISIT_INSTANCE
        public string? Title { get; init; }
        public DateTime StartAt { get; init; }
        public DateTime EndAt { get; init; }
        public bool IsPrivate { get; init; }
    }

    /// <summary>
    /// Returns conflicts keyed by user id. Every id in <paramref name="userIds"/> is present in the
    /// result (empty list when conflict-free), so callers can index without a TryGetValue dance.
    /// </summary>
    public static async Task<Dictionary<ulong, List<Conflict>>> ResolveAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<ulong> userIds,
        DateTime windowStart,
        DateTime windowEnd,
        ulong excludeVisitInstanceId,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => new List<Conflict>());
        if (ids.Count == 0) return result;

        // ── Source A: personal/other calendar events overlapping the window ──
        var calendarConflicts = await db.CalendarEvents
            .Where(e => ids.Contains(e.OwnerUserId)
                        && e.Status == "ACTIVE"
                        && e.DeletedAt == null
                        && e.StartAt < windowEnd
                        && e.EndAt > windowStart)
            .Select(e => new { e.OwnerUserId, e.Title, e.SourceType, e.Visibility, e.StartAt, e.EndAt })
            .ToListAsync(cancellationToken);

        foreach (var e in calendarConflicts)
        {
            // Never leak the content of a private personal event.
            var isPrivate = e.SourceType == "PERSONAL" && e.Visibility == "PRIVATE";
            result[e.OwnerUserId].Add(new Conflict
            {
                Source = "CALENDAR",
                Title = isPrivate ? "Lịch cá nhân" : e.Title,
                StartAt = e.StartAt,
                EndAt = e.EndAt,
                IsPrivate = isPrivate,
            });
        }

        // ── Source B1: other LIVE instances they are the official host of ──
        var hostBusy = await db.VisitRequestCampuses
            .Where(c => c.CurrentHostUserId != null
                        && ids.Contains(c.CurrentHostUserId.Value)
                        && c.VisitInstanceId != excludeVisitInstanceId
                        && (c.Status == VisitInstanceStatus.Assigned
                            || c.Status == VisitInstanceStatus.BeforeVisit
                            || c.Status == VisitInstanceStatus.DuringVisit)
                        && c.PlannedStartAt < windowEnd
                        && c.PlannedEndAt > windowStart)
            .Select(c => new
            {
                UserId = c.CurrentHostUserId!.Value,
                c.VisitInstanceId,
                c.PlannedStartAt,
                c.PlannedEndAt,
                // Conflict label: mixed v2 shows the BUSY instance's own detail name.
                DelegationName = c.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                                 && c.VisitRequest.HasMixedCampusDetails
                    ? (c.FormDetail != null ? c.FormDetail.DelegationName : null)
                    : c.VisitRequest.DelegationName,
            })
            .ToListAsync(cancellationToken);

        // ── Source B2: other LIVE instances they actively participate in ──
        var participantBusy = await (
            from p in db.VisitParticipants
            join c in db.VisitRequestCampuses on p.VisitInstanceId equals c.VisitInstanceId
            where ids.Contains(p.UserId)
                  && (p.Status == ParticipantStatuses.Invited
                      || p.Status == ParticipantStatuses.Accepted
                      || p.Status == ParticipantStatuses.Assigned)
                  && c.VisitInstanceId != excludeVisitInstanceId
                  && (c.Status == VisitInstanceStatus.Assigned
                      || c.Status == VisitInstanceStatus.BeforeVisit
                      || c.Status == VisitInstanceStatus.DuringVisit)
                  && c.PlannedStartAt < windowEnd
                  && c.PlannedEndAt > windowStart
            select new
            {
                p.UserId,
                c.VisitInstanceId,
                c.PlannedStartAt,
                c.PlannedEndAt,
                // Conflict label: mixed v2 shows the BUSY instance's own detail name.
                DelegationName = c.VisitRequest.FormSchemaVersion >= FormSchemaVersions.PerCampus
                                 && c.VisitRequest.HasMixedCampusDetails
                    ? (c.FormDetail != null ? c.FormDetail.DelegationName : null)
                    : c.VisitRequest.DelegationName,
            }).ToListAsync(cancellationToken);

        // Merge host + participant instance busyness, deduped per (user, instance).
        foreach (var group in hostBusy.Concat(participantBusy).GroupBy(b => b.UserId))
        {
            foreach (var inst in group.GroupBy(g => g.VisitInstanceId).Select(g => g.First()))
            {
                result[group.Key].Add(new Conflict
                {
                    Source = "VISIT_INSTANCE",
                    Title = inst.DelegationName ?? "Đoàn khách khác",
                    StartAt = inst.PlannedStartAt,
                    EndAt = inst.PlannedEndAt,
                    IsPrivate = false,
                });
            }
        }

        foreach (var list in result.Values)
            list.Sort((a, b) => a.StartAt.CompareTo(b.StartAt));

        return result;
    }

    /// <summary>Short human-readable summary for the candidate row (null when conflict-free).</summary>
    public static string? BuildSummary(IReadOnlyList<Conflict> conflicts)
        => conflicts is { Count: > 0 }
            ? $"Có {conflicts.Count} lịch trùng với thời gian tiếp khách."
            : null;
}
