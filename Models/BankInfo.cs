namespace ApexBank.Models;

public class BankInfo
{
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string SwiftCode { get; set; } = string.Empty;
    public string DefaultRoutingNumber { get; set; } = string.Empty;
    public string SupportedChannels { get; set; } = "NPSB,BEFTN,RTGS";
    public string Status { get; set; } = "ACTIVE";
    public string LogoColor { get; set; } = "#10B981";

    public override string ToString() => BankName;
}
