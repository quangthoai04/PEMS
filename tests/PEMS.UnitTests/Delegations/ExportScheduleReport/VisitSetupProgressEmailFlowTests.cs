using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common;
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
/// Prepare → refresh → send, over the real handlers.
///
/// <para>
/// The report artifact service is the one thing stubbed: rendering a genuine PDF and pushing it through
/// Drive is covered by its own tests, and doing it here would make every case in this file depend on
/// file storage to assert something about authorisation. Everything else — the guard, the draft rows,
/// the recipients, which attachment is the mandatory one — is the production code.
/// </para>
/// <para>
/// Two things are deliberately NOT covered here and are covered against MySQL instead: the atomic
/// DRAFT → SENT claim (a raw conditional UPDATE that EF InMemory cannot execute) and the MIME envelope,
/// both of which live in the shared dispatcher and are exercised by ManualEmailPipelineTests.
/// </para>
/// </summary>
public class VisitSetupProgressEmailFlowTests
{
    private const ulong Instance = ScheduleReportTestData.VisitInstanceId;
    private const ulong Request = ScheduleReportTestData.VisitRequestId;
    private const ulong Host = ScheduleReportTestData.HostUserId;
    private const ulong TemplateId = 70031;

    // ── Fixture ─────────────────────────────────────────────────────────────

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
            // and both halves matter to the flow now: the documents row identifies the mandatory
            // attachment, and the files row is what a reader dereferences to get the PDF. A stub that
            // wrote only the document would model precisely the broken state these handlers exist to
            // detect, and every reuse test would then exercise the regenerate path by accident.
            Db!.Files.Add(new PEMS.Domain.Entities.Documents.UploadedFile
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

            // The real service archives the report as a documents row, and that row is what identifies
            // the mandatory attachment — so the stub has to write it too or the lookup is untested.
            Db.Documents.Add(new Document
            {
                FileId = fileId,
                OwnerType = "VISIT",
                OwnerId = instance.VisitRequestId,
                CampusId = instance.CampusId,
                Title = artifact.FileName,
                DocumentCategory = SetupProgressDrafts.ReportDocumentCategory,
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
            request.TrustedHtmlBlocks?.TryGetValue(EmailTrustedBlocks.SetupSummaryBlock, out var block);
            LastSetupBlock = request.TrustedHtmlBlocks is { } b
                && b.TryGetValue(EmailTrustedBlocks.SetupSummaryBlock, out var html) ? html : null;

            // Substitutes the block the way the real renderer does, so a test can assert on the body
            // the draft actually ends up holding.
            return Task.FromResult(new EmailRenderResult(
                TemplateId, request.TemplateCode,
                $"[PEMS] Cập nhật công tác chuẩn bị — {request.Variables["delegationName"]}",
                $"<p>noi dung mac dinh</p>{LastSetupBlock}", EmailBodyFormat.HTML, request.Language));
        }
    }

    private sealed class Sut
    {
        public required ScheduleReportTestDbContext Db { get; init; }
        public required FakeScheduleReportCurrentUser User { get; init; }
        public required StubReports Reports { get; init; }
        public required StubRenderer Renderer { get; init; }

        /// <summary>Storage the handlers probe through — a test makes the report unreadable here.</summary>
        public required StubFileStorage Storage { get; init; }
        public required StubGoogleDriveStorage Drive { get; init; }
        public required PrepareVisitSetupProgressEmailDraftCommandHandler Prepare { get; init; }
        public required RefreshVisitSetupProgressEmailReportCommandHandler Refresh { get; init; }
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

        return new Sut
        {
            Db = db,
            User = user,
            Reports = reports,
            Renderer = renderer,
            Storage = storage,
            Drive = drive,
            Prepare = new PrepareVisitSetupProgressEmailDraftCommandHandler(
                db, user, renderer, recipients, reports, formRead, storage, drive,
                new PEMS.UnitTests.TestInfrastructure.StubEmailContactResolver(),
                NullLogger<PrepareVisitSetupProgressEmailDraftCommandHandler>.Instance),
            Refresh = new RefreshVisitSetupProgressEmailReportCommandHandler(
                db, user, reports, renderer, formRead,
                new PEMS.UnitTests.TestInfrastructure.StubEmailContactResolver()),
        };
    }

    private static Task<PrepareVisitSetupProgressEmailDraftResponse> PrepareAsync(
        Sut sut, string language = "vi", bool reuse = true)
        => sut.Prepare.Handle(
            new PrepareVisitSetupProgressEmailDraftCommand(Request, Instance, language, reuse), default);

    private static SendVisitSetupProgressEmailDraftCommandHandler Send(Sut sut, IEmailDraftDispatcher dispatcher)
        => new(sut.Db, sut.User, dispatcher, sut.Storage, sut.Drive);

    private sealed class RecordingDispatcher : IEmailDraftDispatcher
    {
        public int Calls { get; private set; }
        public Task<EmailDraftDispatchResult> DispatchAsync(EmailDraft draft, ulong actorUserId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new EmailDraftDispatchResult(draft.EmailDraftId, 1, "SENT", true, "SENT", "ok"));
        }
    }

