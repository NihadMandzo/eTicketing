using System.Security.Cryptography;

namespace eTicketing.Services.Helpers;

/// <summary>
/// Password helper using PBKDF2 (RFC2898) for secure password hashing
/// </summary>
public static class PasswordHelper
{
    private const int SaltSize = 32; // 256 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 100000; // OWASP recommendation

    /// <summary>
    /// Creates a password hash and salt using PBKDF2 (RFC2898)
    /// </summary>
    public static void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Lozinka ne može biti prazna", nameof(password));

        // Generate a random salt
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        passwordSalt = Convert.ToBase64String(salt);

        // Generate hash using PBKDF2
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(HashSize);
        passwordHash = Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verifies a password against a stored hash and salt
    /// </summary>
    public static bool VerifyPasswordHash(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
            return false;

        try
        {
            byte[] salt = Convert.FromBase64String(storedSalt);
            byte[] storedHashBytes = Convert.FromBase64String(storedHash);

            // Generate hash for the provided password
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(HashSize);

            // Compare the hashes
            return CryptographicOperations.FixedTimeEquals(hash, storedHashBytes);
        }
        catch
        {
            return false;
        }
    }
}
