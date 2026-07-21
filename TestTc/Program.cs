using System;
using System.IO;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

class Program {
    static void Main() {
        var masterConnStr = "server=localhost;port=3306;user=root;password=123456;AllowUserVariables=True;";
        var dbName = "pems_db";
        var sqlPath = @"d:\FULearning\SUMMER 2026 Final\PEMS\docs\database\scripts\PEMS_FULL_V2_SEED_COMPLETE_ROLE_RELATIONS_STAFF_LEADER_INVITES_FIXED.sql";

        using var conn = new MySqlConnection(masterConnStr);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS {dbName} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{dbName}';";
        var tableCount = Convert.ToInt32(cmd.ExecuteScalar());

        if (tableCount < 10) {
            Console.WriteLine("pems_db is empty or missing tables. Seeding now...");
            var sqlContent = File.ReadAllText(sqlPath);
            sqlContent = sqlContent.Replace("USE `pems_db`;", $"USE `{dbName}`;");
            
            var script = new MySqlScript(conn, sqlContent);
            script.Execute();
            Console.WriteLine("Database seeded successfully.");
        } else {
            Console.WriteLine("Database already has tables. Skipping seed.");
        }
    }
}
