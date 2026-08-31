// src/Nestify.Web/Services/MoneyFormatterService.cs
namespace Nestify.Web.Services;

/// <summary>
/// Formats decimal values as Bangladeshi Taka (৳) currency.
/// Per §11.6.1: money is decimal everywhere and displays as ৳1,234.56
/// </summary>
public sealed class MoneyFormatterService
{
    private const string Currency = "৳";

    /// <summary>
    /// Formats a decimal amount as Bangladeshi Taka with comma separators.
    /// Example: 12345.67 → "৳12,345.67"
    /// </summary>
    public string Format(decimal amount)
    {
        var formatted = amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        return $"{Currency}{formatted}";
    }

    /// <summary>
    /// Formats a decimal amount without the currency symbol.
    /// Example: 12345.67 → "12,345.67"
    /// Useful for input fields that display the symbol separately.
    /// </summary>
    public string FormatNumber(decimal amount)
    {
        return amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a string input to decimal, removing currency symbols and comma separators.
    /// Handles inputs like "12,345.67", "৳12,345.67", or "12345.67"
    /// </summary>
    public bool TryParse(string input, out decimal result)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            result = 0;
            return false;
        }

        var cleaned = input
            .Replace(Currency, "")
            .Replace(",", "")
            .Trim();

        return decimal.TryParse(cleaned, out result);
    }

    /// <summary>
    /// Formats a decimal amount with abbreviated form (e.g., "৳1.2K", "৳5M").
    /// Used in compact displays where space is limited.
    /// </summary>
    public string FormatCompact(decimal amount)
    {
        return Math.Abs(amount) switch
        {
            >= 1_000_000 => $"{Currency}{amount / 1_000_000:F1}M",
            >= 1_000 => $"{Currency}{amount / 1_000:F1}K",
            _ => Format(amount)
        };
    }

    /// <summary>
    /// Formats a decimal as a percentage with the ৳ symbol.
    /// Example: 0.15 → "৳0.15" (15%)
    /// </summary>
    public string FormatAsPercentage(decimal value)
    {
        return $"{Currency}{value:P2}";
    }
}
