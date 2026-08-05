using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Delegations.SetupProgressEmail;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;
using PEMS.Shared;
using PEMS.UnitTests.Delegations.ExportScheduleReport;
using Xunit;

namespace PEMS.UnitTests.Delegations.SetupProgressEmail;

/// <summary>
/// Prepare → refresh → send, over the real handlers, with nothing persisted in between.
///
/// <para>
/// This is <c>VisitSetupProgressEmailFlowTests</c> after the draft row was removed. Every guard it
/// covered is still covered: who may prepare, who may refresh, who may send, which visit states allow
/// it, and the four ways an attached Schedule Report can turn out to be unreadable between composing
/// and sending. What is gone is the coverage that was ABOUT the row — reopening the same draft,
/// creating a second one, refusing a draft id belonging to another instance, refusing a draft someone
/// else owns. Those questions no longer have a subject: the composer holds the message in the browser,
/// so there is no server-side draft to reopen, to guess the id of, or to own.
/// </para>
/// <para>
/// The important consequence is that the send guards had to get STRICTER, not weaker, and that is what
/// the send section below pins down. When the server owned the draft it could trust the attachment
/// list it had written; now the client sends the attachment list, so the report is re-identified from
/// <c>documents</c> on every send. A file the Host uploaded and named to look like a report, or a
/// genuine report belonging to a different delegation, is refused here — the two cases that replace
/// "an ordinary draft cannot be pushed through this route".
/// </para>
/// <para>
/// The report artifact service is the one thing stubbed: rendering a genuine PDF and pushing it
/// through Drive is covered by its own tests, and doing it here would make every case in this file
/// depend on file storage to assert something about authorisation. The MIME envelope and the recorded
/// history row are covered against MySQL in the integration suite.
/// </para>
/// </summary>
public class VisitSetupProgressDirectSendFlowTests
{
    private const ulong Instance = ScheduleReportTestData.VisitInstanceId;
    private const ulong Request = ScheduleReportTestData.VisitRequestId;
    private const ulong Host = ScheduleReportTestData.HostUserId;
    private const ulong TemplateId = 70031;

    // ── Prepare ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Prepare returns the message; it does not save one. The report IS archived — it is a real PDF a
    /// reader has to be able to dereference later — but the subject, body and recipients come back to
    /// the composer and stop there.
    /// </summary>
    [Fact]
    public async Task The_host_gets_a_rendered_message_and_an_archived_report_and_nothing_is_saved()
    {
        var sut = CreateSut();

        var result = await PrepareAsync(sut);

        Assert.Contains("Đoàn khách kiểm thử", result.Subject);
        Assert.NotEmpty(result.BodyHtml);
        Assert.Contains("PEMS_Schedule_Report_", result.ReportFileName);

        // The archived report is a real, readable file — the composer marks it undeletable and the send
        // re-identifies it from documents.
        Assert.True(await sut.Db.Files.AnyAsync(f => f.FileId == result.ReportFileId));
        var document = await sut.Db.Documents.SingleAsync(d => d.FileId == result.ReportFileId);
        Assert.Equal(SetupProgressReport.ReportDocumentCategory, document.DocumentCategory);
        Assert.Equal(Request, document.OwnerId);
    }

    [Fact]
    public async Task The_default_envelope_puts_the_guest_in_to_and_accepted_participants_in_cc()
    {
        var sut = CreateSut();
        sut.Db.Users.Add(ScheduleReportTestData.CreateUser(410, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null));
        sut.Db.VisitParticipants.Add(ScheduleReportTestData.CreateParticipant(
            1, 410, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted));
        sut.Db.SaveChanges();

        var result = await PrepareAsync(sut);

        Assert.Equal(
            new[] { "contact@test.local", "guest@test.local" },
            result.Recipients.Where(r => r.RecipientType == "TO")
                .Select(r => r.Email).OrderBy(e => e).ToArray());
        Assert.Equal(
            new[] { "user410@test.local" },
            result.Recipients.Where(r => r.RecipientType == "CC").Select(r => r.Email).ToArray());
        // A blind copy on a message to a guest would be an internal address the guest cannot see and
        // did not consent to. There is never one here.
        Assert.DoesNotContain(result.Recipients, r => r.RecipientType == "BCC");
    }

