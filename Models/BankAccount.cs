using ApexBank.ViewModels;

namespace ApexBank.Models;

public enum AccountCategory
{
    Checking,
    Savings,
    Investment
}

public class BankAccount : ViewModelBase
{
    private decimal _balance;
    private decimal _monthlyInflow;
    private decimal _monthlyOutflow;
    private bool _isActive;

    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountCategory Category { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = "৳";
    public decimal Apy { get; set; }
    public string AccentColor { get; set; } = "#10B981"; // Emerald default
    public string BadgeText { get; set; } = "Primary";

    public decimal Balance
    {
        get => _balance;
        set => SetProperty(ref _balance, value);
    }

    public decimal MonthlyInflow
    {
        get => _monthlyInflow;
        set => SetProperty(ref _monthlyInflow, value);
    }

    public decimal MonthlyOutflow
    {
        get => _monthlyOutflow;
        set => SetProperty(ref _monthlyOutflow, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
