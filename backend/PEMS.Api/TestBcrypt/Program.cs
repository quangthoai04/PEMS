using System;
public class Program
{
    public static void Main()
    {
        Console.WriteLine("Admin@123456: " + BCrypt.Net.BCrypt.Verify("Admin@123456", "$2a$12$A649uaaQNoePUlyK3hrupOXDX..MgWiR8w6.5ndc62xpPgl.ALQU6"));
        Console.WriteLine("123456: " + BCrypt.Net.BCrypt.Verify("123456", "$2a$12$A649uaaQNoePUlyK3hrupOXDX..MgWiR8w6.5ndc62xpPgl.ALQU6"));
        Console.WriteLine("admin: " + BCrypt.Net.BCrypt.Verify("admin", "$2a$12$A649uaaQNoePUlyK3hrupOXDX..MgWiR8w6.5ndc62xpPgl.ALQU6"));
        Console.WriteLine("Correct Hash for Admin@123: " + BCrypt.Net.BCrypt.HashPassword("Admin@123", 12));
    }
}
