using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Common.Utils
{
    public static class ScheduleConflictChecker
    {
        /// <summary>
        /// Invitation conflict check: only checks personal calendar events and OTHER accepted invitations.
        /// Does NOT check logistics request tasks (spec: a person can accept a request task and an invitation at the same time).
        /// </summary>
        public static async Task<bool> HasInvitationConflictAsync(
            IApplicationDbContext context,
            ulong targetUserId,
            DateTime startAt,
            DateTime endAt,
            ulong? currentParticipantId,
            CancellationToken cancellationToken,
            ulong? excludeCalendarEventId = null)
        {
            // 1. Personal calendar events in database
            bool personalConflict = await context.CalendarEvents.AsNoTracking().AnyAsync(e =>
                e.OwnerUserId == targetUserId &&
                e.Status == "ACTIVE" &&
                (excludeCalendarEventId == null || e.CalendarEventId != excludeCalendarEventId.Value) &&
                startAt < e.EndAt &&
                endAt > e.StartAt, cancellationToken);

            if (personalConflict) return true;

            // 2. Accepted invitations in database
            bool invitationConflict = await (
                from p in context.VisitParticipants.AsNoTracking()
                join c in context.VisitRequestCampuses.AsNoTracking() on p.VisitInstanceId equals c.VisitInstanceId
                join vr in context.VisitRequests.AsNoTracking() on c.VisitRequestId equals vr.VisitRequestId
                where p.UserId == targetUserId
                      && p.Status == "ACCEPTED"
                      && vr.Status != "CANCELLED"
                      && c.Status != "CANCELLED"
                      && (currentParticipantId == null || p.ParticipantId != currentParticipantId.Value)
                      && startAt < c.PlannedEndAt
                      && endAt > c.PlannedStartAt
                select p.ParticipantId
            ).AnyAsync(cancellationToken);

            return invitationConflict;
        }

        public static async Task<bool> HasConflictAsync(
            IApplicationDbContext context,
            ulong targetUserId,
            DateTime startAt,
            DateTime endAt,
            ulong? currentLogisticsItemId,
            ulong? currentParticipantId,
            CancellationToken cancellationToken,
            ulong? excludeCalendarEventId = null)
        {
            return await HasInvitationConflictAsync(context, targetUserId, startAt, endAt, currentParticipantId, cancellationToken, excludeCalendarEventId);
        }
    }
}
