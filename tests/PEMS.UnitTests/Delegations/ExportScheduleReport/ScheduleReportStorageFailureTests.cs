using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Translation;
using PEMS.Domain.Entities.Documents;
using Xunit;

namespace PEMS.UnitTests.Delegations.ExportScheduleReport;

/// <summary>
/// What happens to the Schedule Report when a file it references cannot be read.
///
/// <para>
/// Written after a live failure on visit process 3107: the report refused to build at all, with
/// "Không tìm thấy tệp đính kèm trên Google Drive". Nothing was wrong with the report or with any
/// attachment — the delegation's partner carried a <c>logo_file_id</c> whose <c>external_file_id</c>
/// was a seed placeholder (<c>drv-logo-seoultech</c>) that Drive has never heard of. A decorative image
/// was taking down the whole document, and the message named the wrong thing entirely.
/// </para>
/// <para>
/// The rule these tests hold in place: a file the report merely DECORATES with is best-effort, a file
/// the report IS must be real, and the two must fail differently.
/// </para>
/// </summary>
public class ScheduleReportStorageFailureTests
{
    private const ulong PartnerId = 77;
    private const ulong LogoFileId = 900;

    private static (ScheduleReportTestDbContext Db, StubGoogleDriveStorage Drive, ScheduleReportArtifactService Service)
        CreateSut(string logoProvider = "GOOGLE_DRIVE", string? externalFileId = "drv-logo-seoultech")
    {
        var db = ScheduleReportTestDbContext.Create();

        var logo = ScheduleReportTestData.CreateFile(LogoFileId, logoProvider);
        logo.ExternalFileId = externalFileId;
        db.Files.Add(logo);
        db.Partners.Add(ScheduleReportTestData.CreatePartner(PartnerId, "SeoulTech", LogoFileId));
        db.SaveChanges();

        ScheduleReportTestData.SeedBase(db, partnerId: PartnerId);

        var currentUser = new FakeScheduleReportCurrentUser();
        var drive = new StubGoogleDriveStorage();
        var service = new ScheduleReportArtifactService(
            db,
            new VisitFormReadService(db, currentUser, NullLogger<VisitFormReadService>.Instance),
            new StubFileStorage(),
            drive,
            new Mock<IContentTranslationService>(MockBehavior.Loose).Object,
            new Mock<IFileUploadService>(MockBehavior.Loose).Object,
            new Mock<PEMS.Application.Delegations.VisitPhotos.IVisitPhotoFolderService>(MockBehavior.Loose).Object,
            NullLogger<ScheduleReportArtifactService>.Instance);

        return (db, drive, service);
    }

