using eTicketing.Services.Helpers;

namespace eTicketing.Services.Helpers;

/// <summary>
/// Helper class to generate password hashes for seeding the database.
/// Usage: Call GeneratePasswordHash method with desired password to get hash and salt.
/// </summary>
public static class PasswordGenerator
{
    public static (string hash, string salt) GeneratePasswordHash(string password)
    {
        PasswordHelper.CreatePasswordHash(password, out string passwordHash, out string passwordSalt);
        return (passwordHash, passwordSalt);
    }

    // Example usage in comments:
    // var (hash, salt) = PasswordGenerator.GeneratePasswordHash("Admin123!");
    // Console.WriteLine($"PasswordHash: {hash}");
    // Console.WriteLine($"PasswordSalt: {salt}");
}

