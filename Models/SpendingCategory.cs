namespace ApexBank.Models;

public class SpendingCategory
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
    public string ColorHex { get; set; } = "#10B981";
    public string IconKey { get; set; } = "CartIcon";
    public string FormattedAmount => $"${Amount:N2}";
    public string FormattedPercentage => $"{Percentage:F1}%";
}