    [Fact]
    public async Task The_message_and_the_report_are_produced_in_one_language()
    {
        var sut = CreateSut();

        var result = await PrepareAsync(sut, "en");

        Assert.Equal("en", result.LanguageCode);
        Assert.Equal("en", sut.Renderer.LastLanguage);
        Assert.Equal("en", sut.Reports.LastLanguage);
        var vars = sut.Renderer.LastVariables!;
        Assert.Equal("Đoàn khách kiểm thử", vars["delegationName"]);
        Assert.Equal("09:00 01/08/2026", vars["plannedStart"]);
        Assert.Equal("11:00 01/08/2026", vars["plannedEnd"]);
    }

    // ── The guard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Someone_who_is_not_the_current_host_is_refused()
    {
        var sut = CreateSut();
        sut.User.UserId = 999;

        await Assert.ThrowsAsync<ForbiddenException>(() => PrepareAsync(sut));
        // Refused before anything was built: no report was rendered and none was archived.
        Assert.Equal(0, sut.Reports.RenderCount);
        Assert.Equal(0, sut.Reports.StoreCount);
    }

    [Fact]
    public async Task A_replaced_host_can_no_longer_prepare()
    {
        var sut = CreateSut();
        await PrepareAsync(sut);

        var instance = sut.Db.VisitRequestCampuses.Single();
        instance.CurrentHostUserId = 600;
        sut.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => PrepareAsync(sut));
    }

    [Fact]
    public async Task An_instance_that_does_not_belong_to_the_request_is_not_found()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.Prepare.Handle(
            new PrepareVisitSetupProgressEmailCommand(Request + 500, Instance, "vi"), default));
    }

    [Theory]
    [InlineData(VisitInstanceStatus.DuringVisit)]
    [InlineData(VisitInstanceStatus.AfterVisit)]
    [InlineData(VisitInstanceStatus.Closed)]
    public async Task A_visit_past_the_preparation_window_is_a_conflict(string status)
    {
        var sut = CreateSut(status);

        await Assert.ThrowsAsync<ConflictException>(() => PrepareAsync(sut));
    }

    [Fact]
    public async Task A_cancelled_visit_is_a_conflict()
    {
        var sut = CreateSut(VisitInstanceStatus.Cancelled);

        await Assert.ThrowsAsync<ConflictException>(() => PrepareAsync(sut));
    }

    // ── Refresh ─────────────────────────────────────────────────────────────

    /// <summary>
    /// "Đồng bộ dữ liệu mới nhất" rebuilds BOTH halves from one read. The tables in the body describe
    /// the setup, so a fresh PDF beside a stale body would make the message contradict its attachment.
    /// </summary>
    [Fact]
    public async Task Refreshing_rebuilds_the_body_and_the_report_together()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        var refreshed = await sut.Refresh.Handle(
            new RefreshVisitSetupProgressEmailCommand(Request, Instance, prepared.LanguageCode), default);

        Assert.NotEqual(prepared.ReportFileId, refreshed.ReportFileId);
        Assert.NotEmpty(refreshed.BodyHtml);
        Assert.Equal(2, sut.Reports.RenderCount);
        Assert.Equal(2, sut.Reports.StoreCount);
    }

    /// <summary>
    /// A refresh replaces the body wholesale and says so. The client must not merge it into what the
    /// Host was holding: the tables are a picture of the setup at one moment, and half-updating them
    /// would produce a body describing two different moments at once.
    /// </summary>
    [Fact]
    public async Task Refreshing_declares_that_it_replaced_the_body_rather_than_added_to_it()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        var refreshed = await sut.Refresh.Handle(
            new RefreshVisitSetupProgressEmailCommand(Request, Instance, "vi"), default);

        Assert.True(refreshed.BodyRewritten);
        Assert.NotEmpty(refreshed.BodyHtml);
        Assert.NotEmpty(refreshed.ReportGeneratedAt);
        Assert.NotEqual(prepared.ReportFileId, refreshed.ReportFileId);
    }

    [Fact]
    public async Task Refreshing_keeps_the_language_it_was_asked_for()
    {
        var sut = CreateSut();
        await PrepareAsync(sut, "en");

        var refreshed = await sut.Refresh.Handle(
            new RefreshVisitSetupProgressEmailCommand(Request, Instance, "en"), default);

        // A refresh must not swap an English attachment onto an English message with a Vietnamese one.
        Assert.Equal("en", refreshed.LanguageCode);
        Assert.Equal("en", sut.Reports.LastLanguage);
    }

    [Fact]
    public async Task Someone_who_is_not_the_host_cannot_refresh()
    {
        var sut = CreateSut();
        await PrepareAsync(sut);
        sut.User.UserId = 999;

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.Refresh.Handle(
            new RefreshVisitSetupProgressEmailCommand(Request, Instance, "vi"), default));
    }

    // ── Send ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sending_re_checks_the_visit_and_then_uses_the_shared_sender()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        var result = await SendAsync(sut, prepared);

        Assert.True(result.Success);
        var sent = Assert.Single(sut.Sender.Sent);
        // Recorded against the instance, which is what makes it findable from the visit's history.
        Assert.Equal(SetupProgressReport.RelatedType, sent.RelatedType);
        Assert.Equal(Instance, sent.RelatedId);
    }

    [Fact]
    public async Task Sending_is_refused_when_the_attached_report_can_no_longer_be_read()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        // Between composing and sending, the archived report becomes unreadable. Every row is still
        // there, so nothing structural stops the send — and the guest would otherwise receive a message
        // announcing an attachment that was silently dropped on the way out.
        sut.Storage.Unreadable.Add(prepared.ReportFileId);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => SendAsync(sut, prepared));

        Assert.Empty(sut.Sender.Sent);
        // The message has to name the cause and the way out, or the Host has no move to make.
        Assert.Contains("Đồng bộ dữ liệu mới nhất", ex.Message);
        Assert.Contains(StorageErrorCodes.FileNotFound, ex.Message);
    }

    [Fact]
    public async Task Sending_is_refused_when_the_report_row_points_at_no_real_drive_file()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        // The broken-record shape: the files row says the bytes live on Drive but names no file there.
        var file = await sut.Db.Files.SingleAsync(f => f.FileId == prepared.ReportFileId);
        file.StorageProvider = "GOOGLE_DRIVE";
        file.ExternalFileId = null;
        await sut.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => SendAsync(sut, prepared));

        Assert.Empty(sut.Sender.Sent);
        Assert.Contains(StorageErrorCodes.FileReferenceInvalid, ex.Message);
        Assert.Equal(0, sut.Drive.DownloadCalls);   // a broken record never reaches the network
    }

    [Fact]
    public async Task Sending_is_refused_when_drive_refuses_the_report_for_lack_of_permission()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        var file = await sut.Db.Files.SingleAsync(f => f.FileId == prepared.ReportFileId);
        file.StorageProvider = "GOOGLE_DRIVE";
        file.ExternalFileId = "1AbCdEfGhIjKlMnOpQrStUvWxYz012345";
        await sut.Db.SaveChangesAsync();
        sut.Drive.DownloadFailure = StubGoogleDriveStorage.PermissionDenied();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => SendAsync(sut, prepared));

        Assert.Empty(sut.Sender.Sent);
        // Told apart from a deleted file: this one is a share/scope problem an operator can fix.
        Assert.Contains(StorageErrorCodes.FileForbidden, ex.Message);
        Assert.Contains("không có quyền", ex.Message);
    }

    [Fact]
    public async Task A_host_replaced_after_composing_cannot_send()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        var instance = sut.Db.VisitRequestCampuses.Single();
        instance.CurrentHostUserId = 600;
        await sut.Db.SaveChangesAsync();

        // Being signed in is still true — which is exactly why the generic send endpoint would have
        // allowed this, and why this route re-checks the host instead of trusting it.
        await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(sut, prepared));
        Assert.Empty(sut.Sender.Sent);
    }

    [Fact]
    public async Task A_visit_that_started_after_composing_cannot_be_sent_about()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        var instance = sut.Db.VisitRequestCampuses.Single();
        instance.Status = VisitInstanceStatus.DuringVisit;
        await sut.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => SendAsync(sut, prepared));
        Assert.Empty(sut.Sender.Sent);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        sut.User.UserId = null;

        await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(sut, prepared));
        Assert.Empty(sut.Sender.Sent);
    }

    /// <summary>
    /// The body tells the guest a schedule report is attached, so a send carrying no report is refused
    /// rather than delivered bare.
    /// </summary>
    [Fact]
    public async Task A_message_that_lost_its_report_is_refused_rather_than_sent_bare()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => SendAsync(sut, prepared, attachments: new List<EmailComposeAttachmentInput>()));

        Assert.Contains("Báo cáo Lịch trình", ex.Message);
        Assert.Empty(sut.Sender.Sent);
    }

    /// <summary>
    /// The client now supplies the attachment list, so "there is a file attached" is not the question —
    /// "is that file the Schedule Report of THIS visit" is. A file the Host uploaded themselves cannot
    /// satisfy it, however it is named.
    /// </summary>
    [Fact]
    public async Task A_file_the_host_uploaded_and_named_like_a_report_does_not_satisfy_the_requirement()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        sut.Db.Files.Add(new UploadedFile
        {
            FileId = 60001,
            StorageProvider = "LOCAL",
            ObjectKey = "objects/60001",
            OriginalFilename = "PEMS_Schedule_Report_VR-1_20260801_1430.pdf",
            MimeType = "application/pdf",
            UploadedBy = Host,
            UploadedAt = new DateTime(2026, 8, 1),
        });
        await sut.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(sut, prepared,
            attachments: new List<EmailComposeAttachmentInput>
            {
                new() { FileId = 60001, DisplayName = "PEMS_Schedule_Report_VR-1_20260801_1430.pdf" },
            }));

        Assert.Empty(sut.Sender.Sent);
    }

    /// <summary>
    /// A genuine Schedule Report, archived by this very code — but for a different delegation. It is
    /// refused, because the report is matched on the visit it belongs to and not merely on its category.
    /// </summary>
    [Fact]
    public async Task A_genuine_report_belonging_to_another_delegation_does_not_satisfy_the_requirement()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        sut.Db.Files.Add(new UploadedFile
        {
            FileId = 60002,
            StorageProvider = "LOCAL",
            ObjectKey = "objects/60002",
            OriginalFilename = "PEMS_Schedule_Report_VR-999_20260801_1430.pdf",
            MimeType = "application/pdf",
            UploadedBy = Host,
            UploadedAt = new DateTime(2026, 8, 1),
        });
        sut.Db.Documents.Add(new Document
        {
            FileId = 60002,
            OwnerType = "VISIT",
            OwnerId = Request + 999,                   // another delegation entirely
            Title = "PEMS_Schedule_Report_VR-999_20260801_1430.pdf",
            DocumentCategory = SetupProgressReport.ReportDocumentCategory,
            Status = "PUBLISHED",
            CreatedAt = new DateTime(2026, 8, 1),
            CreatedBy = Host,
        });
        await sut.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() => SendAsync(sut, prepared,
            attachments: new List<EmailComposeAttachmentInput> { new() { FileId = 60002 } }));

        Assert.Empty(sut.Sender.Sent);
    }

    // ── Fixture ─────────────────────────────────────────────────────────────

    private sealed class Sut
    {
        public required ScheduleReportTestDbContext Db { get; init; }
        public required FakeScheduleReportCurrentUser User { get; init; }
        public required StubReports Reports { get; init; }
        public required StubRenderer Renderer { get; init; }

        /// <summary>Storage the handlers probe through — a test makes the report unreadable here.</summary>
        public required StubFileStorage Storage { get; init; }
        public required StubGoogleDriveStorage Drive { get; init; }
        public required RecordingDirectEmailSender Sender { get; init; }
        public required PrepareVisitSetupProgressEmailCommandHandler Prepare { get; init; }
        public required RefreshVisitSetupProgressEmailCommandHandler Refresh { get; init; }
        public required SendVisitSetupProgressEmailCommandHandler Send { get; init; }
    }

    private static Sut CreateSut(
        string instanceStatus = VisitInstanceStatus.BeforeVisit,
        string requestStatus = "APPROVED")
    {
        var db = ScheduleReportTestDbContext.Create();
        ScheduleReportTestData.SeedBase(db, instanceStatus);

        var visit = db.VisitRequests.Single();
        visit.Status = requestStatus;
        db.EmailTemplates.Add(new EmailTemplate
        {
            EmailTemplateId = TemplateId,
            TemplateCode = SystemEmailTemplates.VisitSetupProgressUpdate,
            Name = "Cập nhật công tác chuẩn bị tiếp khách",
            Purpose = EmailTemplatePurposes.Report,
            Status = "ACTIVE",
            SubjectVi = "vi", BodyVi = "vi", SubjectEn = "en", BodyEn = "en",
            BodyFormat = EmailBodyFormat.HTML,
            CreatedAt = new DateTime(2026, 1, 1),
        });
        db.SaveChanges();

        var user = new FakeScheduleReportCurrentUser { UserId = Host };
        var reports = new StubReports { Db = db };
        var renderer = new StubRenderer();
        var formRead = new VisitFormReadService(db, user, NullLogger<VisitFormReadService>.Instance);
        var recipients = new VisitSetupProgressRecipientResolver(db);
        var storage = new StubFileStorage();
        var drive = new StubGoogleDriveStorage();
        var sender = new RecordingDirectEmailSender();
        var composer = new VisitSetupProgressComposer(
            db, renderer, reports, formRead,
            new PEMS.UnitTests.TestInfrastructure.StubEmailSenderVariableResolver());

        return new Sut
        {
            Db = db,
            User = user,
            Reports = reports,
            Renderer = renderer,
            Storage = storage,
            Drive = drive,
            Sender = sender,
            Prepare = new PrepareVisitSetupProgressEmailCommandHandler(db, user, composer, recipients),
            Refresh = new RefreshVisitSetupProgressEmailCommandHandler(db, user, composer),
            Send = new SendVisitSetupProgressEmailCommandHandler(db, user, sender, storage, drive),
        };
    }

    private static Task<PrepareVisitSetupProgressEmailResponse> PrepareAsync(Sut sut, string language = "vi")
        => sut.Prepare.Handle(new PrepareVisitSetupProgressEmailCommand(Request, Instance, language), default);

    /// <summary>
    /// Sends the message the composer would be holding: the prepared subject, body, envelope and the
    /// mandatory report, exactly as the browser posts them.
    /// </summary>
    private static Task<SendVisitSetupProgressEmailResponse> SendAsync(
        Sut sut,
        PrepareVisitSetupProgressEmailResponse prepared,
        List<EmailComposeAttachmentInput>? attachments = null)
        => sut.Send.Handle(new SendVisitSetupProgressEmailCommand
        {
            VisitRequestId = Request,
            VisitInstanceId = Instance,
            Subject = prepared.Subject,
            BodyHtml = prepared.BodyHtml,
            LanguageCode = prepared.LanguageCode,
            Recipients = prepared.Recipients
                .Select(r => new EmailComposeRecipientInput
                {
                    Email = r.Email, Name = r.Name, RecipientType = r.RecipientType,
                })
                .ToList(),
            Attachments = attachments ?? new List<EmailComposeAttachmentInput>
            {
                new() { FileId = prepared.ReportFileId, DisplayName = prepared.ReportFileName },
            },
        }, default);

    /// <summary>
    /// Stands in for <see cref="DirectEmailSender"/>. The rules it applies have their own suite; what
    /// matters here is whether the handler reached it at all, and with what.
    /// </summary>
    private sealed class RecordingDirectEmailSender : IDirectEmailSender
    {
        public List<DirectEmailRequest> Sent { get; } = new();

        public Task<DirectEmailPreview> PreviewAsync(
            DirectEmailRequest request, ulong actorUserId, CancellationToken cancellationToken)
            => Task.FromResult(new DirectEmailPreview(
                request.Subject ?? string.Empty, request.BodyContent ?? string.Empty, true,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));

        public Task<ManualEmailResult> SendAsync(
            DirectEmailRequest request, ulong actorUserId, CancellationToken cancellationToken)
        {
            Sent.Add(request);
            return Task.FromResult(new ManualEmailResult(
                SentEmailId: 4242, Status: "SENT", Success: true, Message: "Đã gửi."));
        }
    }

    private sealed class StubReports : IScheduleReportArtifactService
    {
        private ulong _nextFileId = 5000;
        public int RenderCount { get; private set; }
        public int StoreCount { get; private set; }
        public string? LastLanguage { get; private set; }

        public Task<ScheduleReportArtifact> RenderAsync(
            VisitRequestCampus instance, string? languageCode, CancellationToken ct)
        {
            RenderCount++;
            LastLanguage = string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
            var suffix = LastLanguage == "en" ? "_EN" : "";
            // Data must be carried: it is what the body's HTML tables are rendered from, and the
            // snapshot builder refuses an artifact without it rather than issuing a second read.
            return Task.FromResult(new ScheduleReportArtifact(
                new byte[] { 1, 2, 3 },
                $"PEMS_Schedule_Report_VR-{instance.VisitRequestId}_20260801_1430{suffix}.pdf",
                LastLanguage,
                new DateTime(2026, 8, 1, 14, 30, 0))
            {
                Data = Report ?? new ScheduleReportDto
                {
                    DelegationName = "Đoàn khách kiểm thử",
                    PlannedStartAt = instance.PlannedStartAt,
                    PlannedEndAt = instance.PlannedEndAt,
                },
            });
        }

        /// <summary>Lets a test drive the exact report content the HTML tables are built from.</summary>
        public ScheduleReportDto? Report { get; set; }

        public ScheduleReportTestDbContext? Db { get; set; }

        public async Task<ulong> StoreAsync(
            ScheduleReportArtifact artifact, VisitRequestCampus instance, ulong actorUserId, CancellationToken ct)
        {
            StoreCount++;
            var fileId = ++_nextFileId;

            // The real service uploads the bytes and inserts a files row BEFORE recording the document,
            // and both halves matter: the documents row identifies the mandatory attachment, and the
            // files row is what a reader dereferences to get the PDF. A stub that wrote only the
            // document would model precisely the broken state these handlers exist to detect.
            Db!.Files.Add(new UploadedFile
            {
                FileId = fileId,
                StorageProvider = "LOCAL",
                ObjectKey = $"reports/{artifact.FileName}",
                OriginalFilename = artifact.FileName,
                MimeType = "application/pdf",
                FileSize = artifact.Content.LongLength,
                UploadedBy = actorUserId,
                UploadedAt = artifact.GeneratedAt,
            });

            Db.Documents.Add(new Document
            {
                FileId = fileId,
                OwnerType = "VISIT",
                OwnerId = instance.VisitRequestId,
                CampusId = instance.CampusId,
                Title = artifact.FileName,
                DocumentCategory = SetupProgressReport.ReportDocumentCategory,
                Status = "PUBLISHED",
                CreatedAt = artifact.GeneratedAt,
                CreatedBy = actorUserId,
            });
            await Db.SaveChangesAsync(ct);
            return fileId;
        }
    }

    /// <summary>Renders from the seeded template row, exactly as the production renderer's contract says.</summary>
    private sealed class StubRenderer : IEmailTemplateRenderer
    {
        public IReadOnlyDictionary<string, string>? LastVariables { get; private set; }
        public string? LastLanguage { get; private set; }
        public string? LastSetupBlock { get; private set; }
        public int Calls { get; private set; }

        public Task<EmailRenderResult> RenderAsync(EmailRenderRequest request, CancellationToken ct = default)
        {
            Calls++;
            LastVariables = request.Variables;
            LastLanguage = request.Language;
            LastSetupBlock = request.TrustedHtmlBlocks is { } b
                && b.TryGetValue(EmailTrustedBlocks.SetupSummaryBlock, out var html) ? html : null;

            // Substitutes the block the way the real renderer does, so a test can assert on the body
            // the composer actually ends up holding.
            return Task.FromResult(new EmailRenderResult(
                TemplateId, request.TemplateCode,
                $"[PEMS] Cập nhật công tác chuẩn bị — {request.Variables["delegationName"]}",
                $"<p>noi dung mac dinh</p>{LastSetupBlock}", EmailBodyFormat.HTML, request.Language));
        }
    }
}
