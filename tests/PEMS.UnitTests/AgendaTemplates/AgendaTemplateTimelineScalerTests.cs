using System;
using System.Collections.Generic;
using PEMS.Application.AgendaTemplates.Common;
using Xunit;

namespace PEMS.UnitTests.AgendaTemplates;

/// <summary>
/// Core proportional-scaling math used by ApplyAgendaTemplateCommandHandler: a template's
/// StartOffsetMinutes/DurationMinutes stay stored as plain minutes (no schema change), but at apply
/// time they are read as a ratio against the template's own span and re-projected onto the visit
/// instance's actual [PlannedStartAt, PlannedEndAt] window.
/// </summary>
public class AgendaTemplateTimelineScalerTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 9, 0, 0);

    // ── ComputeTemplateSpanMinutes ──────────────────────────────────────────

    [Fact]
    public void Span_is_the_furthest_endpoint_not_the_summed_duration()
    {
        // Item A: 0-20, Item B: 30-80 (a 10-minute gap between them).
        // sum(duration) would be 20+50=70; the real span is the furthest endpoint, 80.
        var items = new (int, int)[] { (0, 20), (30, 50) };
        Assert.Equal(80, AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(items));
    }

    [Fact]
    public void Span_of_empty_template_is_zero()
    {
        Assert.Equal(0, AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(Array.Empty<(int, int)>()));
    }

    // ── Scale: core cases (spec §22) ────────────────────────────────────────

    private static readonly (int StartOffsetMinutes, int DurationMinutes)[] ThreeItemTemplate =
    {
        (0, 20),   // 0-20
        (20, 70),  // 20-90
        (90, 30),  // 90-120
    };

    [Fact]
    public void Same_duration_visit_keeps_the_baseline_timeline()
    {
        var span = AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(ThreeItemTemplate);
        var end = Start.AddMinutes(120);

        var result = AgendaTemplateTimelineScaler.Scale(Start, end, span, ThreeItemTemplate);

        AssertBoundary(result[0], 0, 20);
        AssertBoundary(result[1], 20, 90);
        AssertBoundary(result[2], 90, 120);
    }

    [Fact]
    public void Scale_down_50_percent_halves_every_boundary()
    {
        var span = AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(ThreeItemTemplate);
        var end = Start.AddMinutes(60); // visit is half the template's span

        var result = AgendaTemplateTimelineScaler.Scale(Start, end, span, ThreeItemTemplate);

        AssertBoundary(result[0], 0, 10);
        AssertBoundary(result[1], 10, 45);
        AssertBoundary(result[2], 45, 60);
    }

    [Fact]
    public void Scale_up_200_percent_doubles_every_boundary()
    {
        var span = AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(ThreeItemTemplate);
        var end = Start.AddMinutes(240); // visit is double the template's span

        var result = AgendaTemplateTimelineScaler.Scale(Start, end, span, ThreeItemTemplate);

        AssertBoundary(result[0], 0, 40);
        AssertBoundary(result[1], 40, 180);
        AssertBoundary(result[2], 180, 240);
    }

    [Theory]
    [InlineData(37)]   // odd, non-round visit length
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(241)]
    [InlineData(500)]
    public void Last_items_end_always_lands_exactly_on_plannedEnd(int visitMinutes)
    {
        var span = AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(ThreeItemTemplate);
        var end = Start.AddMinutes(visitMinutes);

        var result = AgendaTemplateTimelineScaler.Scale(Start, end, span, ThreeItemTemplate);

        Assert.Equal(end, result[^1].End);
    }

    // ── Gaps are preserved proportionally (spec §23) ────────────────────────

    [Fact]
    public void Gap_between_items_scales_proportionally_and_items_are_not_auto_joined()
    {
        // Item A: 0-20, Item B: 30-60 -> 10-minute gap out of a 60-minute span (1/6 of the template).
        var items = new (int, int)[] { (0, 20), (30, 30) };
        var span = AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(items);
        Assert.Equal(60, span);

        var end = Start.AddMinutes(120); // visit is double the template span
        var result = AgendaTemplateTimelineScaler.Scale(Start, end, span, items);

        AssertBoundary(result[0], 0, 40);   // Item A: 0-40
        AssertBoundary(result[1], 60, 120); // Item B: 60-120
        // The gap (40 -> 60 = 20 minutes) is double the original 10-minute gap, same 1/6 ratio of span.
        Assert.Equal(20, (result[1].Start - result[0].End).TotalMinutes);
    }

    // ── Existing (legacy) templates need no migration (spec §25) ────────────

    [Fact]
    public void A_template_shaped_exactly_like_existing_seed_data_still_applies()
    {
        // Mirrors a typical seeded template: sequential items, no gaps, using only the two columns
        // that have always existed on agenda_template_items.
        var items = new (int, int)[] { (0, 15), (15, 45), (60, 30), (90, 30) };
        var span = AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(items);
        Assert.Equal(120, span);

        var end = Start.AddMinutes(90);
        var result = AgendaTemplateTimelineScaler.Scale(Start, end, span, items);

        Assert.Equal(4, result.Count);
        Assert.Equal(Start, result[0].Start);
        Assert.Equal(end, result[^1].End);
        for (var i = 0; i < result.Count; i++)
            Assert.True(result[i].End > result[i].Start, $"item {i} must not collapse to zero length");
    }

    // ── Invalid input fails closed (spec §26-27) ─────────────────────────────

    [Fact]
    public void PlannedEnd_not_after_plannedStart_throws()
    {
        var span = AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(ThreeItemTemplate);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => AgendaTemplateTimelineScaler.Scale(Start, Start, span, ThreeItemTemplate));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AgendaTemplateTimelineScaler.Scale(Start, Start.AddMinutes(-10), span, ThreeItemTemplate));
    }

    [Fact]
    public void Zero_or_negative_template_span_throws_instead_of_dividing_by_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AgendaTemplateTimelineScaler.Scale(Start, Start.AddMinutes(60), 0, ThreeItemTemplate));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AgendaTemplateTimelineScaler.Scale(Start, Start.AddMinutes(60), -5, ThreeItemTemplate));
    }

    // ── Degenerate rounding never produces EndTime <= StartTime (spec §10-11) ─

    [Fact]
    public void Extreme_downscale_never_collapses_an_item_to_zero_or_negative_length()
    {
        // 20 items of 1 minute each back-to-back (span 20), applied to a 1-minute visit: every ratio
        // step rounds to the same minute, which would collapse every item to zero length without a guard.
        var items = new List<(int, int)>();
        for (var i = 0; i < 20; i++) items.Add((i, 1));

        var span = AgendaTemplateTimelineScaler.ComputeTemplateSpanMinutes(items);
        var end = Start.AddMinutes(1);
        var result = AgendaTemplateTimelineScaler.Scale(Start, end, span, items);

        Assert.Equal(20, result.Count);
        foreach (var boundary in result)
            Assert.True(boundary.End > boundary.Start, "every item must keep a positive length");
    }

    private static void AssertBoundary(AgendaTemplateTimelineScaler.ScaledBoundary boundary, int expectedStartMinutes, int expectedEndMinutes)
    {
        Assert.Equal(Start.AddMinutes(expectedStartMinutes), boundary.Start);
        Assert.Equal(Start.AddMinutes(expectedEndMinutes), boundary.End);
    }
}
