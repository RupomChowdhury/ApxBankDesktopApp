namespace ApexBank.Models;

public enum TransactionStatus
{
    Completed,
    Pending,
    Failed
}

public enum TransactionType
{
    Debit,   // Money out (expense, transfer out)
    Credit   // Money in (income, refund, deposit)
}

public class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string AccountId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;
    public string ReferenceNumber { get; set; } = $"APX-{Random.Shared.Next(100000, 999999)}";
    public string IconType { get; set; } = "card";
}
