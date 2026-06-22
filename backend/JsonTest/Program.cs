using System;
using MySqlConnector;

class Program
{
    static void Main()
    {
        string connStr = "server=localhost;port=3306;database=pems_db;user=root;password=123456;";
        using var conn = new MySqlConnection(connStr);
        conn.Open();
        
        Console.WriteLine("=== Latest Visit Request ===");
        using var cmd1 = new MySqlCommand("SELECT visit_request_id, request_code, registrant_email, registrant_nationality, media_consent_status, status, submitted_at FROM visit_requests ORDER BY created_at DESC LIMIT 1;", conn);
        using var r1 = cmd1.ExecuteReader();
        while(r1.Read()){
            Console.WriteLine($"ID={r1.GetValue(0)} Code={r1.GetValue(1)} Email={r1.GetValue(2)} Nationality={r1.GetValue(3)} Media={r1.GetValue(4)} Status={r1.GetValue(5)} Submitted={r1.GetValue(6)}");
        }
        r1.Close();
        
        Console.WriteLine("\n=== Guest Members ===");
        using var cmd2 = new MySqlCommand("SELECT guest_member_id, full_name, member_type, email, nationality FROM visit_guest_members WHERE visit_request_id = (SELECT MAX(visit_request_id) FROM visit_requests);", conn);
        using var r2 = cmd2.ExecuteReader();
        while(r2.Read()){
            Console.WriteLine($"  {r2.GetValue(0)}: {r2.GetValue(1)} ({r2.GetValue(2)}) email={r2.GetValue(3)} nat={r2.GetValue(4)}");
        }
        r2.Close();
        
        Console.WriteLine("\n=== Campus Instances ===");
        using var cmd3 = new MySqlCommand("SELECT visit_instance_id, campus_id, instance_code, planned_start_at, planned_end_at, status FROM visit_request_campuses WHERE visit_request_id = (SELECT MAX(visit_request_id) FROM visit_requests);", conn);
        using var r3 = cmd3.ExecuteReader();
        while(r3.Read()){
            Console.WriteLine($"  {r3.GetValue(0)}: Campus={r3.GetValue(1)} Code={r3.GetValue(2)} Start={r3.GetValue(3)} End={r3.GetValue(4)} Status={r3.GetValue(5)}");
        }
    }
}
