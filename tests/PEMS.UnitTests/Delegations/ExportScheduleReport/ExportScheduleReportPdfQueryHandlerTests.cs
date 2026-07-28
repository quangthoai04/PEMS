using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Translation;
using PEMS.Domain.Constants;
using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.Delegations.ExportScheduleReport;

/// <summary>
/// "Báo cáo Lịch trình" download permission: must match the exact scope rule of the VisitProcess
/// screen it is downloaded from (Host / Staff Leader of the campus / HO / Visitor owner / accepted
/// participant) — nobody else, and never when the report itself is structurally incomplete.
/// </summary>
public class ExportScheduleReportPdfQueryHandlerTests
{
    private static (ScheduleReportTestDbContext Db, ExportScheduleReportPdfQueryHandler Handler, FakeScheduleReportCurrentUser CurrentUser)
        CreateSut(string instanceStatus = VisitInstanceStatus.BeforeVisit, ulong? partnerId = null, ulong? visitorUserId = null)
    {
        var db = ScheduleReportTestDbContext.Create();
        ScheduleReportTestData.SeedBase(db, instanceStatus, partnerId, visitorUserId);

        var currentUser = new FakeScheduleReportCurrentUser();
        var formRead = new VisitFormReadService(db, currentUser, NullLogger<VisitFormReadService>.Instance);
        var storage = new Mock<IFileStorageService>(MockBehavior.Loose);
        var drive = new Mock<IGoogleDriveStorageService>(MockBehavior.Loose);
        var translator = new Mock<IContentTranslationService>(MockBehavior.Loose);
        var fileUpload = new Mock<IFileUploadService>(MockBehavior.Loose);
        var folderService = new Mock<PEMS.Application.Delegations.VisitPhotos.IVisitPhotoFolderService>(MockBehavior.Loose);
        var handler = new ExportScheduleReportPdfQueryHandler(
            db, currentUser, formRead, storage.Object, drive.Object, translator.Object,
            fileUpload.Object, folderService.Object,
            NullLogger<ExportScheduleReportPdfQueryHandler>.Instance);
        return (db, handler, currentUser);
    }

    private static void AssertLooksLikePdf(byte[] bytes)
    {
        Assert.True(bytes.Length > 100, "PDF bytes should not be empty/trivial.");
        var header = Encoding.ASCII.GetString(bytes, 0, 5);
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public async Task Handle_allows_the_host_to_download()
    {
        var (_, handler, currentUser) = CreateSut();
        currentUser.UserId = ScheduleReportTestData.HostUserId;

        var bytes = await handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default);

        AssertLooksLikePdf(bytes);
    }

    [Fact]
    public async Task Handle_allows_the_staff_leader_of_the_campus_to_download()
    {
        var (db, handler, currentUser) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(200, ScheduleReportTestData.StaffRoleId, UserSubRoles.Leader, null));
        db.SaveChanges();
        currentUser.UserId = 200;
        currentUser.RoleCode = RoleCodes.Staff;
        currentUser.SubRole = UserSubRoles.Leader;
        currentUser.PrimaryCampusId = ScheduleReportTestData.CampusId;