    private static void AssertLooksLikePdf(byte[] bytes)
    {
        Assert.True(bytes.Length > 100, "PDF bytes should not be empty/trivial.");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    // ── The partner logo is decorative: unreadable must not mean unbuildable ──

    [Fact]
    public async Task A_partner_logo_deleted_from_drive_does_not_stop_the_report_being_built()
    {
        var (db, drive, service) = CreateSut();
        drive.DownloadFailure = StubGoogleDriveStorage.Deleted();
        var instance = await db.VisitRequestCampuses.Include(i => i.VisitRequest).SingleAsync();

        var artifact = await service.RenderAsync(instance, "vi", default);

        AssertLooksLikePdf(artifact.Content);
        Assert.Equal(1, drive.DownloadCalls);   // it really did try before giving up
    }

    [Fact]
    public async Task A_partner_logo_the_credential_may_not_read_does_not_stop_the_report_being_built()
    {
        var (db, drive, service) = CreateSut();
        drive.DownloadFailure = StubGoogleDriveStorage.PermissionDenied();
        var instance = await db.VisitRequestCampuses.Include(i => i.VisitRequest).SingleAsync();

        var artifact = await service.RenderAsync(instance, "vi", default);

        AssertLooksLikePdf(artifact.Content);
        Assert.Equal(1, drive.DownloadCalls);
    }

    [Fact]
    public async Task A_drive_file_row_with_no_external_id_is_never_sent_to_drive_at_all()
    {
        // The broken record case: storage_provider says GOOGLE_DRIVE but the row addresses nothing.
        // Asking Drive about it would produce a 404 that reads like a deleted file and send whoever is
        // investigating to look for a file that was never uploaded.
        var (db, drive, service) = CreateSut(externalFileId: null);
        var instance = await db.VisitRequestCampuses.Include(i => i.VisitRequest).SingleAsync();

        var artifact = await service.RenderAsync(instance, "vi", default);

        AssertLooksLikePdf(artifact.Content);
        Assert.Equal(0, drive.DownloadCalls);
    }

    [Fact]
    public async Task A_partner_logo_whose_files_row_is_gone_does_not_stop_the_report_being_built()
    {
        var (db, _, service) = CreateSut();
        db.Files.RemoveRange(db.Files.ToList());
        await db.SaveChangesAsync();
        var instance = await db.VisitRequestCampuses.Include(i => i.VisitRequest).SingleAsync();

        var artifact = await service.RenderAsync(instance, "vi", default);

        AssertLooksLikePdf(artifact.Content);
    }

    [Fact]
    public async Task A_readable_partner_logo_is_still_fetched_and_used()
    {
        // The negative tests above would all pass just as well against a service that never looked at the
        // logo, so one case has to prove the fetch still happens on the happy path.
        var (db, drive, service) = CreateSut();
        var instance = await db.VisitRequestCampuses.Include(i => i.VisitRequest).SingleAsync();

        var artifact = await service.RenderAsync(instance, "vi", default);

        AssertLooksLikePdf(artifact.Content);
        Assert.Equal(1, drive.DownloadCalls);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed_as_a_missing_logo()
    {
        var (db, drive, service) = CreateSut();
        drive.DownloadFailure = null;
        var instance = await db.VisitRequestCampuses.Include(i => i.VisitRequest).SingleAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RenderAsync(instance, "vi", cts.Token));
    }

    // ── The archived report IS the document: no row without stored bytes ─────

    [Fact]
    public async Task StoreAsync_writes_no_documents_row_when_the_upload_fails()
    {
        var (db, _, _) = CreateSut();
        var instance = await db.VisitRequestCampuses.Include(i => i.VisitRequest).SingleAsync();
        db.Campuses.Single().CampusCode = "HN";
        await db.SaveChangesAsync();

        var upload = new Mock<IFileUploadService>(MockBehavior.Loose);
        upload
            .Setup(u => u.UploadBusinessFileAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<FilePurpose>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException(
                "Không thể tải tệp lên Google Drive.", "GOOGLE_DRIVE_UPLOAD_FAILED"));

        var folders = new Mock<PEMS.Application.Delegations.VisitPhotos.IVisitPhotoFolderService>(MockBehavior.Loose);
        folders
            .Setup(f => f.EnsureDocumentUploadTargetAsync(
                It.IsAny<PEMS.Domain.Entities.Delegations.VisitRequestCampus>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PEMS.Application.Delegations.VisitPhotos.VisitDocumentUploadTarget
            {
                DocumentFolderExternalId = "folder-external-id",
            });

        var service = new ScheduleReportArtifactService(
            db,
            new VisitFormReadService(db, new FakeScheduleReportCurrentUser(), NullLogger<VisitFormReadService>.Instance),
            new StubFileStorage(), new StubGoogleDriveStorage(),
            new Mock<IContentTranslationService>(MockBehavior.Loose).Object,
            upload.Object, folders.Object,
            NullLogger<ScheduleReportArtifactService>.Instance);

        var artifact = new ScheduleReportArtifact(
            new byte[] { 1, 2, 3 }, "PEMS_Schedule_Report_VR-10_20260801_1430.pdf", "vi",
            new DateTime(2026, 8, 1, 14, 30, 0));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.StoreAsync(artifact, instance, ScheduleReportTestData.HostUserId, default));

        // The point: no pointer to bytes that were never stored. A documents row written first would
        // survive this failure and show a Schedule Report that nobody can ever open.
        Assert.Empty(await db.Documents.ToListAsync());
    }

    // ── The probe itself ────────────────────────────────────────────────────

    [Theory]
    [InlineData(StorageErrorCodes.FileNotFound, StoredFileAvailability.NotFound)]
    [InlineData(StorageErrorCodes.FileForbidden, StoredFileAvailability.Forbidden)]
    public async Task The_probe_reports_the_cause_drive_gave(string code, StoredFileAvailability expected)
    {
        var (db, drive, _) = CreateSut();
        drive.DownloadFailure = new BusinessRuleException("nope", code);

        var result = await StoredFileProbe.ProbeAsync(db, new StubFileStorage(), drive, LogoFileId, default);

        Assert.False(result.IsAvailable);
        Assert.Equal(expected, result.Availability);
        Assert.Equal(code, result.ErrorCode);
    }

    [Fact]
    public async Task The_probe_calls_a_drive_row_with_no_external_id_a_broken_reference()
    {
        var (db, drive, _) = CreateSut(externalFileId: null);

        var result = await StoredFileProbe.ProbeAsync(db, new StubFileStorage(), drive, LogoFileId, default);

        Assert.Equal(StoredFileAvailability.ReferenceInvalid, result.Availability);
        Assert.Equal(StorageErrorCodes.FileReferenceInvalid, result.ErrorCode);
        Assert.Equal(0, drive.DownloadCalls);
    }

    [Fact]
    public async Task The_probe_calls_a_missing_files_row_a_broken_reference()
    {
        var (db, drive, _) = CreateSut();

        var result = await StoredFileProbe.ProbeAsync(db, new StubFileStorage(), drive, 999_999, default);

        Assert.Equal(StoredFileAvailability.ReferenceInvalid, result.Availability);
        Assert.Equal(0, drive.DownloadCalls);
    }

    [Fact]
    public async Task The_probe_reports_a_local_file_whose_bytes_are_gone_as_not_found()
    {
        var (db, drive, _) = CreateSut(logoProvider: "LOCAL", externalFileId: null);
        var storage = new StubFileStorage();
        storage.Unreadable.Add(LogoFileId);

        var result = await StoredFileProbe.ProbeAsync(db, storage, drive, LogoFileId, default);

        Assert.Equal(StoredFileAvailability.NotFound, result.Availability);
        Assert.Equal(StorageErrorCodes.FileNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task The_probe_says_available_when_the_bytes_can_be_read()
    {
        var (db, drive, _) = CreateSut();

        var result = await StoredFileProbe.ProbeAsync(db, new StubFileStorage(), drive, LogoFileId, default);

        Assert.True(result.IsAvailable);
        Assert.Null(result.ErrorCode);
    }
}