    // ── Prepare ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_host_gets_a_draft_bound_to_this_instance_with_the_report_attached()
    {
        var sut = CreateSut();

        var result = await PrepareAsync(sut);

        var draft = await sut.Db.EmailDrafts.SingleAsync();
        Assert.Equal(draft.EmailDraftId, result.DraftId);
        Assert.Equal(TemplateId, draft.EmailTemplateId);
        Assert.Equal("VISIT_INSTANCE", draft.RelatedType);
        Assert.Equal(Instance, draft.RelatedId);
        Assert.Equal(Host, draft.CreatedBy);
        Assert.Equal(EmailDraftStatus.DRAFT, draft.Status);

        // Exactly one attachment, and it is the archived report.
        var attachment = await sut.Db.EmailDraftAttachments.SingleAsync();
        Assert.Equal(result.ReportFileId, attachment.FileId);
        Assert.Contains("PEMS_Schedule_Report_", result.ReportFileName);
        Assert.False(result.ReusedExistingDraft);
    }

    [Fact]
    public async Task The_default_envelope_puts_the_guest_in_to_and_accepted_participants_in_cc()
    {
        var sut = CreateSut();
        sut.Db.Users.Add(ScheduleReportTestData.CreateUser(410, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null));
        sut.Db.VisitParticipants.Add(ScheduleReportTestData.CreateParticipant(
            1, 410, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted));
        sut.Db.SaveChanges();

        await PrepareAsync(sut);

        var rows = await sut.Db.EmailDraftRecipients.OrderBy(r => r.DisplayOrder).ToListAsync();
        Assert.Equal(
            new[] { "contact@test.local", "guest@test.local" },
            rows.Where(r => r.RecipientType == "TO").Select(r => r.RecipientEmail).OrderBy(e => e).ToArray());
        Assert.Equal(
            new[] { "user410@test.local" },
            rows.Where(r => r.RecipientType == "CC").Select(r => r.RecipientEmail).ToArray());
        Assert.Empty(rows.Where(r => r.RecipientType == "BCC"));
    }

    [Fact]
    public async Task The_template_variables_describe_this_instance()
    {
        var sut = CreateSut();

        await PrepareAsync(sut, "en");

        Assert.Equal("en", sut.Renderer.LastLanguage);
        Assert.Equal("en", sut.Reports.LastLanguage);   // message and report share one language
        var vars = sut.Renderer.LastVariables!;
        Assert.Equal("Đoàn khách kiểm thử", vars["delegationName"]);
        Assert.Equal("09:00 01/08/2026", vars["plannedStart"]);
        Assert.Equal("11:00 01/08/2026", vars["plannedEnd"]);
    }

    [Fact]
    public async Task Preparing_twice_reopens_the_same_draft_instead_of_piling_up_files()
    {
        var sut = CreateSut();
        var first = await PrepareAsync(sut);

        var second = await PrepareAsync(sut);

        Assert.True(second.ReusedExistingDraft);
        Assert.Equal(first.DraftId, second.DraftId);
        Assert.Equal(1, await sut.Db.EmailDrafts.CountAsync());
        // The report is the expensive half: re-opening must not archive another copy of it.
        Assert.Equal(1, sut.Reports.StoreCount);
        Assert.NotEmpty(second.Warnings);
    }

