using System;
using System.IO;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace PEMS.IntegrationTests.TestInfrastructure;

public static class DisposableDatabaseManager
{
    private static readonly object _lock = new();
    private static string? _disposableConnectionString;
    private static string? _disposableDbName;

    public static string GetDisposableConnectionString(string originalConnectionString)
    {
        if (_disposableConnectionString != null)
            return _disposableConnectionString;

        lock (_lock)
        {
            if (_disposableConnectionString != null)
                return _disposableConnectionString;

            // Generate full 32 character guid to match allowlist regex
            _disposableDbName = "pems_test_run_" + Guid.NewGuid().ToString("N");
            
            // Remove 'database=xyz;' and 'GuidFormat=xyz;' for the MySql.Data master connection
            var masterConnStr = Regex.Replace(originalConnectionString, @"database=[^;]+;?", "", RegexOptions.IgnoreCase);
            masterConnStr = Regex.Replace(masterConnStr, @"GuidFormat=[^;]+;?", "", RegexOptions.IgnoreCase);
            
            using (var conn = new MySqlConnection(masterConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"CREATE DATABASE `{_disposableDbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                    cmd.ExecuteNonQuery();
                }
                
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !(Directory.Exists(Path.Combine(dir.FullName, "backend")) && Directory.Exists(Path.Combine(dir.FullName, "tests"))))
                {
                    dir = dir.Parent;
                }
                
                if (dir != null)
                {
                    var sqlPath = Path.Combine(dir.FullName, "docs", "database", "scripts", "PEMS_FULL_V2_SEED_COMPLETE_CONTACT_GUARD_AND_DASHBOARD_COVERAGE.sql");
                    if (File.Exists(sqlPath))
                    {
                        var sqlContent = File.ReadAllText(sqlPath);
                        sqlContent = sqlContent.Replace("USE `pems_db`;", $"USE `{_disposableDbName}`;");
                        var script = new MySqlScript(conn, sqlContent);
                        script.Execute();
                    }
                }
            }
            
            // For the app/tests to use (EF Core supports GuidFormat)
            _disposableConnectionString = Regex.Replace(originalConnectionString, @"database=[^;]+;", $"database={_disposableDbName};", RegexOptions.IgnoreCase);
            
            // Emergency cleanup
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try
                {
                    DropDisposableDatabase(masterConnStr, _disposableDbName);
                }
                catch {}
            };
            
            return _disposableConnectionString;
        }
    }

    public static void DropDisposableDatabase(string originalConnectionString, string dbName)
    {
        if (string.IsNullOrWhiteSpace(dbName) || !Regex.IsMatch(dbName, @"^pems_test_run_[0-9a-fA-F]{32}$"))
        {
            throw new InvalidOperationException($"Attempted to drop a database with an invalid or protected name: {dbName}");
        }

        var masterConnStr = Regex.Replace(originalConnectionString, @"database=[^;]+;?", "", RegexOptions.IgnoreCase);
        masterConnStr = Regex.Replace(masterConnStr, @"GuidFormat=[^;]+;?", "", RegexOptions.IgnoreCase);

        using var conn = new MySqlConnection(masterConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS `{dbName}`;";
        cmd.ExecuteNonQuery();
    }
}
