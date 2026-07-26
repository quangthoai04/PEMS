using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Queries.GetVisitSubmissionResult;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Plan §17 items 6–8 — "did my submission go through?" against real MySQL.
///
/// This exists because a connection dropped after the verify transaction commits is, from the
/// browser, indistinguishable from one that never arrived. Without a lookup the visitor's only
/// options were to give up or to submit the whole form again; the second is how duplicate
/// delegations get created.
///
/// Runs against disposable <c>pems_pr3_test</c>, each test inside a rolled-back transaction.
/// </summary>
public sealed class VisitSubmissionResultLookupV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8; // a seeded VISITOR user
    private static bool? _dbUp;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    private static readonly DateTime Now = DateTime.Now;

    private sealed class FixedClock : PEMS.Application.Common.Interfaces.IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private static GetVisitSubmissionResultQueryHandler Handler(ApplicationDbContext db)
        => new(db, new FixedClock());

    private static CampusVisitFormDto Campus(string code, string delegation)
    {
        var start = Now.AddDays(20);
        return new CampusVisitFormDto(
            code, start, start.AddHours(2), delegation, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);
    }

    private static VisitRequestFormDataV2 Form(string submissionId, params CampusVisitFormDto[] campuses)
        => new(
            submissionId,
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491111", "registrant@example.com"),
            new ContactPointDto("Contact Person", "Org", "+8490000", "registrant@example.com"),
            null,
            campuses.ToList());

    private static async Task<VisitRequestPendingForm> SeedPendingAsync(
        ApplicationDbContext db, string submissionId, DateTime? consumedAt, DateTime expiresAt)
    {
        var pending = new VisitRequestPendingForm
        {
            SubmissionId = submissionId,
            RegistrantEmail = "registrant@example.com",
            FingerprintV2 = "fp-" + submissionId,
            SnapshotJson = "{}",
            CreatedAt = Now,
            ExpiresAt = expiresAt,
            ConsumedAt = consumedAt,
        };
        db.VisitRequestPendingForms.Add(pending);
        await db.SaveChangesAsync();
        return pending;
    }

    [Fact]
    public async Task A_created_request_is_reported_COMPLETED_with_its_code_and_status()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var submissionId = Guid.NewGuid().ToString("N");
        var created = await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(submissionId, Campus("HN", "Đoàn Lookup")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetVisitSubmissionResultQuery(submissionId), CancellationToken.None);

        Assert.Equal(VisitSubmissionStates.Completed, result.State);
        Assert.Equal(created.VisitRequestId, result.VisitRequestId);
        Assert.False(string.IsNullOrWhiteSpace(result.RequestCode));
        Assert.False(string.IsNullOrWhiteSpace(result.Status));
        Assert.Equal(1, result.CampusCount);
        // Wall-clock, no offset — the same shape every other DATETIME leaves here in.
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$", result.SubmittedAt!);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_created_request_reports_COMPLETED_even_though_its_snapshot_was_consumed()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // The real verify consumes the snapshot in the SAME transaction as the create, so a
        // committed request ALWAYS coexists with a consumed pending row. The request must win.
        var submissionId = Guid.NewGuid().ToString("N");
        await SeedPendingAsync(db, submissionId, consumedAt: Now, expiresAt: Now.AddMinutes(30));
        await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(submissionId, Campus("HN", "Đoàn Consumed")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetVisitSubmissionResultQuery(submissionId), CancellationToken.None);

        Assert.Equal(VisitSubmissionStates.Completed, result.State);
        Assert.NotNull(result.VisitRequestId);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_open_submission_is_PENDING_and_names_nothing()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var submissionId = Guid.NewGuid().ToString("N");
        await SeedPendingAsync(db, submissionId, consumedAt: null, expiresAt: Now.AddMinutes(30));

        var result = await Handler(db).Handle(new GetVisitSubmissionResultQuery(submissionId), CancellationToken.None);

        Assert.Equal(VisitSubmissionStates.Pending, result.State);
        Assert.Null(result.VisitRequestId);
        Assert.Null(result.RequestCode);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_consumed_submission_with_no_request_is_FAILED_not_PENDING()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // What the duplicate guard leaves behind: the snapshot is consumed and no request exists.
        // Calling that "pending" would tell the user to keep waiting for something that will never come.
        var submissionId = Guid.NewGuid().ToString("N");
        await SeedPendingAsync(db, submissionId, consumedAt: Now, expiresAt: Now.AddMinutes(30));

        var result = await Handler(db).Handle(new GetVisitSubmissionResultQuery(submissionId), CancellationToken.None);

        Assert.Equal(VisitSubmissionStates.Failed, result.State);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_expired_submission_is_FAILED()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var submissionId = Guid.NewGuid().ToString("N");
        await SeedPendingAsync(db, submissionId, consumedAt: null, expiresAt: Now.AddMinutes(-1));

        var result = await Handler(db).Handle(new GetVisitSubmissionResultQuery(submissionId), CancellationToken.None);

        Assert.Equal(VisitSubmissionStates.Failed, result.State);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_unknown_submission_is_NOT_FOUND_so_the_user_may_safely_send_again()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var result = await Handler(db).Handle(
            new GetVisitSubmissionResultQuery(Guid.NewGuid().ToString("N")), CancellationToken.None);

        Assert.Equal(VisitSubmissionStates.NotFound, result.State);
        Assert.Null(result.VisitRequestId);

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_submission_id_is_NOT_FOUND_rather_than_an_error(string submissionId)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var result = await Handler(db).Handle(new GetVisitSubmissionResultQuery(submissionId), CancellationToken.None);

        Assert.Equal(VisitSubmissionStates.NotFound, result.State);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task The_lookup_never_returns_who_submitted_it()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // The caller is anonymous, so the response must carry nothing beyond what someone holding
        // this submissionId already submitted: no registrant identity, no contact details.
        var submissionId = Guid.NewGuid().ToString("N");
        await new VisitRequestV2CreateService(db).CreateV2Async(
            Form(submissionId, Campus("HN", "Đoàn Riêng Tư")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        await db.SaveChangesAsync();

        var result = await Handler(db).Handle(new GetVisitSubmissionResultQuery(submissionId), CancellationToken.None);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.DoesNotContain("registrant@example.com", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Registrant", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("Đoàn Riêng Tư", serialized, StringComparison.Ordinal);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Two_submissions_by_the_same_person_resolve_independently()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        // Keyed on the submit INTENT, never on email — "the newest request for this address" is a
        // different question and would hand back the wrong one.
        var firstId = Guid.NewGuid().ToString("N");
        var secondId = Guid.NewGuid().ToString("N");
        var svc = new VisitRequestV2CreateService(db);
        var first = await svc.CreateV2Async(
            Form(firstId, Campus("HN", "Đoàn Một")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        var second = await svc.CreateV2Async(
            Form(secondId, Campus("HCM", "Đoàn Hai")), Registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        await db.SaveChangesAsync();

        var a = await Handler(db).Handle(new GetVisitSubmissionResultQuery(firstId), CancellationToken.None);
        var b = await Handler(db).Handle(new GetVisitSubmissionResultQuery(secondId), CancellationToken.None);

        Assert.Equal(first.VisitRequestId, a.VisitRequestId);
        Assert.Equal(second.VisitRequestId, b.VisitRequestId);
        Assert.NotEqual(a.VisitRequestId, b.VisitRequestId);

        await tx.RollbackAsync();
    }
}
