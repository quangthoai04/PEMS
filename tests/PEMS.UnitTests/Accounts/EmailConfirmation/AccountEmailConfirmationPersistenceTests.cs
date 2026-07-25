using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.UnitTests.TestInfrastructure;

namespace PEMS.UnitTests.Accounts.EmailConfirmation;

/// <summary>
/// P0 #1 persistence contract for <see cref="AccountEmailConfirmation"/>: the entity maps and round-trips
/// all lifecycle fields, the new pending status constant is stable, and the additive migration that creates
/// the table exists and is idempotent (guarded, create-only).
/// </summary>
public class AccountEmailConfirmationPersistenceTests
{
    [Fact]
    public async Task Entity_roundtrips_with_all_lifecycle_fields()
    {
        await using var db = TestApplicationDbContext.Create();
        db.Users.Add(Uc106TestData.CreateUser(500, Uc106TestData.StudentRoleId, null, null));
        await db.SaveChangesAsync();

        db.AccountEmailConfirmations.Add(new AccountEmailConfirmation
        {
            UserId = 500,
            TargetEmail = "owner@fpt.edu.vn",
            TokenHash = new string('a', 64),
            Status = AccountEmailConfirmationStatuses.Pending,
            ExpiresAt = new DateTime(2026, 8, 1, 12, 0, 0),
            ResendCount = 0,
            CreatedAt = new DateTime(2026, 7, 25, 9, 0, 0),
        });
        await db.SaveChangesAsync();

        var row = await db.AccountEmailConfirmations.SingleAsync();
        Assert.True(row.ConfirmationId > 0);
        Assert.Equal(500ul, row.UserId);
        Assert.Equal("owner@fpt.edu.vn", row.TargetEmail);
        Assert.Equal(AccountEmailConfirmationStatuses.Pending, row.Status);
        Assert.Equal(0, row.ResendCount);
        Assert.Null(row.ConfirmedAt);
        Assert.Null(row.CancelledAt);
    }

    [Fact]
    public void Pending_status_constant_matches_the_documented_value()
    {
        Assert.Equal("PENDING_EMAIL_CONFIRMATION", UserStatuses.PendingEmailConfirmation);
    }

    [Fact]
    public void Migration_exists_and_is_idempotent_and_additive()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "database", "scripts", "migrations",
            "2026_07_25_account_email_confirmations_additive.sql");
        Assert.True(File.Exists(path), $"migration not found at {path}");

        var sql = File.ReadAllText(path);
        Assert.Contains("CREATE TABLE account_email_confirmations", sql);
        Assert.Contains("information_schema.tables", sql);   // idempotent existence guard
        Assert.Contains("already exists", sql);              // the skip branch
        Assert.DoesNotContain("DROP TABLE", sql);            // additive only — never drops
        Assert.Contains("uq_account_email_confirmations_token_hash", sql);  // token hash is unique
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !(Directory.Exists(Path.Combine(dir.FullName, "backend")) &&
                                Directory.Exists(Path.Combine(dir.FullName, "tests"))))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