    [Fact]
    public async Task A_draft_whose_report_vanished_from_storage_is_rebuilt_rather_than_reopened()
    {
        var sut = CreateSut();
        var first = await PrepareAsync(sut);

        // The row survives; the bytes do not. Exactly what a purged Drive file — or one whose id was
        // never real — looks like to this handler.
        sut.Storage.Unreadable.Add(first.ReportFileId);

        var second = await PrepareAsync(sut);

        Assert.False(second.ReusedExistingDraft);
        Assert.NotEqual(first.ReportFileId, second.ReportFileId);
        // Rebuilt from the setup as it stands now: a second render AND a second archive.
        Assert.Equal(2, sut.Reports.RenderCount);
        Assert.Equal(2, sut.Reports.StoreCount);
        // And the new draft carries a report that can actually be read.
        var attachments = await sut.Db.EmailDraftAttachments
            .Where(a => a.EmailDraftId == second.DraftId).ToListAsync();
        Assert.Equal(second.ReportFileId, Assert.Single(attachments).FileId);
    }

    [Fact]
    public async Task A_draft_whose_report_points_at_no_real_drive_file_is_also_rebuilt()
    {
        var sut = CreateSut();
        var first = await PrepareAsync(sut);

        // The shape seeded databases are full of: storage_provider GOOGLE_DRIVE, external_file_id that
        // addresses nothing. Structurally the draft is intact, so only a probe can tell.
        var file = await sut.Db.Files.SingleAsync(f => f.FileId == first.ReportFileId);
        file.StorageProvider = "GOOGLE_DRIVE";
        file.ExternalFileId = null;
        await sut.Db.SaveChangesAsync();

        var second = await PrepareAsync(sut);

        Assert.False(second.ReusedExistingDraft);
        Assert.Equal(2, sut.Reports.StoreCount);
    }

    [Fact]
    public async Task A_draft_whose_report_drive_refuses_to_serve_is_also_rebuilt()
    {
        var sut = CreateSut();
        var first = await PrepareAsync(sut);

        var file = await sut.Db.Files.SingleAsync(f => f.FileId == first.ReportFileId);
        file.StorageProvider = "GOOGLE_DRIVE";
        file.ExternalFileId = "1AbCdEfGhIjKlMnOpQrStUvWxYz012345";
        await sut.Db.SaveChangesAsync();
        sut.Drive.DownloadFailure = StubGoogleDriveStorage.Deleted();

        var second = await PrepareAsync(sut);

        Assert.False(second.ReusedExistingDraft);
        Assert.Equal(2, sut.Reports.StoreCount);
    }

    [Fact]
    public async Task Asking_for_a_new_update_creates_a_second_draft()
    {
        var sut = CreateSut();
        var first = await PrepareAsync(sut);

        var second = await PrepareAsync(sut, reuse: false);

        Assert.False(second.ReusedExistingDraft);
        Assert.NotEqual(first.DraftId, second.DraftId);
        Assert.Equal(2, await sut.Db.EmailDrafts.CountAsync());
    }

    // ── The guard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Someone_who_is_not_the_current_host_is_refused()
    {
        var sut = CreateSut();
        sut.User.UserId = 999;

        await Assert.ThrowsAsync<ForbiddenException>(() => PrepareAsync(sut));
        Assert.Equal(0, await sut.Db.EmailDrafts.CountAsync());
    }

    [Fact]
    public async Task A_replaced_host_can_no_longer_prepare_even_holding_the_old_draft()
    {
        var sut = CreateSut();
        await PrepareAsync(sut);

        var instance = sut.Db.VisitRequestCampuses.Single();
        instance.CurrentHostUserId = 600;
        sut.Db.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenException>(() => PrepareAsync(sut, reuse: false));
    }

