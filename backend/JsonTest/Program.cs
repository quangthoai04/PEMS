using System;
using MySqlConnector;

class Program
{
    static void Main()
    {
        string connStr = "server=localhost;port=3306;database=pems_db;user=root;password=123456;";
        using var conn = new MySqlConnection(connStr);
        conn.Open();
        
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM partners;", conn);
        var count = cmd.ExecuteScalar();
        Console.WriteLine($"Total partners: {count}");
        
        using var cmd2 = new MySqlCommand("SELECT partner_id, name FROM partners LIMIT 10;", conn);
        using var reader = cmd2.ExecuteReader();
        while(reader.Read()){
            Console.WriteLine($"ID: {reader.GetValue(0)}, Name: {reader.GetValue(1)}");
        }
    }
}
