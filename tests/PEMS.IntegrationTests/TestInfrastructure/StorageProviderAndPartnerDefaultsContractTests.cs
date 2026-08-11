using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Pins the two schema decisions taken by the SQL cleanup re-audit, both of which are invisible to
/// the application and would therefore rot silently.
///
/// <para>
/// <b>files.storage_provider</b> was shrunk from six values to three. Only the DB enforces that;
/// no validator, DTO or handler ever rejected 'S3'. Without a test, re-widening the ENUM — or a
/// stray hand-written INSERT — would go unnoticed until a row existed that no read path
/// understands. LOCAL is asserted as VALID on purpose: it is the live disk-storage branch
/// (LocalFileStorageService.SaveAsync → OpenReadAsync → /api/files/{id}/download), not a legacy
/// value waiting to be cleaned up, and a future pass should have to delete this assertion
/// deliberately rather than drop LOCAL by momentum.
/// </para>
/// <para>
/// <b>partners.profile_status / visibility</b> defaults were changed to fail closed. Every
/// application INSERT names both columns, so the defaults are reached only by hand-written SQL —
/// which is exactly why nothing else would catch a regression here. The point of the change is
/// that raw SQL can no longer mint an APPROVED, PUBLIC partner that never passed approval.
/// </para>
/// <para>
/// Runs against the disposable database built from the pinned canonical script, so the schema
/// under test is the one the repository ships. Real databases are never touched.
/// </para>
/// </summary>
public sealed class StorageProviderAndPartnerDefaultsContractTests
{
    private static string ConnString => DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    /// <summary>High ids, well clear of the seed, so these rows cannot collide with fixture data.</summary>
    private const ulong FileIdBase = 9_900_100;
    private const ulong PartnerIdBase = 9_900_200;

