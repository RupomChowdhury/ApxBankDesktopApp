using System;
using System.Security.Cryptography;

namespace ApexBank.Services;

public static class PasswordHasher
{
    private const int SaltSize = 16; // 128 bits
    private const int KeySize = 32;  // 256 bits
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public static string HashPassword(string password, out string salt)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        salt = Convert.ToBase64String(saltBytes);

        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            HashAlgorithm,
            KeySize);

        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        try
        {
            byte[] saltBytes = Convert.FromBase64String(storedSalt);
            byte[] expectedHash = Convert.FromBase64String(storedHash);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                HashAlgorithm,
                KeySize);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}