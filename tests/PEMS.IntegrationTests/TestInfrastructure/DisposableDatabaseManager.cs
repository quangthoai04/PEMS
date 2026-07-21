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

            _disposableDbName = "pems_test_run_" + Guid.NewGuid().ToString("N")[..8];
            
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
                    var sqlPath = Path.Combine(dir.FullName, "docs", "database", "scripts", "PEMS_FULL_V2_SEED_COMPLETE_ROLE_RELATIONS_STAFF_LEADER_INVITES_FIXED.sql");
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
            
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try
                {
                    using var conn = new MySqlConnection(masterConnStr);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"DROP DATABASE IF EXISTS `{_disposableDbName}`;";
                    cmd.ExecuteNonQuery();
                }
                catch {}
            };
            
            return _disposableConnectionString;
        }
    }
}
