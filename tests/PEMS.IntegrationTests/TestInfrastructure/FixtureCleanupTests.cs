using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Files;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// What <see cref="FixtureCleanup"/> has to get right for a suite to be independent of whatever ran
/// before it.
///
/// <para>
/// The behaviour under test is an ordering one, and ordering bugs are invisible on a clean database —
/// the old hand-written cleanups passed for months and only failed once another suite left a row behind.
/// So each test here plants exactly the kind of leftover that used to take a whole class down, and then
/// asserts the cleanup still completes.
/// </para>
/// </summary>
public sealed class FixtureCleanupTests : IDisposable
{
    private const ulong Base = 993_100;
    private const ulong CampusId = Base + 1;
    private const ulong IcDeptId = Base + 2;
    private const ulong OwnerId = Base + 10;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "pems-fixclean-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { /* a temp dir left behind must never fail a test run */ }
    }

    private static ApplicationDbContext Db() => EmailEvidenceHarness.NewContext();

    private static Task CleanAsync(ApplicationDbContext db)
        => FixtureCleanup.For(db)
            .Root("files", $"uploaded_by BETWEEN {Base} AND {Base + 100}")
            .Root("users", $"user_id BETWEEN {Base} AND {Base + 100}")
            .Root("departments", $"department_id = {IcDeptId}")
            .Root("campuses", $"campus_id = {CampusId}")
            .RunAsync();

    private static async Task SeedWorldAsync(ApplicationDbContext db)
    {
        await CleanAsync(db);

        var staffRole = await db.Roles.AsNoTracking()
            .Where(r => r.RoleCode == "STAFF").Select(r => r.RoleId).FirstAsync();

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO campuses (campus_id, campus_code, name, status) VALUES ({0}, 'FXC', {1}, 'ACTIVE')",
            CampusId, "PEMS FixtureCleanup Campus");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO departments (department_id, campus_id, name, department_type, status) "
            + "VALUES ({0}, {1}, {2}, 'IC', 'ACTIVE')",
            IcDeptId, CampusId, "PEMS FixtureCleanup IC");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO users (user_id, full_name, email, role_id, sub_role, primary_campus_id, "
            + $"department_id, status) VALUES ({OwnerId}, {{0}}, {{1}}, {staffRole}, 'STAFF', "
            + $"{CampusId}, {IcDeptId}, 'ACTIVE')",
            "FixtureCleanup Owner", $"fixclean-{OwnerId}@partner.example.com");
    }

    private static async Task<ulong> SeedFileAsync(ApplicationDbContext db)
    {
        var objectKey = $"fixclean/{Guid.NewGuid():N}.pdf";
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO files (storage_provider, object_key, original_filename, mime_type, file_size, "
            + $"uploaded_by, uploaded_at, file_purpose) VALUES ('LOCAL', {{0}}, 'tep.pdf', 'application/pdf', "
            + $"6, {OwnerId}, NOW(), '{FilePurposeDbValues.Other}')",
            objectKey);

        return await db.Files.AsNoTracking()
            .Where(f => f.ObjectKey == objectKey).Select(f => f.FileId).FirstAsync();
    }

    // ── The two leftovers that actually broke CI ─────────────────────────────

    /// <summary>
    /// A <c>documents</c> row pointing at a fixture file. <c>fk_documents_file</c> is ON DELETE RESTRICT,
    /// so deleting <c>files</c> first is refused — this is the exact shape that failed nine tests in
    /// setup, before a line of product code ran.
    /// </summary>
    [Fact]
    public async Task A_document_holding_a_fixture_file_does_not_block_the_cleanup()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = Db();
        await SeedWorldAsync(db);

        var fileId = await SeedFileAsync(db);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO documents (file_id, owner_type, title, status, created_at, created_by) "
            + $"VALUES ({fileId}, 'GENERAL', '[IT] FixtureCleanup', 'PUBLISHED', NOW(), {OwnerId})");

        await CleanAsync(db);

        Assert.Empty(await db.Files.AsNoTracking().Where(f => f.FileId == fileId).ToListAsync());
        Assert.Equal(0, await CountAsync(db, $"SELECT COUNT(*) FROM documents WHERE file_id = {fileId}"));
    }

    /// <summary>
    /// A <c>visit_participants</c> row pointing at a fixture user — the second blocker, one level further
    /// on, which appeared the moment the first was fixed by hand.
    /// </summary>
    [Fact]
    public async Task A_participant_holding_a_fixture_user_does_not_block_the_cleanup()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = Db();
        await SeedWorldAsync(db);

        var (requestId, instanceId) = await SeedVisitAsync(db);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_participants (visit_instance_id, user_id, participant_role, status, invited_at) "
            + $"VALUES ({instanceId}, {OwnerId}, 'IC_SUPPORT', 'INVITED', NOW())");

        await CleanAsync(db);

        Assert.Equal(0, await CountAsync(db, $"SELECT COUNT(*) FROM users WHERE user_id = {OwnerId}"));
        Assert.Equal(0, await CountAsync(db,
            $"SELECT COUNT(*) FROM visit_participants WHERE user_id = {OwnerId}"));

        await DropVisitAsync(db, requestId);
    }

    // ── The properties the helper promises ───────────────────────────────────

    [Fact]
    public async Task Running_it_twice_in_a_row_succeeds_and_changes_nothing_the_second_time()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = Db();
        await SeedWorldAsync(db);
        await SeedFileAsync(db);

        await CleanAsync(db);
        await CleanAsync(db); // must not throw on an already-empty band

        Assert.Equal(0, await CountAsync(db, $"SELECT COUNT(*) FROM campuses WHERE campus_id = {CampusId}"));
    }

    [Fact]
    public async Task Cleaning_an_empty_band_is_a_no_op()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = Db();
        await CleanAsync(db);
        await CleanAsync(db);
    }

    /// <summary>
    /// The reason the helper is scoped to declared roots rather than to whole tables: a row that merely
    /// sits in the same table as a fixture row is somebody else's, and must survive.
    /// </summary>
    [Fact]
    public async Task A_neighbouring_row_outside_the_declared_roots_is_left_alone()
    {
        EmailEvidenceHarness.RequireDb();
        using var db = Db();
        await SeedWorldAsync(db);

        var seedUsers = await CountAsync(db, $"SELECT COUNT(*) FROM users WHERE user_id < {Base}");
        var seedFiles = await CountAsync(db, $"SELECT COUNT(*) FROM files WHERE uploaded_by < {Base}");
        await SeedFileAsync(db);

        await CleanAsync(db);

        Assert.Equal(seedUsers, await CountAsync(db, $"SELECT COUNT(*) FROM users WHERE user_id < {Base}"));
        Assert.Equal(seedFiles, await CountAsync(db, $"SELECT COUNT(*) FROM files WHERE uploaded_by < {Base}"));
    }

    /// <summary>
    /// The guard that keeps a mis-declared root from reaching a real schema. It is checked before any
    /// statement runs, so a refusal cannot have deleted anything first.
    /// </summary>
    [Fact]
    public void It_refuses_to_run_against_a_database_that_is_not_the_disposable_one()
    {
        Assert.False(CanonicalSqlScript.DisposableNamePattern.IsMatch("pems_db"));
        Assert.False(CanonicalSqlScript.DisposableNamePattern.IsMatch("pems_test"));
        Assert.Matches(CanonicalSqlScript.DisposableNamePattern, CanonicalSqlScript.NewDisposableDatabaseName());
    }

    [Fact]
    public void A_root_without_a_predicate_is_rejected_rather_than_emptying_the_table()
    {
        Assert.Throws<ArgumentException>(() => FixtureCleanup.For(null!).Root("users", "   "));
        Assert.Throws<ArgumentException>(() => FixtureCleanup.For(null!).Root("users; DROP", "user_id = 1"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<long> CountAsync(ApplicationDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task<(ulong RequestId, ulong InstanceId)> SeedVisitAsync(ApplicationDbContext db)
    {
        var requestId = Base + 30;
        var instanceId = Base + 31;

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_requests (visit_request_id, request_code, registrant_full_name, "
            + "registrant_email, registrant_job_title, registrant_nationality, registrant_organization, "
            + "contact_person_full_name, contact_person_email, contact_person_organization, status, created_at) "
            + $"VALUES ({requestId}, 'FIXCLEAN-{requestId}', 'FixtureCleanup', "
            + "'fixclean-visit@partner.example.com', 'Tester', 'VN', 'FixtureCleanup Org', "
            + "'FixtureCleanup Contact', 'fixclean-contact@partner.example.com', 'FixtureCleanup Org', "
            + "'PENDING_APPROVAL', NOW())");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_request_campuses (visit_instance_id, visit_request_id, campus_id, "
            + $"planned_start_at, planned_end_at, status) VALUES ({instanceId}, {requestId}, {CampusId}, "
            + "DATE_ADD(NOW(), INTERVAL 30 DAY), DATE_ADD(NOW(), INTERVAL 31 DAY), 'WAITING_REQUEST_APPROVAL')");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO visit_instance_form_details (visit_instance_id, delegation_name, "
            + $"operational_contact_full_name, purpose) VALUES ({instanceId}, 'FixtureCleanup', "
            + "'FixtureCleanup Contact', 'FixtureCleanup')");

        return (requestId, instanceId);
    }

    private static Task DropVisitAsync(ApplicationDbContext db, ulong requestId)
        => FixtureCleanup.For(db).Root("visit_requests", $"visit_request_id = {requestId}").RunAsync();
}
