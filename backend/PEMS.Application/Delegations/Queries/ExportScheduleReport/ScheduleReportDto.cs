namespace PEMS.Application.Delegations.Queries.ExportScheduleReport;

/// <summary>Fully-resolved content of the "Báo cáo Lịch trình" PDF for one campus instance —
/// everything the renderer needs, with no further DB access. Kept separate from the PDF-drawing
/// code so the data-mapping rules (composition lists, logo rule, agenda venue fallback) are
/// unit-testable without generating actual PDF bytes.</summary>
public sealed class ScheduleReportDto
{
    public string DelegationName { get; set; } = default!;
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }

    /// <summary>Overview "Địa điểm" field — always "FPT University" per spec (the agenda table's own
    /// Venue column carries the per-activity location).</summary>
    public string Location { get; set; } = "FPT University";

    public string? Purpose { get; set; }

    /// <summary>Guest members + external-support members from the registration form, merged into one
    /// "Thành phần phía khách" list.</summary>
    public List<ScheduleReportPersonDto> GuestSide { get; set; } = new();

    /// <summary>Host + accepted (non-host) participants — "Thành phần phía FPT". Never includes
    /// invited/declined/removed rows.</summary>
    public List<ScheduleReportPersonDto> FptSide { get; set; } = new();

    public List<ScheduleReportAgendaRowDto> Agenda { get; set; } = new();

    /// <summary>Set only when the linked Partner has a logo file — tells the renderer to draw the
    /// FPT logo on the left and the partner logo on the right instead of a single centered FPT logo.</summary>
    public ulong? PartnerLogoFileId { get; set; }
    public string? PartnerName { get; set; }
}

public sealed class ScheduleReportPersonDto
{
    public string FullName { get; set; } = default!;
    public string? Organization { get; set; }
    /// <summary>Guest side: "Khách mời" / "Nhân sự hỗ trợ". FPT side: "Host" / role label.</summary>
    public string? RoleLabel { get; set; }
}

public sealed class ScheduleReportAgendaRowDto
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    /// <summary>Falls back to "FPT University" when the agenda row has no explicit location.</summary>
    public string Venue { get; set; } = default!;

    /// <summary>
    /// Who runs this item — the free-typed name the Host entered, or EMPTY when they left it blank.
    ///
    /// <para>
    /// Empty rather than "FPT University". The PDF's "Party in Charge" column used to print that literal
    /// on every row, so a guest reading the schedule could not tell who to look for at any point in the
    /// day; the fallback then survived into the setup-progress email's HTML table, where it read as a
    /// stray cell in an otherwise per-activity row. Both renderers now show their own "no value"
    /// placeholder, which is what the rest of their columns already do.
    /// </para>
    /// </summary>
    public string Responsible { get; set; } = string.Empty;
}
