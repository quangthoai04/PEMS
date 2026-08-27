using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Enums;

namespace PEMS.Application.Delegations.Reminders;

/// <summary>
/// Keeps <c>visit_instance_reminder_settings</c> PENDING rows honest against the two things that can
/// invalidate a reminder someone already configured: the campus instance leaving BEFORE_VISIT (the
/// only stage a "chuẩn bị trước chuyến thăm" reminder means anything in), and the instance's planned
/// start time moving after the reminder was scheduled against the old one.
///
/// <para>
/// This is Layer 1 of the reminder eligibility defence — called from every lifecycle command that can
/// move a BEFORE_VISIT instance away, and from every command that can move its planned start. Layer 2
/// is <see cref="VisitReminderDispatchService"/> revalidating the instance's live status right before
/// it would actually send — a PENDING row this layer failed to catch (a future write site, a direct
/// DB edit) must still never fire.
/// </para>
///
/// <para>
/// Only PENDING rows are ever touched. SENT/FAILED/CANCELLED are history and this class never rewrites
/// history, matching <see cref="VisitReminderDispatchService"/>'s own rule for the same table.
/// </para>
///
/// <para>
/// Every write here is a conditional <c>ExecuteUpdateAsync</c> re-checking <c>status = PENDING</c> at
/// the database, the same technique <see cref="VisitReminderDispatchService"/>'s own claim uses and for
/// the same reason: a plain load-mutate-SaveChanges here would carry no WHERE beyond the primary key,
/// so if a dispatch tick's claim already moved a row to SENT a moment after this method's own SELECT
/// read it as PENDING, SaveChanges would blindly overwrite that SENT status back to CANCELLED —
/// corrupting the record of a message that had already gone out to a real person. The conditional
/// UPDATE instead loses that race cleanly: it matches zero rows and leaves the SENT row alone.
/// </para>
/// </summary>
public static class VisitReminderLifecycleSync
{
    /// <summary>
    /// Cancels every PENDING reminder of one instance because it left BEFORE_VISIT (cancelled,
    /// rejected — though a rejected instance can never have reached BEFORE_VISIT in the first place
    /// and so never has a row to cancel — or advanced to DURING_VISIT/AFTER_VISIT/CLOSED). Safe to call
    /// unconditionally: an instance with no PENDING rows is simply a no-op query. One bulk conditional
    /// UPDATE, atomic for every row it touches.
    /// </summary>
    public static Task<int> CancelPendingForIneligibleStatusAsync(
        IApplicationDbContext db, ulong visitInstanceId, DateTime now, CancellationToken ct)
        => db.VisitInstanceReminderSettings
            .Where(r => r.VisitInstanceId == visitInstanceId && r.Status == VisitReminderStatus.PENDING)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.Status, VisitReminderStatus.CANCELLED)
                    .SetProperty(r => r.ErrorMessage, ReminderCancelReasons.Record(ReminderCancelReasons.VisitNoLongerEligible))
                    .SetProperty(r => r.UpdatedAt, now),
                ct);

    /// <summary>
    /// Recomputes every PENDING reminder's <c>scheduled_at</c> after the instance's planned start
    /// changes, so "N phút/giờ/ngày/tuần trước" keeps meaning N before the NEW start — never stuck at
    /// the old one. A reminder whose new moment would already be in the past (the schedule moved
    /// earlier than the offset allows) is cancelled instead of left to fire immediately: a reminder
    /// firing the moment a schedule changes is not "N before" anything the recipient asked to be
    /// warned about, it is a surprise, so it is treated the same as any other now-invalid schedule.
    ///
    /// <para>
    /// The outcome (reschedule vs. cancel) is data-dependent per row, so this reads the candidate rows
    /// first — but every actual WRITE is still its own single-row conditional
    /// <c>ExecuteUpdateAsync(... WHERE id = ? AND status = PENDING)</c>, not a bulk SaveChanges over the
    /// loaded snapshot. A row count here is small (one per configured channel/audience, at most 4), so
    /// looping single-row atomic updates costs nothing worth avoiding it for.
    /// </para>
    /// </summary>
    public static async Task RescheduleForPlannedStartChangeAsync(
        IApplicationDbContext db, ulong visitInstanceId, DateTime newPlannedStartAt, DateTime now, CancellationToken ct)
    {
        var pending = await db.VisitInstanceReminderSettings
            .AsNoTracking()
            .Where(r => r.VisitInstanceId == visitInstanceId && r.Status == VisitReminderStatus.PENDING)
            .Select(r => new { r.ReminderSettingId, r.OffsetMinutes })
            .ToListAsync(ct);

        foreach (var reminder in pending)
        {
            var newScheduledAt = newPlannedStartAt.AddMinutes(-reminder.OffsetMinutes);

            if (newScheduledAt <= now)
            {
                await db.VisitInstanceReminderSettings
                    .Where(r => r.ReminderSettingId == reminder.ReminderSettingId && r.Status == VisitReminderStatus.PENDING)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(r => r.Status, VisitReminderStatus.CANCELLED)
                            .SetProperty(r => r.ErrorMessage, ReminderCancelReasons.Record(ReminderCancelReasons.ScheduleNoLongerValid))
                            .SetProperty(r => r.UpdatedAt, now),
                        ct);
            }
            else
            {
                await db.VisitInstanceReminderSettings
                    .Where(r => r.ReminderSettingId == reminder.ReminderSettingId && r.Status == VisitReminderStatus.PENDING)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(r => r.ScheduledAt, newScheduledAt)
                            .SetProperty(r => r.UpdatedAt, now),
                        ct);
            }
        }
    }
}
