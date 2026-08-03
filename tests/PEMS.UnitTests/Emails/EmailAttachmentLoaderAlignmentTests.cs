using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Enums;
using PEMS.Application.Emails.Common;
using PEMS.UnitTests.Delegations.ExportScheduleReport;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The attachment loader's contract about POSITION.
///
/// <para>
/// <see cref="EmailAttachmentLoader.LoadAsync"/> drops files it cannot read, which is the right
/// behaviour for a mailer that must not be stopped by one missing blob. The danger is entirely in what
/// a caller then does with the shorter list: the draft dispatcher paired it back against its own
/// attachment rows by index, so a single unreadable file slid every later attachment onto the previous
/// row's display name — a message could go out with one document's filename wrapped around another
/// document's bytes. On the setup-progress email that is an internal file reaching a guest under the
/// name of the Schedule Report.
/// </para>
/// </summary>
public class EmailAttachmentLoaderAlignmentTests
{
    private const ulong ReportFileId = 5001;
    private const ulong PhotoFileId = 5002;

    private static ScheduleReportTestDbContext CreateDb()
    {
        var db = ScheduleReportTestDbContext.Create();
        db.Files.AddRange(
            NewFile(ReportFileId, "PEMS_Schedule_Report_VR-10_20260801_1430.pdf", "application/pdf"),
            NewFile(PhotoFileId, "anh-noi-bo.jpg", "image/jpeg"));
        db.SaveChanges();
        return db;
    }

    private static UploadedFile NewFile(ulong fileId, string name, string mime) => new()
    {
        FileId = fileId,
        StorageProvider = "LOCAL",
        ObjectKey = $"objects/{fileId}",
        OriginalFilename = name,
        MimeType = mime,
        UploadedAt = new System.DateTime(2026, 8, 1),
    };

    private static List<(ulong, EmailAttachmentType, string?, string?)> TwoAttachments() => new()
    {
        (ReportFileId, EmailAttachmentType.ATTACHMENT, null, "PEMS_Schedule_Report_VR-10_20260801_1430.pdf"),
        (PhotoFileId, EmailAttachmentType.ATTACHMENT, null, "anh-noi-bo.jpg"),
    };

    [Fact]
    public async Task An_unreadable_file_leaves_a_hole_rather_than_shifting_the_ones_after_it()
    {
        using var db = CreateDb();
        var storage = new StubFileStorage();
        storage.Unreadable.Add(ReportFileId);      // slot 0 is gone

        var loaded = await EmailAttachmentLoader.LoadAlignedAsync(db, storage, TwoAttachments(), default);

        Assert.Equal(2, loaded.Count);
        Assert.Null(loaded[0]);
        // The surviving file keeps its OWN name. Compacting would have moved it into slot 0 and let a
        // positional caller label this photo as the schedule report.
        Assert.Equal("anh-noi-bo.jpg", loaded[1]!.FileName);
    }

    [Fact]
    public async Task Every_input_gets_exactly_one_slot_in_input_order()
    {
        using var db = CreateDb();

        var loaded = await EmailAttachmentLoader.LoadAlignedAsync(db, new StubFileStorage(), TwoAttachments(), default);

        Assert.Equal(
            new[] { "PEMS_Schedule_Report_VR-10_20260801_1430.pdf", "anh-noi-bo.jpg" },
            loaded.Select(a => a!.FileName).ToArray());
    }

    [Fact]
    public async Task A_file_id_with_no_files_row_also_leaves_a_hole()
    {
        using var db = CreateDb();
        var attachments = TwoAttachments();
        attachments[0] = (999_999, EmailAttachmentType.ATTACHMENT, null, "khong-ton-tai.pdf");

        var loaded = await EmailAttachmentLoader.LoadAlignedAsync(db, new StubFileStorage(), attachments, default);

        Assert.Equal(2, loaded.Count);
        Assert.Null(loaded[0]);
        Assert.Equal("anh-noi-bo.jpg", loaded[1]!.FileName);
    }

    [Fact]
    public async Task The_compacted_overload_still_returns_only_what_loaded()
    {
        using var db = CreateDb();
        var storage = new StubFileStorage();
        storage.Unreadable.Add(ReportFileId);

        var loaded = await EmailAttachmentLoader.LoadAsync(db, storage, TwoAttachments(), default);

        Assert.Equal("anh-noi-bo.jpg", Assert.Single(loaded).FileName);
    }
}
