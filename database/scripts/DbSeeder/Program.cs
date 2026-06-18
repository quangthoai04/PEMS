using System;
using System.IO;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace DbSeeder
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var sqlFile = "pems_full.sql";
            if (!File.Exists(sqlFile))
            {
                Console.WriteLine($"Cannot find {sqlFile}");
                return;
            }

            var scriptText = await File.ReadAllTextAsync(sqlFile);

            var connStrBuilder = new MySqlConnectionStringBuilder
            {
                Server = "localhost",
                Port = 3306,
                UserID = "root",
                Password = "123456",
                AllowUserVariables = true
            };

            // First connect without DB to create it if not exists
            using (var conn = new MySqlConnection(connStrBuilder.ConnectionString))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE DATABASE IF NOT EXISTS pems_db;";
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("Database created/exists.");
            }

            connStrBuilder.Database = "pems_db";
            using (var conn = new MySqlConnection(connStrBuilder.ConnectionString))
            {
                await conn.OpenAsync();
                var script = new MySqlScript(conn, scriptText);
                await script.ExecuteAsync();
                Console.WriteLine("Successfully executed pems_full.sql.");
            }
        }
    }
}
