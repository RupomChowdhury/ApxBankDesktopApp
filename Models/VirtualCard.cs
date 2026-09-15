using System;
using ApexBank.ViewModels;

namespace ApexBank.Models;

public class VirtualCard : ViewModelBase
{
    private bool _isFrozen;
    private decimal _dailyLimit = 5000.00m;
    private decimal _dailySpent = 0.00m;
    private bool _contactlessEnabled = true;
    private bool _onlinePurchasesEnabled = true;
    private bool _internationalPaymentsEnabled = true;

    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string CardHolder { get; set; } = string.Empty;
    public string MaskedNumber { get; set; } = string.Empty;
    public string FullNumber { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
    public string Brand { get; set; } = "VISA";
    public string Tier { get; set; } = "Infinite Platinum";
    public bool IsDisposable { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsFrozen
    {
        get => _isFrozen;
        set => SetProperty(ref _isFrozen, value);
    }

    public decimal DailyLimit
    {
        get => _dailyLimit;
        set => SetProperty(ref _dailyLimit, value);
    }

    public decimal DailySpent
    {
        get => _dailySpent;
        set => SetProperty(ref _dailySpent, value);
    }

    public bool ContactlessEnabled
    {
        get => _contactlessEnabled;
        set
        {
            if (SetProperty(ref _contactlessEnabled, value))
            {
                OnPropertyChanged(nameof(AllowContactless));
            }
        }
    }

    public bool AllowContactless
    {
        get => ContactlessEnabled;
        set => ContactlessEnabled = value;
    }

    public bool OnlinePurchasesEnabled
    {
        get => _onlinePurchasesEnabled;
        set
        {
            if (SetProperty(ref _onlinePurchasesEnabled, value))
            {
                OnPropertyChanged(nameof(AllowOnlinePurchases));
            }
        }
    }

    public bool AllowOnlinePurchases
    {
        get => OnlinePurchasesEnabled;
        set => OnlinePurchasesEnabled = value;
    }

    public bool InternationalPaymentsEnabled
    {
        get => _internationalPaymentsEnabled;
        set
        {
            if (SetProperty(ref _internationalPaymentsEnabled, value))
            {
                OnPropertyChanged(nameof(AllowInternational));
            }
        }
    }

    public bool AllowInternational
    {
        get => InternationalPaymentsEnabled;
        set => InternationalPaymentsEnabled = value;
    }
}
