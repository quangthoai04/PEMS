using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.GetVisitInstanceSummary;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Feedbacks.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Feedbacks;
using PEMS.Domain.Entities.Minutes;
using PEMS.Infrastructure.Persistence;
using Xunit;
// PEMS.Domain.Entities.News is both a namespace and (nested inside it) a class of the same name,
// which makes the bare identifier `News` ambiguous even through a using-alias here — the two News
// entities below are referenced fully-qualified instead.
using NewsEntity = PEMS.Domain.Entities.News.News;
using NewsTranslationEntity = PEMS.Domain.Entities.News.NewsTranslation;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Dual-read migration tests for GetVisitInstanceSummaryQueryHandler — INSTANCE-LEVEL (query key
/// VisitInstanceId; scope = Staff-Leader-of-campus / HO / Host). A MIXED v2 request must return 200 with
/// the TARGET instance's form content. Disposable <c>pems_pr3_test</c>, per-test rolled-back transaction.
/// </summary>
public sealed class VisitInstanceSummaryV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong VisitorOwner = 8, VisitorOther = 22, SlCampus1 = 3, SlCampus2 = 9, HoUser = 2;
    private const ulong Campus1 = 1, Campus2 = 2, Campus3 = 3;
    // Real seeded IC Staff users on pems_pr3_test, campus-matched — same constants the sibling
    // Host-scope test files in this folder already use (ConfirmFaceTagsScopeV2Tests,
    // MinutesMutationScopeV2Tests, NewsContributionConsentV2Tests, etc.).
    private const ulong HostCampus1 = 101, HostCampus2 = 103;

    private static bool? _dbUp;

    private static ApplicationDbContext NewContext(CommandCounter? counter = null)
    {
        var b = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString));
        if (counter is not null) b.AddInterceptors(counter);
        return new ApplicationDbContext(b.Options);
    }

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master into it to run these tests.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => UserId is not null;
        public ulong? UserId { get; init; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static FakeUser Ho() => new() { UserId = HoUser, RoleCode = RoleCodes.Ho };
    private static FakeUser VisitorUnrelated() => new() { UserId = VisitorOther, RoleCode = RoleCodes.Visitor };
    private static FakeUser StaffLeader(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId };
    // Host eligibility (isHost = instance.CurrentHostUserId == userId) has no role/subrole check of
    // its own in the handler, but a real Host is always Staff — matching the sibling scope test files.
    private static FakeUser Host(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = campusId };

    /// <summary>Marks the given instance ASSIGNED with a host, matching what CampusApprovalExecutor
    /// would have written in one transaction — needed because the shared Seed() helper always leaves
    /// instances at WAITING_REQUEST_APPROVAL with no host. trg_visit_campuses guards refuse a host on
    /// a campus that hasn't been decided, so Status/DecidedBy/DecidedAt/HostAssignedBy/HostAssignedAt
    /// must all move together (same field set VisitInstanceContributionV2Tests.NewInstance uses).</summary>
    private static async Task AssignHost(ApplicationDbContext db, ulong visitInstanceId, ulong hostUserId, ulong decidedByUserId)
    {
        var instance = await db.VisitRequestCampuses.FirstAsync(c => c.VisitInstanceId == visitInstanceId);
        instance.Status = VisitInstanceStatuses.Assigned;
        instance.CurrentHostUserId = hostUserId;
        instance.HostAssignedBy = decidedByUserId;
        instance.HostAssignedAt = DateTime.Now;
        instance.DecidedBy = decidedByUserId;
        instance.DecidedAt = DateTime.Now;
        instance.DecisionActorRole = "STAFF_LEADER";
        await db.SaveChangesAsync();
    }

    /// <summary>Writes one real Feedback row exactly the way SubmitVisitFeedbackCommandHandler would —
    /// VisitInstanceId always set, denormalized submitter/target name snapshots always populated.</summary>
    private static async Task<Feedback> SeedFeedback(
        ApplicationDbContext db, ulong visitRequestId, ulong visitInstanceId,
        ulong submittedByUserId, string submitterRole, string feedbackType, byte rating,
        string targetNameSnapshot, string? comment = null)
    {
        var feedback = new Feedback
        {
            VisitRequestId = visitRequestId,
            VisitInstanceId = visitInstanceId,
            FeedbackType = feedbackType,
            SubmittedByUserId = submittedByUserId,
            SubmitterRole = submitterRole,
            SubmitterContext = submitterRole == FeedbackSubmitterRoles.Host ? "Host phụ trách" : "Người tạo đoàn",
            SubmitterNameSnapshot = $"Submitter-{submittedByUserId}",
            TargetType = FeedbackTargetTypes.VisitInstance,
            TargetNameSnapshot = targetNameSnapshot,
            Rating = rating,
            Comment = comment,
            SubmittedAt = DateTime.Now,
        };
        db.Feedbacks.Add(feedback);
        await db.SaveChangesAsync();
        return feedback;
    }

    private static GetVisitInstanceSummaryQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new VisitFormReadService(db, user, NullLogger<VisitFormReadService>.Instance));

    private static Task<ProcessSummaryPageDto> Run(ApplicationDbContext db, ICurrentUserService user, ulong instanceId)
        => Handler(db, user).Handle(new GetVisitInstanceSummaryQuery(instanceId), CancellationToken.None);

    // ── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Three mixed campuses, targeting the LAST one.
    ///
    /// Replaces the former V1 test: the global form columns are gone, so the live risk is a reader that
    /// treats one campus as representative of the request. A two-campus A/B pair cannot separate "reads the
    /// target" from "reads the first two"; a third campus can. The permissions block is asserted too,
    /// because it carries its own copy of the name and could drift from the summary.
    /// </summary>
    [Fact]
    public async Task Mixed_three_campus_target_C_reads_only_C()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2, Campus3 }, mixed: true);
        var dto = await Run(db, Ho(), inst[2].VisitInstanceId);

        Assert.Equal("DELEG-C", dto.RequestSummary.DelegationName);
        Assert.Equal("DELEG-C", dto.Permissions.DelegationName);
        Assert.Contains(dto.RequestSummary.GuestMembers, m => m.FullName == "C-guest");
        Assert.DoesNotContain(dto.RequestSummary.GuestMembers, m => m.FullName == "A-guest");
        Assert.DoesNotContain(dto.RequestSummary.GuestMembers, m => m.FullName == "B-guest");
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_single_reads_target_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, Ho(), inst[0].VisitInstanceId);

        Assert.Equal("V2-DELEG", dto.RequestSummary.DelegationName);
        Assert.NotEqual("GLOBAL-DELEG", dto.RequestSummary.DelegationName);
        Assert.Contains(dto.RequestSummary.GuestMembers, m => m.FullName == "A-guest");
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_A_returns_200_with_A_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, Ho(), inst[0].VisitInstanceId);

        Assert.Equal("DELEG-A", dto.RequestSummary.DelegationName);
        Assert.Equal("DELEG-A", dto.Permissions.DelegationName);
        Assert.Contains(dto.RequestSummary.GuestMembers, m => m.FullName == "A-guest");
        Assert.DoesNotContain(dto.RequestSummary.GuestMembers, m => m.FullName == "B-guest");
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_multi_mixed_target_B_returns_200_with_B_only()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var dto = await Run(db, Ho(), inst[1].VisitInstanceId);

        Assert.Equal("DELEG-B", dto.RequestSummary.DelegationName);
        Assert.Contains(dto.RequestSummary.GuestMembers, m => m.FullName == "B-guest");
        Assert.DoesNotContain(dto.RequestSummary.GuestMembers, m => m.FullName == "A-guest");
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task V2_missing_detail_throws_no_fallback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var detail = await db.VisitInstanceFormDetails.FirstAsync(d => d.VisitInstanceId == inst[0].VisitInstanceId);
        db.VisitInstanceFormDetails.Remove(detail);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(db, Ho(), inst[0].VisitInstanceId));
        Assert.Equal(VisitFormV2ErrorCodes.VisitFormDetailMissing, ex.ErrorCode);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task StaffLeader_of_campusA_cannot_access_campusB_instance()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => Run(db, StaffLeader(SlCampus1, Campus1), inst[1].VisitInstanceId));
        var ok = await Run(db, StaffLeader(SlCampus1, Campus1), inst[0].VisitInstanceId);
        Assert.Equal("DELEG-A", ok.RequestSummary.DelegationName);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Visitor_is_forbidden()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        await Assert.ThrowsAsync<ForbiddenException>(() => Run(db, VisitorUnrelated(), inst[0].VisitInstanceId));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Query_count_constant_regardless_of_campus_count()
    {
        RequireDb();

        int small, large;
        var c1 = new CommandCounter();
        using (var db = NewContext(c1))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
            c1.Count = 0;
            await Run(db, Ho(), inst[0].VisitInstanceId);
            small = c1.Count;
            await tx.RollbackAsync();
        }
        var c3 = new CommandCounter();
        using (var db = NewContext(c3))
        using (var tx = await db.Database.BeginTransactionAsync())
        {
            var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2, Campus3 }, mixed: true);
            c3.Count = 0;
            await Run(db, Ho(), inst[0].VisitInstanceId);
            large = c3.Count;
            await tx.RollbackAsync();
        }
        Assert.Equal(small, large);
    }

    // ── VisitRequestId + real Minutes/Media/News workspace state ──────────────

    [Fact]
    public async Task VisitRequestId_is_populated_for_the_frontend_history_link()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, Ho(), inst[0].VisitInstanceId);

        Assert.Equal(req.VisitRequestId, dto.VisitRequestId);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Minutes_media_news_report_no_data_when_none_exists_not_a_fake_placeholder()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var dto = await Run(db, Ho(), inst[0].VisitInstanceId);

        Assert.False(dto.MinutesSummary!.HasMinutes);
        Assert.Equal("NOT_STARTED", dto.MinutesSummary.Status);
        Assert.False(dto.MinutesSummary.CanCurrentUserEdit);
        Assert.False(dto.MinutesSummary.CanCurrentUserTakeLock);

        Assert.Empty(dto.MediaSummary!.Items);
        Assert.Equal(0, dto.MediaSummary.UploadedCount);
        Assert.False(dto.MediaSummary.CanCurrentUserUpload);

        Assert.False(dto.NewsSummary!.HasNews);
        Assert.Equal("NOT_STARTED", dto.NewsSummary.Status);
        Assert.False(dto.NewsSummary.CanCurrentUserCreate);
        Assert.False(dto.NewsSummary.CanCurrentUserEdit);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Minutes_shows_real_content_when_it_exists_but_never_grants_edit_on_this_page()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        db.Minutes.Add(new Minute
        {
            VisitInstanceId = inst[0].VisitInstanceId,
            Title = "Bien ban buoi lam viec",
            Content = "Noi dung bien ban that",
            Status = "SAVED", // minutes.status ENUM is ('DRAFT','SAVED') only — no 'COMPLETED' value exists
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        // A Staff Leader is a genuine Contribution-page editor for a live instance — using that
        // viewer here proves the forced-false flags are this handler's own decision, not an
        // accident of picking a viewer (HO) who was never going to get edit rights anyway.
        var dto = await Run(db, StaffLeader(SlCampus1, Campus1), inst[0].VisitInstanceId);

        Assert.True(dto.MinutesSummary!.HasMinutes);
        Assert.Equal("Noi dung bien ban that", dto.MinutesSummary.Content);
        Assert.Equal("SAVED", dto.MinutesSummary.Status);
        Assert.False(dto.MinutesSummary.CanCurrentUserEdit);
        Assert.False(dto.MinutesSummary.CanCurrentUserTakeLock);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task News_shows_real_content_when_it_exists_but_never_grants_create_or_edit_on_this_page()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var news = new NewsEntity
        {
            VisitInstanceId = inst[0].VisitInstanceId,
            AuthorUserId = HoUser,
            Status = "PUBLISHED",
            SubmittedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        news.Translations.Add(new NewsTranslationEntity
        {
            LanguageCode = "vi",
            Title = "Doan ABC tham FPTU",
            Slug = "doan-abc-tham-fptu-" + Guid.NewGuid().ToString("N")[..8],
            Summary = "Buoi lam viec thanh cong.",
            CreatedAt = DateTime.Now,
        });
        db.News.Add(news);
        await db.SaveChangesAsync();

        var dto = await Run(db, StaffLeader(SlCampus1, Campus1), inst[0].VisitInstanceId);

        Assert.True(dto.NewsSummary!.HasNews);
        Assert.Equal("Doan ABC tham FPTU", dto.NewsSummary.Title);
        Assert.Equal("Buoi lam viec thanh cong.", dto.NewsSummary.Description);
        Assert.False(dto.NewsSummary.CanCurrentUserCreate);
        Assert.False(dto.NewsSummary.CanCurrentUserEdit);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task HO_never_receives_the_internal_news_rejection_note()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var news = new NewsEntity
        {
            VisitInstanceId = inst[0].VisitInstanceId,
            AuthorUserId = HoUser,
            Status = "REJECTED",
            SubmittedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
            ReviewNote = "Noi dung chua phu hop, can chinh sua lai.",
        };
        news.Translations.Add(new NewsTranslationEntity
        {
            LanguageCode = "vi",
            Title = "Doan XYZ tham FPTU",
            Slug = "doan-xyz-tham-fptu-" + Guid.NewGuid().ToString("N")[..8],
            CreatedAt = DateTime.Now,
        });
        db.News.Add(news);
        await db.SaveChangesAsync();

        var asHo = await Run(db, Ho(), inst[0].VisitInstanceId);
        Assert.Null(asHo.NewsSummary!.RejectionReason);

        var asStaffLeader = await Run(db, StaffLeader(SlCampus1, Campus1), inst[0].VisitInstanceId);
        Assert.Equal("Noi dung chua phu hop, can chinh sua lai.", asStaffLeader.NewsSummary!.RejectionReason);
        await tx.RollbackAsync();
    }

    // ── Feedback: HO access, instance isolation, foreign access, empty state ───

    [Fact]
    public async Task HO_receives_the_correct_feedback_for_the_instance()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        var feedback = await SeedFeedback(
            db, req.VisitRequestId, inst[0].VisitInstanceId,
            VisitorOwner, FeedbackSubmitterRoles.Visitor, FeedbackTypes.VisitorOverall,
            rating: 5, targetNameSnapshot: "V2-DELEG", comment: "Rat tot.");

        var dto = await Run(db, Ho(), inst[0].VisitInstanceId);

        var item = Assert.Single(dto.FeedbackSummary);
        Assert.Equal(feedback.FeedbackId, item.FeedbackId);
        Assert.Equal(FeedbackTypes.VisitorOverall, item.FeedbackType);
        Assert.Equal(5, item.Rating);
        Assert.Equal("Rat tot.", item.Comment);
        Assert.Equal("V2-DELEG", item.TargetNameSnapshot);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Instance_isolation_sibling_campus_feedback_never_leaks_into_the_requested_instance()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Two campuses on the SAME multi-campus request — the only case where a VisitRequestId-only
        // filter (instead of the real VisitInstanceId filter) would visibly leak.
        var (req, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        var instanceA = inst[0].VisitInstanceId;
        var instanceB = inst[1].VisitInstanceId;

        await SeedFeedback(db, req.VisitRequestId, instanceA, VisitorOwner,
            FeedbackSubmitterRoles.Visitor, FeedbackTypes.VisitorOverall, rating: 5, targetNameSnapshot: "Feedback-A");
        await SeedFeedback(db, req.VisitRequestId, instanceB, VisitorOwner,
            FeedbackSubmitterRoles.Visitor, FeedbackTypes.VisitorOverall, rating: 1, targetNameSnapshot: "Feedback-B");

        var dtoA = await Run(db, Ho(), instanceA);
        var itemA = Assert.Single(dtoA.FeedbackSummary);
        Assert.Equal("Feedback-A", itemA.TargetNameSnapshot);
        Assert.DoesNotContain(dtoA.FeedbackSummary, f => f.TargetNameSnapshot == "Feedback-B");

        var dtoB = await Run(db, Ho(), instanceB);
        var itemB = Assert.Single(dtoB.FeedbackSummary);
        Assert.Equal("Feedback-B", itemB.TargetNameSnapshot);
        Assert.DoesNotContain(dtoB.FeedbackSummary, f => f.TargetNameSnapshot == "Feedback-A");
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Current_host_receives_feedback_for_their_own_instance()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);
        await AssignHost(db, inst[0].VisitInstanceId, HostCampus1, decidedByUserId: SlCampus1);
        await SeedFeedback(db, req.VisitRequestId, inst[0].VisitInstanceId,
            HostCampus1, FeedbackSubmitterRoles.Host, FeedbackTypes.HostDelegationOverall,
            rating: 4, targetNameSnapshot: "V2-DELEG");

        var dto = await Run(db, Host(HostCampus1, Campus1), inst[0].VisitInstanceId);

        var item = Assert.Single(dto.FeedbackSummary);
        Assert.Equal(FeedbackTypes.HostDelegationOverall, item.FeedbackType);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Unrelated_host_of_a_different_instance_is_forbidden_and_never_sees_feedback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        // Host of instance B (Campus2) — has no relation to instance A (Campus1) at all.
        await AssignHost(db, inst[1].VisitInstanceId, HostCampus2, decidedByUserId: SlCampus2);
        await SeedFeedback(db, req.VisitRequestId, inst[0].VisitInstanceId,
            VisitorOwner, FeedbackSubmitterRoles.Visitor, FeedbackTypes.VisitorOverall,
            rating: 3, targetNameSnapshot: "Feedback-A");

        await Assert.ThrowsAsync<ForbiddenException>(
            () => Run(db, Host(HostCampus2, Campus2), inst[0].VisitInstanceId));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Foreign_staff_leader_is_forbidden_and_never_sees_feedback()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1, Campus2 }, mixed: true);
        await SeedFeedback(db, req.VisitRequestId, inst[0].VisitInstanceId,
            VisitorOwner, FeedbackSubmitterRoles.Visitor, FeedbackTypes.VisitorOverall,
            rating: 3, targetNameSnapshot: "Feedback-A");

        // Staff Leader of Campus1 asking about the Campus2 instance of the SAME request — already
        // proven forbidden generally by StaffLeader_of_campusA_cannot_access_campusB_instance; this
        // asserts the same refusal holds even when the target instance genuinely has feedback data.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => Run(db, StaffLeader(SlCampus1, Campus1), inst[1].VisitInstanceId));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Empty_feedback_is_a_200_with_an_empty_list_never_a_403()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (_, inst) = await Seed(db, FormSchemaVersions.PerCampus, new[] { Campus1 }, mixed: false);

        var dto = await Run(db, Ho(), inst[0].VisitInstanceId);

        Assert.NotNull(dto.FeedbackSummary);
        Assert.Empty(dto.FeedbackSummary);
        await tx.RollbackAsync();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private static VisitRequest NewRequest(byte schemaVersion, string scope, bool mixed) => new()
    {
        RequestCode = "VIS-" + Guid.NewGuid().ToString("N")[..12],
        RegistrantUserId = VisitorOwner,
        CreatedSource = "VISITOR_SUBMITTED",
        HasMixedCampusDetails = mixed,
        RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
        RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
        VisitScope = scope,
        // Pure V2: form content is per campus (see NewDetail). The request row keeps only the PRIMARY
        // contact — a request-level relation, distinct from each campus's operational contact.
        Status = "PENDING_APPROVAL", SubmittedAt = DateTime.Now, CreatedAt = DateTime.Now,
    };

    private static VisitRequestCampus NewInstance(ulong campusId) => new()
    {
        CampusId = campusId,
        PlannedStartAt = DateTime.Now.AddDays(20),
        PlannedEndAt = DateTime.Now.AddDays(20).AddHours(2),
        Status = "WAITING_REQUEST_APPROVAL",
        // Self-matched: the registrant is this campus's operational contact, so the campus sits
        // past the confirmation gate. A campus beyond WAITING_CONTACT_CONFIRMATION with no
        // contact is refused by trg_visit_campuses_op_contact_guard_bi.
        OperationalContactUserId = VisitorOwner,
        OperationalContactConfirmedAt = DateTime.Now,
        OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
        CreatedAt = DateTime.Now,
    };

    private static VisitInstanceFormDetail NewDetail(string tag, bool perCampus) => new()
    {
        DelegationName = perCampus ? $"DELEG-{tag}" : "V2-DELEG",
        VisitType = "MEETING",
        Purpose = perCampus ? $"PURPOSE-{tag}" : "V2-PURPOSE",
        WorkingContent = perCampus ? $"CONTENT-{tag}" : "V2-CONTENT",
        OperationalContactFullName = $"Op-{tag}", OperationalContactOrganization = $"OpOrg-{tag}", OperationalContactJobTitle = "Trưởng phòng Hợp tác",
        OperationalContactPhone = "+8410", OperationalContactEmail = $"op-{tag}@example.com",
        WorkingLanguage = "EN", MediaConsentStatus = "AGREED",
        FormRevision = 1, ApprovalRevision = 1, CreatedAt = DateTime.Now,
    };

    private static VisitGuestMember NewMember(ulong requestId, string name, string type = "GUEST") => new()
    {
        VisitRequestId = requestId, MemberType = type, FullName = name,
        Organization = "GOrg", JobTitle = "GJob", Nationality = "VN", CreatedAt = DateTime.Now,
    };

    private static async Task<(VisitRequest req, List<VisitRequestCampus> instances)> Seed(
        ApplicationDbContext db, byte schemaVersion, ulong[] campusIds, bool mixed)
    {
        var req = NewRequest(schemaVersion, campusIds.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS", mixed);
        var isV2 = schemaVersion >= FormSchemaVersions.PerCampus;
        var tags = new[] { "A", "B", "C", "D", "E" };
        for (var i = 0; i < campusIds.Length; i++)
        {
            var inst = NewInstance(campusIds[i]);
            if (isV2) inst.FormDetail = NewDetail(tags[i], perCampus: mixed);
            req.CampusInstances.Add(inst);
        }
        if (!isV2)
        {
            req.GuestMembers.Add(NewMember(0, "G1"));
            req.GuestMembers.Add(NewMember(0, "S1", "EXTERNAL_SUPPORT"));
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();
        if (isV2)
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                var member = NewMember(req.VisitRequestId, $"{tags[i]}-guest");
                db.VisitGuestMembers.Add(member);
                await db.SaveChangesAsync();
                db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
                {
                    VisitRequestId = req.VisitRequestId,
                    VisitInstanceId = ordered[i].VisitInstanceId,
                    GuestMemberId = member.GuestMemberId,
                    DisplayOrder = 0, CreatedAt = DateTime.Now,
                });
            }
            await db.SaveChangesAsync();
        }
        return (req, ordered);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Count++; return base.ReaderExecuting(command, eventData, result); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        { Count++; return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
    }
}