        var bytes = await handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default);

        AssertLooksLikePdf(bytes);
    }

    [Fact]
    public async Task Handle_allows_ho_to_download()
    {
        var (db, handler, currentUser) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(300, ScheduleReportTestData.HoRoleId, null, null));
        db.SaveChanges();
        currentUser.UserId = 300;
        currentUser.RoleCode = RoleCodes.Ho;
        currentUser.SubRole = null;
        currentUser.PrimaryCampusId = null;

        var bytes = await handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default);

        AssertLooksLikePdf(bytes);
    }

    [Fact]
    public async Task Handle_allows_an_accepted_participant_to_download()
    {
        var (db, handler, currentUser) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(400, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.VisitParticipants.Add(ScheduleReportTestData.CreateParticipant(
            1, 400, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted));
        db.SaveChanges();
        currentUser.UserId = 400;

        var bytes = await handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default);

        AssertLooksLikePdf(bytes);
    }

    [Fact]
    public async Task Handle_allows_the_visitor_owner_to_download()
    {
        var (db, handler, currentUser) = CreateSut(visitorUserId: 500);
        db.Users.Add(ScheduleReportTestData.CreateUser(500, ScheduleReportTestData.VisitorRoleId, null, null));
        db.SaveChanges();
        currentUser.UserId = 500;
        currentUser.RoleCode = RoleCodes.Visitor;
        currentUser.SubRole = null;

        var bytes = await handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default);

        AssertLooksLikePdf(bytes);
    }

    [Fact]
    public async Task Handle_throws_forbidden_for_a_user_with_no_relation_to_the_instance()
    {
        var (db, handler, currentUser) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(999, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.SaveChanges();
        currentUser.UserId = 999;
        currentUser.RoleCode = RoleCodes.Staff;
        currentUser.SubRole = UserSubRoles.Staff;
        currentUser.PrimaryCampusId = ScheduleReportTestData.CampusId;

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default));
    }

    [Fact]
    public async Task Handle_throws_forbidden_for_an_invited_but_not_yet_accepted_participant()
    {
        var (db, handler, currentUser) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(401, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.VisitParticipants.Add(ScheduleReportTestData.CreateParticipant(
            1, 401, ParticipantRoles.IcSupport, ParticipantStatuses.Invited));
        db.SaveChanges();
        currentUser.UserId = 401;

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default));
    }

    [Fact]
    public async Task Handle_throws_not_found_when_the_instance_does_not_belong_to_the_visit_request()
    {
        var (_, handler, _) = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, 999999), default));
    }

    [Fact]
    public async Task Handle_throws_validation_when_no_host_is_assigned_yet()
    {
        var db = ScheduleReportTestDbContext.Create();
        db.Campuses.Add(ScheduleReportTestData.CreateCampus());
        db.Roles.Add(ScheduleReportTestData.CreateRole(ScheduleReportTestData.HoRoleId, RoleCodes.Ho));
        db.VisitRequests.Add(ScheduleReportTestData.CreateVisitRequest());
        db.VisitRequestCampuses.Add(ScheduleReportTestData.CreateVisitInstance(currentHostUserId: null));
        db.Users.Add(ScheduleReportTestData.CreateUser(300, ScheduleReportTestData.HoRoleId, null, null));
        db.SaveChanges();

        var currentUser = new FakeScheduleReportCurrentUser { UserId = 300, RoleCode = RoleCodes.Ho, SubRole = null, PrimaryCampusId = null };
        var formRead = new VisitFormReadService(db, currentUser, NullLogger<VisitFormReadService>.Instance);
        var handler = new ExportScheduleReportPdfQueryHandler(
            db, currentUser, formRead,
            new Mock<IFileStorageService>(MockBehavior.Loose).Object,
            new Mock<IGoogleDriveStorageService>(MockBehavior.Loose).Object,
            new Mock<IContentTranslationService>(MockBehavior.Loose).Object,
            new Mock<IFileUploadService>(MockBehavior.Loose).Object,
            new Mock<PEMS.Application.Delegations.VisitPhotos.IVisitPhotoFolderService>(MockBehavior.Loose).Object,
            NullLogger<ExportScheduleReportPdfQueryHandler>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default));
    }

    [Fact]
    public async Task Handle_renders_both_logos_when_the_linked_partner_has_one()
    {
        var (db, handler, currentUser) = CreateSut(partnerId: 50);
        // Reuse the embedded FPT logo bytes as a stand-in valid PNG for the partner's logo — QuestPDF
        // actually decodes image bytes at render time, so this must be a real image, not a stub.
        db.Partners.Add(ScheduleReportTestData.CreatePartner(50, "Asia University", logoFileId: 900));
        db.Files.Add(ScheduleReportTestData.CreateFile(900, storageProvider: "LOCAL"));
        db.SaveChanges();
        currentUser.UserId = ScheduleReportTestData.HostUserId;

        var storage = new Mock<IFileStorageService>(MockBehavior.Loose);
        storage.Setup(s => s.OpenReadAsync(It.Is<PEMS.Domain.Entities.Documents.UploadedFile>(f => f.FileId == 900), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(ScheduleReportAssets.FptLogoBytes));
        var formRead = new VisitFormReadService(db, currentUser, NullLogger<VisitFormReadService>.Instance);
        var handlerWithLogo = new ExportScheduleReportPdfQueryHandler(
            db, currentUser, formRead, storage.Object,
            new Mock<IGoogleDriveStorageService>(MockBehavior.Loose).Object,
            new Mock<IContentTranslationService>(MockBehavior.Loose).Object,
            new Mock<IFileUploadService>(MockBehavior.Loose).Object,
            new Mock<PEMS.Application.Delegations.VisitPhotos.IVisitPhotoFolderService>(MockBehavior.Loose).Object,
            NullLogger<ExportScheduleReportPdfQueryHandler>.Instance);

        var bytes = await handlerWithLogo.Handle(
            new ExportScheduleReportPdfQuery(ScheduleReportTestData.VisitRequestId, ScheduleReportTestData.VisitInstanceId), default);

        AssertLooksLikePdf(bytes);
        storage.Verify(s => s.OpenReadAsync(It.Is<PEMS.Domain.Entities.Documents.UploadedFile>(f => f.FileId == 900), It.IsAny<CancellationToken>()), Times.Once);
    }
}
