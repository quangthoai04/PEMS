using PEMS.Application.Delegations.News;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.Delegations.NewsTests;

/// <summary>
/// "isReadonlyViewer" regression (VisitProcess §5 Tin tức đoàn khách): a Host who is ALSO the guest
/// side of their own visit (the registrant self-submitted it, or is the confirmed operational
/// contact) must keep full Host capabilities — create/edit/approve/reject — exactly as a Host who has
/// no such extra relation. Before the fix, <c>isReadonlyViewer = actor.IsHo || actor.IsGuestSide</c>
/// ignored IsHost/IsStaffLeaderOfCampus entirely, so this exact combination silently lost the "Tạo bài
/// tin tức" button and every action flag — while the sibling endpoint behind the Create-News page
/// (GetEligibleVisitInstancesForNewsQueryHandler, same VisitNewsAccess.Evaluate call) kept saying
/// CanCreate: true for the identical user + instance. Two verdicts about one user is exactly the bug
/// VisitNewsAccess was built to make impossible (see its class doc) — this file pins the part of that
/// contract this handler used to break on its own, after the evaluator already got it right.
/// </summary>
public class GetVisitInstanceNewsQueryHandlerTests
{
    private static (DelegationsTestDbContext Db, GetVisitInstanceNewsQueryHandler Handler, FakeDelegationsCurrentUser CurrentUser)
        CreateSut()
    {
        var db = DelegationsTestDbContext.Create();
        // The writing window (VisitNewsAccess.IsWritingWindow) only opens at AFTER_VISIT/CLOSED —
        // these tests are about who may write, not about the window itself, so seed already-open.
        DelegationsTestData.SeedBase(db, VisitInstanceStatus.AfterVisit);
        var currentUser = new FakeDelegationsCurrentUser();
        var handler = new GetVisitInstanceNewsQueryHandler(db, currentUser);
        return (db, handler, currentUser);
    }

    [Fact]
    public async Task Host_who_is_also_the_registrant_can_still_create_news()
    {
        var (db, handler, currentUser) = CreateSut();
        // The Host self-submitted this visit (internal staff-initiated registration) — IsGuestSide
        // is now true for them via IsRegistrant, on top of already being IsHost.
        db.VisitRequests.Single(r => r.VisitRequestId == DelegationsTestData.VisitRequestId)
            .RegistrantUserId = DelegationsTestData.HostUserId;
        db.SaveChanges();
        currentUser.UserId = DelegationsTestData.HostUserId;

        var result = await handler.Handle(
            new GetVisitInstanceNewsQuery(DelegationsTestData.VisitInstanceId), default);

        Assert.True(result.CanCreate);
    }

    [Fact]
    public async Task Host_who_is_also_the_confirmed_operational_contact_can_still_create_news()
    {
        var (db, handler, currentUser) = CreateSut();
        var instance = db.VisitRequestCampuses.Single(c => c.VisitInstanceId == DelegationsTestData.VisitInstanceId);
        instance.OperationalContactUserId = DelegationsTestData.HostUserId;
        db.SaveChanges();
        currentUser.UserId = DelegationsTestData.HostUserId;

        var result = await handler.Handle(
            new GetVisitInstanceNewsQuery(DelegationsTestData.VisitInstanceId), default);

        Assert.True(result.CanCreate);
    }

    [Fact]
    public async Task Host_without_any_extra_guest_side_relation_can_create_news_unaffected()
    {
        var (_, handler, currentUser) = CreateSut();
        currentUser.UserId = DelegationsTestData.HostUserId;

        var result = await handler.Handle(
            new GetVisitInstanceNewsQuery(DelegationsTestData.VisitInstanceId), default);

        Assert.True(result.CanCreate);
    }

    [Fact]
    public async Task Host_who_is_also_registrant_keeps_canEdit_and_reviewNote_on_their_own_post()
    {
        var (db, handler, currentUser) = CreateSut();
        db.VisitRequests.Single(r => r.VisitRequestId == DelegationsTestData.VisitRequestId)
            .RegistrantUserId = DelegationsTestData.HostUserId;
        db.News.Add(new PEMS.Domain.Entities.News.News
        {
            NewsId = 1,
            VisitInstanceId = DelegationsTestData.VisitInstanceId,
            AuthorUserId = DelegationsTestData.HostUserId,
            Status = NewsConstants.Status.Rejected,
            ReviewNote = "Thiếu ảnh minh họa",
            SubmittedAt = new DateTime(2026, 8, 1),
            CreatedAt = new DateTime(2026, 8, 1),
        });
        db.SaveChanges();
        currentUser.UserId = DelegationsTestData.HostUserId;

        var result = await handler.Handle(
            new GetVisitInstanceNewsQuery(DelegationsTestData.VisitInstanceId), default);

        var item = Assert.Single(result.Items);
        Assert.True(item.CanEdit);
        Assert.Equal("Thiếu ảnh minh họa", item.ReviewNote);
    }