    private static void Exec(ApplicationDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static T Scalar<T>(ApplicationDbContext db, string sql, Func<DbDataReader, T> read)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read(), $"Query returned no row: {sql}");
        return read(reader);
    }

    private static string ColumnType(ApplicationDbContext db, string table, string column) =>
        Scalar(db, $"SELECT COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() " +
                   $"AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'", r => r.GetString(0));

    private static string ColumnDefault(ApplicationDbContext db, string table, string column) =>
        Scalar(db, $"SELECT COALESCE(COLUMN_DEFAULT, '<null>') FROM information_schema.COLUMNS " +
                   $"WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'",
            r => r.GetString(0));

    // ── files.storage_provider ────────────────────────────────────────────────

    [Fact]
    public void StorageProvider_Enum_Is_Exactly_Local_GoogleDrive_Other()
    {
        using var db = NewContext();
        Assert.Equal("enum('LOCAL','GOOGLE_DRIVE','OTHER')", ColumnType(db, "files", "storage_provider"));
        Assert.Equal("LOCAL", ColumnDefault(db, "files", "storage_provider"));
    }

    [Theory]
    [InlineData("LOCAL")]         // disk storage — live, not legacy
    [InlineData("GOOGLE_DRIVE")]  // FileUploadService / UploadFileCommandHandler Drive branch
    [InlineData("OTHER")]         // metadata-only rows, e.g. embedded YouTube media
    public void StorageProvider_Accepts_Every_Supported_Value(string provider)
    {
        using var db = NewContext();
        var id = FileIdBase + (ulong)provider.Length;
        try
        {
            Exec(db, $"INSERT INTO files (file_id, storage_provider, object_key, original_filename, uploaded_at) " +
                     $"VALUES ({id}, '{provider}', 'cleanup-contract/{id}', 'contract.bin', NOW())");

            var stored = Scalar(db, $"SELECT storage_provider FROM files WHERE file_id = {id}", r => r.GetString(0));
            Assert.Equal(provider, stored);
        }
        finally
        {
            Exec(db, $"DELETE FROM files WHERE file_id = {id}");
        }
    }

    [Theory]
    [InlineData("S3")]
    [InlineData("AZURE")]
    [InlineData("GCS")]
    public void StorageProvider_Rejects_Every_Removed_Value(string provider)
    {
        using var db = NewContext();
        var id = FileIdBase + 50 + (ulong)provider.Length;
        try
        {
            // STRICT_TRANS_TABLES turns a value outside the ENUM into an error rather than a silent ''.
            var ex = Assert.ThrowsAny<DbException>(() =>
                Exec(db, $"INSERT INTO files (file_id, storage_provider, object_key, original_filename, uploaded_at) " +
                         $"VALUES ({id}, '{provider}', 'cleanup-contract/{id}', 'contract.bin', NOW())"));

            Assert.Contains("storage_provider", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, Scalar(db, $"SELECT COUNT(*) FROM files WHERE file_id = {id}", r => r.GetInt64(0)));
        }
        finally
        {
            Exec(db, $"DELETE FROM files WHERE file_id = {id}");
        }
    }

    // ── partners defaults ─────────────────────────────────────────────────────

    [Fact]
    public void Partner_Insert_Without_Status_Or_Visibility_Falls_Back_To_PendingApproval_And_Internal()
    {
        using var db = NewContext();
        var id = PartnerIdBase + 1;
        try
        {
            // Deliberately omits profile_status and visibility — the one case the DB defaults decide.
            Exec(db, $"INSERT INTO partners (partner_id, owner_campus_id, name, partner_type, created_at) " +
                     $"VALUES ({id}, 1, 'Cleanup Contract Partner', 'UNIVERSITY', NOW())");

            var (status, visibility) = Scalar(db,
                $"SELECT profile_status, visibility FROM partners WHERE partner_id = {id}",
                r => (r.GetString(0), r.GetString(1)));

            Assert.Equal("PENDING_APPROVAL", status);
            Assert.Equal("INTERNAL", visibility);
        }
        finally
        {
            Exec(db, $"DELETE FROM partners WHERE partner_id = {id}");
        }
    }

    [Fact]
    public void Partner_Defaults_Are_Declared_FailClosed_In_The_Schema()
    {
        using var db = NewContext();
        Assert.Equal("PENDING_APPROVAL", ColumnDefault(db, "partners", "profile_status"));
        Assert.Equal("INTERNAL", ColumnDefault(db, "partners", "visibility"));
    }

    [Fact]
    public void Partner_ProfileStatus_Enum_Still_Carries_Draft()
    {
        using var db = NewContext();

        // DRAFT has live rows plus a filter, a label and a badge in the partner management screen.
        // The default moved; the value set did not. Shrinking it is a separate, unapproved decision.
        Assert.Equal("enum('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')",
            ColumnType(db, "partners", "profile_status"));
        Assert.Equal("enum('PRIVATE','INTERNAL','PUBLIC')", ColumnType(db, "partners", "visibility"));
    }

    [Fact]
    public void Explicit_Values_Still_Win_Over_The_New_Defaults()
    {
        using var db = NewContext();
        var id = PartnerIdBase + 2;
        try
        {
            // The approval workflow is untouched: a caller that names the columns still gets what it asked
            // for, which is what every application INSERT path does.
            Exec(db, $"INSERT INTO partners (partner_id, owner_campus_id, name, partner_type, " +
                     $"profile_status, visibility, created_at) " +
                     $"VALUES ({id}, 1, 'Cleanup Contract Approved', 'COMPANY', 'APPROVED', 'PUBLIC', NOW())");

            var (status, visibility) = Scalar(db,
                $"SELECT profile_status, visibility FROM partners WHERE partner_id = {id}",
                r => (r.GetString(0), r.GetString(1)));

            Assert.Equal("APPROVED", status);
            Assert.Equal("PUBLIC", visibility);
        }
        finally
        {
            Exec(db, $"DELETE FROM partners WHERE partner_id = {id}");
        }
    }
}
