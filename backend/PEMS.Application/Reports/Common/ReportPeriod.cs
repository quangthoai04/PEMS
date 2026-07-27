using System;

namespace PEMS.Application.Reports.Common;

/// <summary>
/// The one place a reporting period turns into the two strings a reader sees.
///
/// <para>
/// It exists because those strings now appear twice — in the email subject/body rendered from
/// <c>email_templates</c>, and on the cover of the PDF attached to it — and the two must never disagree.
/// The reporting queries work on a half-open range <c>[from, toExclusive)</c>, while a reader expects
/// the last day IN the period; converting that in one place is what stops one surface saying
/// "01/07 – 31/07" while the other says "01/07 – 01/08".
/// </para>
/// </summary>
public static class ReportPeriod
{
    /// <summary>Shown when a bound was never given — the invoice panels allow an open start.</summary>
    public const string NotSpecified = "—";

    public static string Label(DateTime value) => value.ToString("dd/MM/yyyy");

    /// <summary>
    /// Labels for a half-open range as the report guards produce it. The upper bound is stepped back one
    /// day so it names the last day included, which is what the previous email subjects already showed.
    /// </summary>
    public static (string From, string To) Labels(DateTime fromVn, DateTime toVnExclusive)
        => (Label(fromVn), Label(toVnExclusive.AddDays(-1)));

    /// <summary>
    /// Labels for the invoice panels, which do not filter by period at all — the lines are the ones the
    /// sender ticked, and the dates are a caption. An absent start stays absent rather than being
    /// invented as "the beginning of the year".
    /// </summary>
    public static (string From, string To) InvoiceLabels(DateTime? fromDate, DateTime? toDate, DateTime nowVn)
        => (fromDate.HasValue ? Label(fromDate.Value) : NotSpecified, Label(toDate ?? nowVn));
}
