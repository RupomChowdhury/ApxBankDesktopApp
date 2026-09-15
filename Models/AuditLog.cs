using System;

namespace ApexBank.Models;

public class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Status { get; set; } = "SUCCESS"; // SUCCESS, FAILED, WARNING
    public string MachineInfo { get; set; } = Environment.MachineName;
}