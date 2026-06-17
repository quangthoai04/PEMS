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
            var script = await File.ReadAllTextAsync(sqlFile);

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
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SHOW COLUMNS FROM users;";
                using var reader = await cmd.ExecuteReaderAsync();
                while(await reader.ReadAsync()) {
                    Console.WriteLine(reader.GetString(0));
                }
            }
        }
    }
}
