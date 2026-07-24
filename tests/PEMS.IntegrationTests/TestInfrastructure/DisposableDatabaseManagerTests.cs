using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PEMS.IntegrationTests.TestInfrastructure;

[Collection("IntegrationTests")]
public class DisposableDatabaseManagerTests : IClassFixture<PemsWebApplicationFactory>
{
    private readonly string _connectionString;

    public DisposableDatabaseManagerTests(PemsWebApplicationFactory factory)
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
    }

    [Theory]
    [InlineData("pems_test")]
    [InlineData("pems_db")]
    [InlineData("pems_pr3_test")]
    [InlineData("pems_test_backup")]
    [InlineData("pems_test_run_invalid")]
    [InlineData("pems_test_run_1234567890123456789012345678901")] // 31 chars
    [InlineData("pems_test_run_123456789012345678901234567890123")] // 33 chars
    public void DropDisposableDatabase_WithDisallowedName_ThrowsException(string dbName)
    {
        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => DisposableDatabaseManager.DropDisposableDatabase(_connectionString, dbName));
        Assert.Equal($"Attempted to drop a database with an invalid or protected name: {dbName}", ex.Message);
    }

    [Fact]
    public void DropDisposableDatabase_WithAllowedName_DropsDatabase()
    {
        // Arrange
        var dbName = "pems_test_run_" + Guid.NewGuid().ToString("N");
        // Create it first so we can drop it
        var masterConnStr = System.Text.RegularExpressions.Regex.Replace(_connectionString, @"database=[^;]+;?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        masterConnStr = System.Text.RegularExpressions.Regex.Replace(masterConnStr, @"GuidFormat=[^;]+;?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        using (var conn = new MySql.Data.MySqlClient.MySqlConnection(masterConnStr))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE `{dbName}`;";
            cmd.ExecuteNonQuery();
        }

        // Act
        var ex = Record.Exception(() => DisposableDatabaseManager.DropDisposableDatabase(_connectionString, dbName));

        // Assert
        Assert.Null(ex);
    }
}