    [Fact]
    public async Task An_instance_that_does_not_belong_to_the_request_is_not_found()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.Prepare.Handle(
            new PrepareVisitSetupProgressEmailDraftCommand(Request + 500, Instance, "vi"), default));
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

    [Fact]
    public async Task Refreshing_rebuilds_the_body_and_the_attachment_but_keeps_subject_and_recipients()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);

        // The Host edits the draft the way the composer would.
        var draft = await sut.Db.EmailDrafts.SingleAsync();
        draft.Subject = "Tiêu đề Host tự viết";
        draft.BodyContent = "<p>Host tu viet</p>";
        sut.Db.EmailDraftRecipients.Add(new EmailDraftRecipient
        {
            EmailDraftId = draft.EmailDraftId, RecipientEmail = "them@partner.example",
            RecipientType = "CC", DisplayOrder = 9, CreatedAt = VietnamTime.Now(),
        });
        await sut.Db.SaveChangesAsync();
        var recipientsBefore = await sut.Db.EmailDraftRecipients.CountAsync();

        var refreshed = await sut.Refresh.Handle(
            new RefreshVisitSetupProgressEmailReportCommand(Request, Instance, draft.EmailDraftId, null), default);

        Assert.NotEqual(prepared.ReportFileId, refreshed.ReportFileId);
        var attachment = await sut.Db.EmailDraftAttachments.SingleAsync();
        Assert.Equal(refreshed.ReportFileId, attachment.FileId);

        var after = await sut.Db.EmailDrafts.SingleAsync();

        // The BODY is rebuilt — that is the point of "đồng bộ": the tables in it describe the setup,
        // and leaving them stale beside a fresh PDF would make the message contradict its attachment.
        Assert.NotEqual("<p>Host tu viet</p>", after.BodyContent);
        Assert.Equal(refreshed.BodyHtml, after.BodyContent);
        Assert.True(refreshed.BodyRewritten);

        // Subject and recipients are addressing decisions, not a picture of the setup: untouched.
        Assert.Equal("Tiêu đề Host tự viết", after.Subject);
        Assert.Equal(recipientsBefore, await sut.Db.EmailDraftRecipients.CountAsync());
    }

    [Fact]
    public async Task Refreshing_keeps_the_language_the_draft_was_built_in()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut, "en");

        var refreshed = await sut.Refresh.Handle(
            new RefreshVisitSetupProgressEmailReportCommand(Request, Instance, prepared.DraftId, null), default);

        // A null language must not silently swap an English attachment onto an English message's place
        // with a Vietnamese one.
        Assert.Equal("en", refreshed.LanguageCode);
    }

    [Fact]
    public async Task A_draft_belonging_to_another_instance_cannot_be_refreshed_through_this_route()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var draft = await sut.Db.EmailDrafts.SingleAsync();
        draft.RelatedId = Instance + 77;          // as if the id had been guessed from another campus
        await sut.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.Refresh.Handle(
            new RefreshVisitSetupProgressEmailReportCommand(Request, Instance, prepared.DraftId, null), default));
    }

    // ── Send ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sending_re_checks_the_visit_and_then_uses_the_shared_dispatcher()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var dispatcher = new RecordingDispatcher();

        var result = await Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, prepared.DraftId), default);

        Assert.True(result.Success);
        Assert.Equal(1, dispatcher.Calls);
    }

    [Fact]
    public async Task Sending_is_refused_when_the_attached_report_can_no_longer_be_read()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var dispatcher = new RecordingDispatcher();

        // Between composing and sending, the archived report becomes unreadable. The draft still looks
        // complete — the documents row and the attachment row are both there — so nothing before this
        // change would have stopped the send, and the guest would have received a message announcing an
        // attachment that was silently dropped on the way out.
        sut.Storage.Unreadable.Add(prepared.ReportFileId);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, prepared.DraftId), default));

        Assert.Equal(0, dispatcher.Calls);
        // The message has to name the cause and the way out, or the Host has no move to make.
        Assert.Contains("Đồng bộ dữ liệu mới nhất", ex.Message);
        Assert.Contains(StorageErrorCodes.FileNotFound, ex.Message);
    }

    [Fact]
    public async Task Sending_is_refused_when_the_report_row_points_at_no_real_drive_file()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var dispatcher = new RecordingDispatcher();

        // The broken-record shape: the files row says the bytes live on Drive but names no file there.
        // Every row involved still exists, so the draft passes every structural check ahead of this one.
        var file = await sut.Db.Files.SingleAsync(f => f.FileId == prepared.ReportFileId);
        file.StorageProvider = "GOOGLE_DRIVE";
        file.ExternalFileId = null;
        await sut.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, prepared.DraftId), default));

        Assert.Equal(0, dispatcher.Calls);
        Assert.Contains(StorageErrorCodes.FileReferenceInvalid, ex.Message);
        Assert.Equal(0, sut.Drive.DownloadCalls);   // a broken record never reaches the network
    }

    [Fact]
    public async Task Sending_is_refused_when_drive_refuses_the_report_for_lack_of_permission()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var dispatcher = new RecordingDispatcher();

        var file = await sut.Db.Files.SingleAsync(f => f.FileId == prepared.ReportFileId);
        file.StorageProvider = "GOOGLE_DRIVE";
        file.ExternalFileId = "1AbCdEfGhIjKlMnOpQrStUvWxYz012345";
        await sut.Db.SaveChangesAsync();
        sut.Drive.DownloadFailure = StubGoogleDriveStorage.PermissionDenied();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, prepared.DraftId), default));

        Assert.Equal(0, dispatcher.Calls);
        // Told apart from a deleted file: this one is a share/scope problem an operator can fix.
        Assert.Contains(StorageErrorCodes.FileForbidden, ex.Message);
        Assert.Contains("không có quyền", ex.Message);
    }

    [Fact]
    public async Task A_host_replaced_after_composing_cannot_send_the_draft_they_still_own()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var dispatcher = new RecordingDispatcher();

        var instance = sut.Db.VisitRequestCampuses.Single();
        instance.CurrentHostUserId = 600;
        await sut.Db.SaveChangesAsync();

        // Ownership is still true — which is exactly why the generic send endpoint would have allowed it.
        await Assert.ThrowsAsync<ForbiddenException>(() => Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, prepared.DraftId), default));
        Assert.Equal(0, dispatcher.Calls);
    }

    [Fact]
    public async Task A_visit_that_started_after_composing_cannot_be_sent_about()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var dispatcher = new RecordingDispatcher();

        var instance = sut.Db.VisitRequestCampuses.Single();
        instance.Status = VisitInstanceStatus.DuringVisit;
        await sut.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, prepared.DraftId), default));
        Assert.Equal(0, dispatcher.Calls);
    }

    [Fact]
    public async Task An_ordinary_composed_email_cannot_be_pushed_through_this_send_route()
    {
        var sut = CreateSut();
        var dispatcher = new RecordingDispatcher();

        // A hand-composed draft: right owner, right instance, but not this template.
        sut.Db.EmailDrafts.Add(new EmailDraft
        {
            EmailDraftId = 8001,
            EmailTemplateId = null,
            RelatedType = "VISIT_INSTANCE",
            RelatedId = Instance,
            Subject = "Thu tay", BodyContent = "<p>x</p>",
            BodyFormat = EmailBodyFormat.HTML, Status = EmailDraftStatus.DRAFT,
            CreatedBy = Host, CreatedAt = VietnamTime.Now(),
        });
        await sut.Db.SaveChangesAsync();

        // The guards of this route were written for a template with no token in it; nothing else may
        // borrow them.
        await Assert.ThrowsAsync<NotFoundException>(() => Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, 8001), default));
        Assert.Equal(0, dispatcher.Calls);
    }

    [Fact]
    public async Task A_draft_that_lost_its_report_is_refused_rather_than_sent_bare()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var dispatcher = new RecordingDispatcher();

        sut.Db.EmailDraftAttachments.RemoveRange(sut.Db.EmailDraftAttachments);
        await sut.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() => Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, prepared.DraftId), default));
        Assert.Equal(0, dispatcher.Calls);
    }

    [Fact]
    public async Task A_draft_someone_else_owns_is_refused()
    {
        var sut = CreateSut();
        var prepared = await PrepareAsync(sut);
        var dispatcher = new RecordingDispatcher();

        var draft = await sut.Db.EmailDrafts.SingleAsync();
        draft.CreatedBy = 777;
        await sut.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => Send(sut, dispatcher).Handle(
            new SendVisitSetupProgressEmailDraftCommand(Request, Instance, prepared.DraftId), default));
        Assert.Equal(0, dispatcher.Calls);
    }
}
