using System.Security.Cryptography;
using MobileAICLI.Services;

namespace MobileAICLI;

/// <summary>
/// Utility program to generate password hashes for authentication
/// Usage: dotnet run --project MobileAICLI GeneratePasswordHash <password>
/// </summary>
public class GeneratePasswordHash
{
    public static void Main(string[] args)
    {
        if (args.Length == 0 || args[0] != "GeneratePasswordHash")
        {
            // Not running the utility, skip
            return;
        }

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run --project MobileAICLI GeneratePasswordHash <password>");
            Console.WriteLine();
            Console.WriteLine("This utility generates a password hash for MobileAICLI authentication.");
            Console.WriteLine("Set the generated hash as the MOBILEAICLI_PASSWORD_HASH environment variable.");
            return;
        }

        string password = args[1];
        string hash = AuthService.GeneratePasswordHash(password);
        
        Console.WriteLine("Generated password hash:");
        Console.WriteLine(hash);
        Console.WriteLine();
        Console.WriteLine("To use this hash, set it as an environment variable:");
        Console.WriteLine($"export MOBILEAICLI_PASSWORD_HASH='{hash}'");
        Console.WriteLine();
        Console.WriteLine("Or on Windows:");
        Console.WriteLine($"set MOBILEAICLI_PASSWORD_HASH={hash}");
    }
}
