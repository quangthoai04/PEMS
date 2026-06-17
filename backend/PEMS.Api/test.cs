using System;
public class Program
{
    public static void Main()
    {
        bool isValid = BCrypt.Net.BCrypt.Verify("Admin@123", "$2a$12$A649uaaQNoePUlyK3hrupOXDX..MgWiR8w6.5ndc62xpPgl.ALQU6");
        Console.WriteLine("IsValid: " + isValid);
    }
}
