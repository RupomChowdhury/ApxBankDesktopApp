using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ApexBank.Services;

/// <summary>
/// Provides industry-standard AES-256 encryption and decryption for sensitive payment credentials (PAN, CVV)
/// before persisting them into the Excel database sheet.
/// </summary>
public static class EncryptionService
{
    // Secure application secret and salt used to derive a 256-bit AES encryption key via PBKDF2
    private static readonly byte[] SystemSalt = [
        0x41, 0x70, 0x65, 0x78, 0x42, 0x61, 0x6E, 0x6B, 
        0x53, 0x65, 0x63, 0x75, 0x72, 0x65, 0x4B, 0x65
    ];
    private const string SecretPassphrase = "ApexBank-Enterprise-CardVault-Key-2026!#$";
    private static readonly byte[] Key256 = Rfc2898DeriveBytes.Pbkdf2(
        Encoding.UTF8.GetBytes(SecretPassphrase),
        SystemSalt,
        iterations: 10_000,
        HashAlgorithmName.SHA256,
        outputLength: 32);

    private const string EncryptedPrefix = "ENC:v1:";

    /// <summary>
    /// Encrypts sensitive plaintext (such as card number or CVV) using AES-256-CBC with a random 128-bit IV.
    /// Returns a versioned ciphertext string: "ENC:v1:{ivBase64}:{cipherBase64}".
    /// </summary>
    public static string Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] iv = new byte[16];
        RandomNumberGenerator.Fill(iv);

        using var aes = Aes.Create();
        aes.Key = Key256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        byte[] cipherBytes = aes.EncryptCbc(plainBytes, iv, PaddingMode.PKCS7);

        string ivBase64 = Convert.ToBase64String(iv);
        string cipherBase64 = Convert.ToBase64String(cipherBytes);

        return $"{EncryptedPrefix}{ivBase64}:{cipherBase64}";
    }

    /// <summary>
    /// Decrypts a versioned ciphertext string produced by <see cref="Encrypt"/>.
    /// If the string is not encrypted (e.g. legacy or unencrypted text), it is safely returned as-is.
    /// </summary>
    public static string Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return string.Empty;
        }

        if (!cipherText.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            // Fallback for unencrypted strings
            return cipherText;
        }

        try
        {
            string payload = cipherText[EncryptedPrefix.Length..];
            int separatorIndex = payload.IndexOf(':');
            if (separatorIndex <= 0)
            {
                return cipherText;
            }

            string ivBase64 = payload[..separatorIndex];
            string cipherBase64 = payload[(separatorIndex + 1)..];

            byte[] iv = Convert.FromBase64String(ivBase64);
            byte[] cipherBytes = Convert.FromBase64String(cipherBase64);

            using var aes = Aes.Create();
            aes.Key = Key256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] plainBytes = aes.DecryptCbc(cipherBytes, iv, PaddingMode.PKCS7);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // In case of any decryption failure, do not crash the application
            return string.Empty;
        }
    }

    /// <summary>
    /// Computes a standard SHA-256 hex digest of an input string for indexing or fingerprinting.
    /// </summary>
    public static string HashSha256(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
