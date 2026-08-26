using System;
using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.AgendaTemplates.Common;

/// <summary>
/// Proportionally maps an agenda template's relative timeline (StartOffsetMinutes / DurationMinutes —
/// still the only two fields stored on <c>agenda_template_items</c>) onto the actual planned window of
/// one visit instance (<c>PlannedStartAt</c> .. <c>PlannedEndAt</c>).
///
/// The template's minutes are read as a RATIO against the template's own span, not as absolute elapsed
/// time from PlannedStartAt — so a template authored against a 120-minute visit still lands correctly on
/// a 60-minute or a 240-minute one. No schema change: existing templates keep working unmodified, their
/// stored minutes simply become the baseline the ratio is computed from.
/// </summary>
public static class AgendaTemplateTimelineScaler
{
    /// <summary>One template item's [start, end) boundary, scaled onto the actual visit window.</summary>
    public readonly record struct ScaledBoundary(DateTime Start, DateTime End);

    /// <summary>
    /// templateSpanMinutes = max(StartOffsetMinutes + DurationMinutes) across all items — the furthest
    /// endpoint any item reaches. Deliberately NOT sum(DurationMinutes): a template may have gaps
    /// between items (a break) or overlapping items, and summing durations would misrepresent the
    /// timeline's actual length in either case.
    /// </summary>
    public static int ComputeTemplateSpanMinutes(
        IReadOnlyCollection<(int StartOffsetMinutes, int DurationMinutes)> items)
    {
        if (items.Count == 0) return 0;
        return items.Max(i => i.StartOffsetMinutes + i.DurationMinutes);
    }

    /// <summary>
    /// Scales every item's template-relative [start, end) boundary onto [plannedStart, plannedEnd],
    /// preserving <paramref name="orderedItems"/>'s order 1:1 in the result.
    ///
    /// Each boundary is computed independently as <c>plannedStart + visitSpan * (offset / templateSpan)</c>
    /// — never by scaling a duration and chaining it onto the previous item's computed end — so per-item
    /// minute rounding can never accumulate into drift across the timeline. The item whose template-relative
    /// end equals <paramref name="templateSpanMinutes"/> (there is always at least one — the item(s) that
    /// define the span) has its End pinned EXACTLY to <paramref name="plannedEnd"/> rather than recomputed
    /// through the ratio, so the agenda's last boundary always lands on the visit's real end regardless of
    /// rounding.
    ///
    /// Guards against a degenerate rounding case where an item's scaled duration collapses to zero
    /// minutes (possible when the visit window is much shorter than the template, e.g. many items packed
    /// into a template applied to a very short visit): End is bumped forward by 1 minute so no row is ever
    /// persisted with EndTime &lt;= StartTime. For the item pinned to plannedEnd this can, only in that
    /// pathological case, push End one minute past plannedEnd — preferred over silently writing an
    /// invalid/zero-length agenda row.
    /// </summary>
    public static IReadOnlyList<ScaledBoundary> Scale(
        DateTime plannedStart,
        DateTime plannedEnd,
        int templateSpanMinutes,
        IReadOnlyList<(int StartOffsetMinutes, int DurationMinutes)> orderedItems)
    {
        if (templateSpanMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(templateSpanMinutes), "Template span must be greater than 0.");
        if (plannedEnd <= plannedStart)
            throw new ArgumentOutOfRangeException(nameof(plannedEnd), "plannedEnd must be after plannedStart.");

        var visitSpan = plannedEnd - plannedStart;
        var result = new List<ScaledBoundary>(orderedItems.Count);

        foreach (var item in orderedItems)
        {
            var templateEnd = item.StartOffsetMinutes + item.DurationMinutes;

            var start = ScaleBoundary(plannedStart, visitSpan, item.StartOffsetMinutes, templateSpanMinutes);
            var end = templateEnd >= templateSpanMinutes
                ? plannedEnd
                : ScaleBoundary(plannedStart, visitSpan, templateEnd, templateSpanMinutes);

            if (end <= start)
                end = start.AddMinutes(1);

            result.Add(new ScaledBoundary(start, end));
        }

        return result;
    }

    /// <summary>
    /// plannedStart + visitSpan * (templateMinuteOffset / templateSpanMinutes), rounded to the nearest
    /// whole minute. Computed directly from plannedStart every time (never from a previously-computed
    /// boundary), which is what keeps rounding independent per boundary instead of compounding.
    /// </summary>
    private static DateTime ScaleBoundary(
        DateTime plannedStart, TimeSpan visitSpan, int templateMinuteOffset, int templateSpanMinutes)
    {
        var ratio = (decimal)templateMinuteOffset / templateSpanMinutes;
        var scaledMinutes = (decimal)visitSpan.TotalMinutes * ratio;
        var roundedMinutes = Math.Round(scaledMinutes, MidpointRounding.AwayFromZero);
        return plannedStart.AddMinutes((double)roundedMinutes);
    }
}
