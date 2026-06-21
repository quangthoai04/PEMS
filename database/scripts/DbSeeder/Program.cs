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
                using (var verifyCmd = conn.CreateCommand())
                {
                    verifyCmd.CommandText = @"
                        SELECT u.full_name, u.email, u.status, u.sub_role, r.role_code
                        FROM users u
                        JOIN roles r ON r.role_id = u.role_id
                        WHERE r.role_code = 'STAFF'
                        ORDER BY u.email;
                    ";
                    using (var reader = await verifyCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Console.WriteLine($"{reader["email"],-30} | {reader["full_name"],-25} | {reader["sub_role"],-10} | {reader["role_code"]}");
                        }
                    }
                }
                var fixScriptFile = @"..\fix_seed.sql";
                if (File.Exists(fixScriptFile))
                {
                    var fixScriptText = await File.ReadAllTextAsync(fixScriptFile);
                    var fixScript = new MySqlScript(conn, fixScriptText);
                    await fixScript.ExecuteAsync();
                    Console.WriteLine("Successfully executed fix_seed.sql.");
                }

                Console.WriteLine("\n--- DIAGNOSTIC CHECKS ---");
                var queries = new[]
                {
                    ("invalid_operational_instance_without_host", @"SELECT COUNT(*) FROM visit_request_campuses vrc JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id WHERE vr.status = 'APPROVED' AND vrc.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED','CANCELLED') AND vrc.current_host_user_id IS NULL;"),
                    ("invalid_current_host_fk", @"SELECT COUNT(*) FROM visit_request_campuses vrc LEFT JOIN users u ON u.user_id = vrc.current_host_user_id WHERE vrc.current_host_user_id IS NOT NULL AND u.user_id IS NULL;"),
                    ("invalid_single_campus_auto_staff_leader_source", @"SELECT COUNT(*) FROM visit_request_campuses vrc JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id WHERE vr.visit_scope = 'SINGLE_CAMPUS' AND vrc.host_assignment_source = 'AUTO_STAFF_LEADER';"),
                    ("invalid_staff_leader_as_ic_host_participant", @"SELECT COUNT(*) FROM visit_participants vp JOIN users u ON u.user_id = vp.user_id WHERE u.role_id IN (SELECT role_id FROM roles WHERE role_code = 'STAFF') AND UPPER(u.sub_role) = 'LEADER' AND vp.participant_role = 'IC_HOST';"),
                    ("invalid_participant_is_also_current_host", @"SELECT COUNT(*) FROM visit_participants vp JOIN visit_request_campuses vrc ON vrc.visit_instance_id = vp.visit_instance_id WHERE vp.status = 'ACCEPTED' AND vp.is_host = 0 AND vp.user_id = vrc.current_host_user_id;")
                };

                foreach (var q in queries)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = q.Item2;
                    var res = await cmd.ExecuteScalarAsync();
                    Console.WriteLine($"{q.Item1}: {res}");
                }

                using (var debugCmd = conn.CreateCommand())
                {
                    debugCmd.CommandText = "SELECT vrc.visit_instance_id, vrc.status, vrc.campus_id, vr.visit_scope, vr.status AS vr_status, vrc.current_host_user_id FROM visit_request_campuses vrc JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id WHERE vr.status = 'APPROVED' AND vrc.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED','CANCELLED') AND vrc.current_host_user_id IS NULL;";
                    using (var reader = await debugCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Console.WriteLine($"DEBUG MISSING HOST: Instance={reader["visit_instance_id"]}, Campus={reader["campus_id"]}, Scope={reader["visit_scope"]}, InstanceStatus={reader["status"]}");
                        }
                    }
                }
            }
        }
    }
}
