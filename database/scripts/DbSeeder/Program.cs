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
            var sqlFile = @"..\pems_full(3).sql";
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
                
                Console.WriteLine("Successfully executed pems_full.sql and updated sub_roles via script.");
                
                Console.WriteLine("\n--- STAFF SUB-ROLES VERIFICATION ---");
                using var verifyCmd = conn.CreateCommand();
                verifyCmd.CommandText = @"
                    SELECT u.full_name, u.email, u.status, u.sub_role, r.role_code
                    FROM users u
                    JOIN roles r ON r.role_id = u.role_id
                    WHERE r.role_code = 'STAFF'
                    ORDER BY u.email;
                ";
                using var reader = await verifyCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Console.WriteLine($"{reader["email"],-30} | {reader["full_name"],-25} | {reader["sub_role"],-10} | {reader["role_code"]}");
                }
            }
        }
    }
}
