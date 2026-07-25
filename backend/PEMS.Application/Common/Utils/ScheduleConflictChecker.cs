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
        public static async Task<bool> HasConflictAsync(
            IApplicationDbContext context,
            ulong targetUserId,
            DateTime startAt,
            DateTime endAt,
            ulong? currentLogisticsItemId,
            ulong? currentParticipantId,
            CancellationToken cancellationToken)
        {
            // 1. Personal calendar events in database
            bool personalConflict = await context.CalendarEvents.AsNoTracking().AnyAsync(e =>
                e.OwnerUserId == targetUserId &&
                e.Status == "ACTIVE" &&
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

            if (invitationConflict) return true;

            // 3. Assigned / Accepted logistics requests in database
            bool requestConflict = await (
                from l in context.VisitLogisticsItems.AsNoTracking()
                join c in context.VisitRequestCampuses.AsNoTracking() on l.VisitInstanceId equals c.VisitInstanceId
                join vr in context.VisitRequests.AsNoTracking() on c.VisitRequestId equals vr.VisitRequestId
                where l.AssignedToUserId == targetUserId
                      && (l.Status == "ACCEPTED" || l.Status == "ASSIGNED" || l.Status == "IN_PROGRESS" || l.Status == "CHANGE_PROPOSED")
                      && vr.Status != "CANCELLED"
                      && c.Status != "CANCELLED"
                      && (currentLogisticsItemId == null || l.LogisticsItemId != currentLogisticsItemId.Value)
                      && startAt < (l.UsageEndAt ?? c.PlannedEndAt)
                      && endAt > (l.UsageStartAt ?? c.PlannedStartAt)
                select l.LogisticsItemId
            ).AnyAsync(cancellationToken);

            if (requestConflict) return true;

            return false;
        }
    }
}
