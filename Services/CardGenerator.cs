using System;
using System.Text;

namespace ApexBank.Services;

/// <summary>
/// Generates realistic, dynamic payment card credentials compliant with ISO/IEC 7812 and the Luhn algorithm.
/// </summary>
public static class CardGenerator
{
    /// <summary>
    /// Generates a valid 16-digit card number with a valid Luhn check digit.
    /// </summary>
    /// <param name="brand">Card brand (e.g. "VISA" or "Mastercard")</param>
    public static string GenerateCardNumber(string brand = "VISA")
    {
        var sb = new StringBuilder();

        if (brand.Equals("Mastercard", StringComparison.OrdinalIgnoreCase))
        {
            // Mastercard standard BIN prefix 52-55
            int prefix = Random.Shared.Next(52, 56);
            sb.Append(prefix);
        }
        else
        {
            // Visa prefix 4 + 3 digits (e.g. 4738 or 4000-4999)
            sb.Append('4');
            sb.Append(Random.Shared.Next(100, 999));
        }

        // Fill remaining digits up to 15 digits
        while (sb.Length < 15)
        {
            sb.Append(Random.Shared.Next(0, 10));
        }

        // Calculate Luhn check digit for the 16th position
        int checkDigit = CalculateLuhnCheckDigit(sb.ToString());
        sb.Append(checkDigit);

        // Format as "XXXX XXXX XXXX XXXX"
        string digits = sb.ToString();
        return $"{digits[..4]} {digits[4..8]} {digits[8..12]} {digits[12..16]}";
    }

    /// <summary>
    /// Generates a dynamic 3-digit card verification value (CVV).
    /// </summary>
    public static string GenerateCvv()
    {
        return Random.Shared.Next(100, 1000).ToString();
    }

    /// <summary>
    /// Generates an expiry date formatted as "MM/yy", defaulting to 4 years from now.
    /// </summary>
    public static string GenerateExpiry(int yearsAhead = 4)
    {
        var date = DateTime.UtcNow.AddYears(yearsAhead);
        return date.ToString("MM/yy");
    }

    /// <summary>
    /// Formats a 16-digit card number into a secure masked display representation:
    /// "4738 •••• •••• 8829"
    /// </summary>
    public static string MaskCardNumber(string fullNumber)
    {
        string digits = fullNumber.Replace(" ", "").Trim();
        if (digits.Length != 16)
        {
            return "•••• •••• •••• ••••";
        }

        return $"{digits[..4]} •••• •••• {digits[12..16]}";
    }

    /// <summary>
    /// Extracts the last 4 digits of a card number.
    /// </summary>
    public static string ExtractLast4(string fullNumber)
    {
        string digits = fullNumber.Replace(" ", "").Trim();
        return digits.Length >= 4 ? digits[^4..] : digits;
    }

    private static int CalculateLuhnCheckDigit(string numberWithoutCheckDigit)
    {
        int sum = 0;
        bool alternate = true;

        for (int i = numberWithoutCheckDigit.Length - 1; i >= 0; i--)
        {
            int n = numberWithoutCheckDigit[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9)
                {
                    n = (n % 10) + 1;
                }
            }
            sum += n;
            alternate = !alternate;
        }

        return (10 - (sum % 10)) % 10;
    }
}
