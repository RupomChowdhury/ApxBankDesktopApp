using System;

namespace ApexBank.Models;

public class NotificationItem
{
    public string Id { get; set; } = $"notif_{Guid.NewGuid().ToString("N")[..8]}";
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = "Security"; // Security, Transfer, Card, System
    public bool IsRead { get; set; } = false;
    public string IconType { get; set; } = "Bell";
}
