using System;
using ApexBank.ViewModels;

namespace ApexBank.Models;

public class Beneficiary : ViewModelBase
{
    private int _transferCount;

    public string Id { get; set; } = $"ben_{Guid.NewGuid().ToString("N")[..8]}";
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = "BRAC Bank PLC";
    public string RoutingNumber { get; set; } = "060260724";
    public string Channel { get; set; } = "NPSB";
    public string Category { get; set; } = "Personal";
    public string AvatarInitials { get; set; } = string.Empty;
    public string AvatarGradient { get; set; } = "linear-gradient(135deg, #10B981, #059669)";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int TransferCount
    {
        get => _transferCount;
        set => SetProperty(ref _transferCount, value);
    }
}