    [Fact]
    public async Task Staff_leader_who_is_also_the_registrant_keeps_canApprove_on_someone_elses_post()
    {
        var (db, handler, currentUser) = CreateSut();
        db.VisitRequests.Single(r => r.VisitRequestId == DelegationsTestData.VisitRequestId)
            .RegistrantUserId = 700;
        db.Users.Add(DelegationsTestData.CreateUser(700, DelegationsTestData.StaffRoleId, UserSubRoles.Leader, null));
        db.News.Add(new PEMS.Domain.Entities.News.News
        {
            NewsId = 2,
            VisitInstanceId = DelegationsTestData.VisitInstanceId,
            AuthorUserId = DelegationsTestData.HostUserId,
            Status = NewsConstants.Status.PendingReview,
            SubmittedAt = new DateTime(2026, 8, 1),
            CreatedAt = new DateTime(2026, 8, 1),
        });
        db.SaveChanges();
        currentUser.UserId = 700;
        currentUser.RoleCode = RoleCodes.Staff;
        currentUser.SubRole = UserSubRoles.Leader;
        currentUser.PrimaryCampusId = DelegationsTestData.CampusId;

        var result = await handler.Handle(
            new GetVisitInstanceNewsQuery(DelegationsTestData.VisitInstanceId), default);

        var item = Assert.Single(result.Items);
        Assert.True(item.CanApprove);
        Assert.True(item.CanReject);
        // A Staff Leader who is not ALSO the campus's Host is not an eligible writer relation — the
        // fix only restores their review flags, not a write right VisitNewsAccess never granted them.
        Assert.False(result.CanCreate);
    }

    [Fact]
    public async Task A_pure_guest_side_registrant_who_is_not_host_stays_readonly()
    {
        var (db, handler, currentUser) = CreateSut();
        db.VisitRequests.Single(r => r.VisitRequestId == DelegationsTestData.VisitRequestId)
            .RegistrantUserId = 800;
        db.Users.Add(DelegationsTestData.CreateUser(800, DelegationsTestData.StaffRoleId, UserSubRoles.Staff, null));
        // A pure guest-side, non-host viewer only ever sees PUBLISHED posts (visibility filter above
        // canEdit/canApprove) — PENDING_REVIEW would just be invisible to them, which is a different
        // assertion than the readonly one this test is pinning.
        db.News.Add(new PEMS.Domain.Entities.News.News
        {
            NewsId = 3,
            VisitInstanceId = DelegationsTestData.VisitInstanceId,
            AuthorUserId = DelegationsTestData.HostUserId,
            Status = NewsConstants.Status.Published,
            ReviewNote = "internal note",
            SubmittedAt = new DateTime(2026, 8, 1),
            CreatedAt = new DateTime(2026, 8, 1),
        });
        db.SaveChanges();
        currentUser.UserId = 800;
        currentUser.RoleCode = RoleCodes.Staff;
        currentUser.SubRole = UserSubRoles.Staff;
        currentUser.PrimaryCampusId = DelegationsTestData.CampusId;

        var result = await handler.Handle(
            new GetVisitInstanceNewsQuery(DelegationsTestData.VisitInstanceId), default);

        Assert.False(result.CanCreate);
        var item = Assert.Single(result.Items);
        Assert.False(item.CanApprove);
        Assert.False(item.CanReject);
        Assert.Null(item.ReviewNote);
    }

    [Fact]
    public async Task Ho_stays_readonly_even_when_also_guest_side()
    {
        var (db, handler, currentUser) = CreateSut();
        db.VisitRequests.Single(r => r.VisitRequestId == DelegationsTestData.VisitRequestId)
            .RegistrantUserId = 900;
        db.Roles.Add(DelegationsTestData.CreateRole(6, RoleCodes.Ho));
        db.Users.Add(DelegationsTestData.CreateUser(900, 6, null, null));
        db.SaveChanges();
        currentUser.UserId = 900;
        currentUser.RoleCode = RoleCodes.Ho;
        currentUser.SubRole = null;
        currentUser.PrimaryCampusId = null;

        var result = await handler.Handle(
            new GetVisitInstanceNewsQuery(DelegationsTestData.VisitInstanceId), default);

        Assert.False(result.CanCreate);
    }
}
