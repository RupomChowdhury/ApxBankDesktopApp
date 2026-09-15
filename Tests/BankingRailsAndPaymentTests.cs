using System;
using System.IO;
using System.Linq;
using ApexBank.Models;
using ApexBank.Services;
using ClosedXML.Excel;
using Xunit;

namespace Tests;

public class BankingRailsAndPaymentTests
{
    private string GetTempExcelPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ApexBankTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "ApexBankTestDb.xlsx");
    }

    [Fact]
    public void SameBank_InternalTransfer_ChargesZeroFee_AndUpdatesBalances()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var bankService = new BankService(db);

            var login = bankService.Login("alexander", "Apex@2026");
            Assert.True(login.Success);

            var chk = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);
            var sav = bankService.Accounts.First(a => a.Category == AccountCategory.Savings);
            decimal chkBefore = chk.Balance;
            decimal savBefore = sav.Balance;

            decimal transferAmt = 15000.00m;
            var result = bankService.TransferSameBank(chk.Id, sav.Id, transferAmt, "Emergency reserve allocation");

            Assert.True(result.Success);
            Assert.Equal(chkBefore - transferAmt, chk.Balance); // Zero fee!
            Assert.Equal(savBefore + transferAmt, sav.Balance);

            // Verify in Excel sheet
            var dbAccounts = db.GetAccounts(bankService.CurrentUser!.Id);
            var dbChk = dbAccounts.First(a => a.Id == chk.Id);
            var dbSav = dbAccounts.First(a => a.Id == sav.Id);
            Assert.Equal(chkBefore - transferAmt, dbChk.Balance);
            Assert.Equal(savBefore + transferAmt, dbSav.Balance);

            // Check Transactions Sheet
            var txs = db.GetTransactions(bankService.CurrentUser!.Id);
            Assert.Contains(txs, t => t.Amount == -transferAmt && t.Title.Contains("Internal Transfer"));
        }
        finally
        {
            CleanupDir(path);
        }
    }

    [Fact]
    public void SameBank_PeerTransfer_ChargesZeroFee_AndCreditsRecipientInExcel()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var bankService = new BankService(db);

            // 1. Create Recipient Bob
            var regBob = bankService.Register("Bob Ahmed", "bob_ahmed", "bob@apex.bd", "Pass@12345");
            Assert.True(regBob.Success);
            string bobId = bankService.CurrentUser!.Id;
            var bobChecking = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);
            string bobAccNumber = bobChecking.AccountNumber;
            decimal bobBefore = bobChecking.Balance;

            // 2. Login Alexander (Sender)
            bankService.Logout();
            var loginAlex = bankService.Login("alexander", "Apex@2026");
            Assert.True(loginAlex.Success);

            var alexChk = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);
            decimal alexBefore = alexChk.Balance;

            decimal transferAmt = 5000.00m;
            var result = bankService.TransferSameBank(alexChk.Id, bobAccNumber, transferAmt, "Freelance design work");
            Assert.True(result.Success);

            // Sender debited with zero extra fee
            Assert.Equal(alexBefore - transferAmt, alexChk.Balance);

            // Check Excel for Bob (Recipient was credited in DB)
            var bobDbAccs = db.GetAccounts(bobId);
            var bobDbChk = bobDbAccs.First(a => a.Id == bobChecking.Id);
            Assert.Equal(bobBefore + transferAmt, bobDbChk.Balance);

            // Check Transactions for Bob
            var bobTxs = db.GetTransactions(bobId);
            Assert.Contains(bobTxs, t => t.Amount == transferAmt && t.Type == TransactionType.Credit);
        }
        finally
        {
            CleanupDir(path);
        }
    }

    [Fact]
    public void ExternalNpsbTransfer_AppliesCorrectChannelFee_DeductsTotal_AndLogsReference()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var bankService = new BankService(db);

            var login = bankService.Login("alexander", "Apex@2026");
            Assert.True(login.Success);

            var chk = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);
            decimal beforeBal = chk.Balance;

            decimal amount = 20000.00m;
            decimal npsbFee = BankService.GetTransferFee("NPSB"); // ৳10
            Assert.Equal(10.00m, npsbFee);

            var result = bankService.TransferExternalBangladesh(
                sourceAccountId: chk.Id,
                bankName: "BRAC Bank PLC",
                channel: "NPSB",
                recipientAccount: "1501204899128001",
                recipientName: "Rafiqul Islam",
                routingNumber: "060260485",
                amount: amount,
                note: "Vendor payment for office supply",
                saveBeneficiary: true
            );

            Assert.True(result.Success);
            Assert.Equal(beforeBal - (amount + npsbFee), chk.Balance);

            // Check Excel Transactions sheet
            var txs = db.GetTransactions(bankService.CurrentUser!.Id);
            var latestTx = txs.First();
            Assert.Equal(-(amount + npsbFee), latestTx.Amount);
            Assert.StartsWith("NPSB-", latestTx.ReferenceNumber);
            Assert.Contains("BRAC Bank", latestTx.Title);

            // Verify Beneficiary was saved
            var beneficiaries = db.GetBeneficiaries(bankService.CurrentUser!.Id);
            Assert.Contains(beneficiaries, b => b.Name == "Rafiqul Islam" && b.BankName == "BRAC Bank PLC");
        }
        finally
        {
            CleanupDir(path);
        }
    }

    [Fact]
    public void ExternalRtgsTransfer_EnforcesMinimumLimit_AndAppliesFee()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var bankService = new BankService(db);

            var login = bankService.Login("alexander", "Apex@2026");
            Assert.True(login.Success);

            var chk = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);

            // 1. Try RTGS under ৳100,000 -> Should fail per Bangladesh Bank rules
            var failResult = bankService.TransferExternalBangladesh(
                sourceAccountId: chk.Id,
                bankName: "City Bank PLC",
                channel: "RTGS",
                recipientAccount: "1102948210",
                recipientName: "Mustafizur Rahman",
                routingNumber: "225260421",
                amount: 50000m,
                note: "Below limit",
                saveBeneficiary: false);
            Assert.False(failResult.Success);
            Assert.Contains("100,000", failResult.Message);

            // 2. Try RTGS >= ৳100,000 -> Should succeed with ৳100 fee
            decimal beforeBal = chk.Balance;
            decimal rtgsAmount = 120000m;
            decimal rtgsFee = BankService.GetTransferFee("RTGS");
            Assert.Equal(100.00m, rtgsFee);

            var okResult = bankService.TransferExternalBangladesh(
                sourceAccountId: chk.Id,
                bankName: "City Bank PLC",
                channel: "RTGS",
                recipientAccount: "1102948210",
                recipientName: "Mustafizur Rahman",
                routingNumber: "225260421",
                amount: rtgsAmount,
                note: "Corporate settlement",
                saveBeneficiary: false);
            Assert.True(okResult.Success);
            Assert.Equal(beforeBal - (rtgsAmount + rtgsFee), chk.Balance);
        }
        finally
        {
            CleanupDir(path);
        }
    }

    [Fact]
    public void ExternalNpsbTransfer_EnforcesMaxLimit()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var bankService = new BankService(db);

            var login = bankService.Login("alexander", "Apex@2026");
            Assert.True(login.Success);

            var chk = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);

            // NPSB max is ৳300,000 per txn
            var failResult = bankService.TransferExternalBangladesh(
                sourceAccountId: chk.Id,
                bankName: "Dutch-Bangla Bank",
                channel: "NPSB",
                recipientAccount: "11510394821",
                recipientName: "Rezaul Karim",
                routingNumber: "090260412",
                amount: 350000m,
                note: "Too large for NPSB",
                saveBeneficiary: false);
            Assert.False(failResult.Success);
            Assert.Contains("300,000", failResult.Message);
        }
        finally
        {
            CleanupDir(path);
        }
    }

    [Fact]
    public void PayViaCard_UtilityBill_ValidatesSecurity_AndUpdatesDailySpentInExcel()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var bankService = new BankService(db);

            var login = bankService.Login("alexander", "Apex@2026");
            Assert.True(login.Success);

            var card = bankService.Cards.First();
            var chk = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);
            decimal chkBefore = chk.Balance;
            decimal cardSpentBefore = card.DailySpent;

            // 1. Invalid CVV fails
            var invalidCvvResult = bankService.PayViaCard(card.Id, "DESCO Electricity", "1029384756", 2500m, "000", "Utilities", "May 2026 Electricity Bill");
            Assert.False(invalidCvvResult.Success);
            Assert.Contains("CVV", invalidCvvResult.Message);

            // 2. Valid payment
            decimal billAmt = 2500m;
            decimal fee = 10m;
            var okResult = bankService.PayViaCard(card.Id, "DESCO Electricity", "1029384756", billAmt, card.Cvv, "Utilities", "May 2026 Electricity Bill");
            Assert.True(okResult.Success);

            // Linked account debited (bill amount + ৳10 fee)
            Assert.Equal(chkBefore - (billAmt + fee), chk.Balance);

            // Card daily spent updated with purchase amount
            Assert.Equal(cardSpentBefore + billAmt, card.DailySpent);

            // Check Excel Cards sheet
            var dbCards = db.GetCards(bankService.CurrentUser!.Id);
            var dbCard = dbCards.First(c => c.Id == card.Id);
            Assert.Equal(cardSpentBefore + billAmt, dbCard.DailySpent);

            // Check Excel Transactions sheet
            var txs = db.GetTransactions(bankService.CurrentUser!.Id);
            var latest = txs.First();
            Assert.Equal(-(billAmt + fee), latest.Amount);
            Assert.Contains("DESCO", latest.Title);
            Assert.StartsWith("CARD-", latest.ReferenceNumber);
        }
        finally
        {
            CleanupDir(path);
        }
    }

    [Fact]
    public void PayViaCard_BlocksWhenCardFrozenOrEcommerceDisabled()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var bankService = new BankService(db);

            var login = bankService.Login("alexander", "Apex@2026");
            Assert.True(login.Success);

            var card = bankService.Cards.First();

            // Disable online purchases
            bankService.UpdateCardSecuritySettings(card.Id, online: false);
            var disabledResult = bankService.PayViaCard(card.Id, "Dhaka WASA", "WASA-994", 1200m, card.Cvv, "Utilities", "Water bill");
            Assert.False(disabledResult.Success);
            Assert.Contains("disabled", disabledResult.Message, StringComparison.OrdinalIgnoreCase);

            // Re-enable online but freeze card
            bankService.UpdateCardSecuritySettings(card.Id, online: true);
            bankService.ToggleCardFreeze(card.Id);
            var frozenResult = bankService.PayViaCard(card.Id, "Dhaka WASA", "WASA-994", 1200m, card.Cvv, "Utilities", "Water bill");
            Assert.False(frozenResult.Success);
            Assert.Contains("frozen", frozenResult.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupDir(path);
        }
    }

    [Fact]
    public void SupportedBanksSheet_IsCreatedAndPopulatedInExcel()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var banks = db.GetSupportedBanks();

            Assert.True(banks.Count >= 10);
            Assert.Contains(banks, b => b.BankName.Contains("BRAC Bank"));
            Assert.Contains(banks, b => b.BankName.Contains("City Bank"));
            Assert.Contains(banks, b => b.BankName.Contains("Dutch-Bangla"));
            Assert.Contains(banks, b => b.BankName.Contains("bKash"));
            Assert.Contains(banks, b => b.BankName.Contains("Nagad"));

            using var wb = new XLWorkbook(path);
            Assert.True(wb.Worksheets.Contains("SupportedBanks"));
        }
        finally
        {
            CleanupDir(path);
        }
    }

    [Fact]
    public void BeneficiariesSheet_SupportsManualAddAndDeletion()
    {
        string path = GetTempExcelPath();
        try
        {
            var db = new ExcelDatabaseService(path);
            var bankService = new BankService(db);

            var login = bankService.Login("alexander", "Apex@2026");
            Assert.True(login.Success);

            int initialCount = bankService.Beneficiaries.Count;

            var newBen = new Beneficiary
            {
                Id = "ben_test_" + Guid.NewGuid().ToString("N")[..6],
                UserId = bankService.CurrentUser!.Id,
                Name = "Tasnim Rahman",
                BankName = "Eastern Bank PLC",
                AccountNumber = "104108492019",
                RoutingNumber = "095260124",
                Channel = "BEFTN",
                Category = "Family",
                CreatedAt = DateTime.UtcNow
            };

            bankService.AddBeneficiary(newBen);
            Assert.Equal(initialCount + 1, bankService.Beneficiaries.Count);

            // Verify in Excel sheet
            var dbBens = db.GetBeneficiaries(bankService.CurrentUser!.Id);
            Assert.Contains(dbBens, b => b.Id == newBen.Id && b.Name == "Tasnim Rahman");

            // Remove Beneficiary
            bankService.RemoveBeneficiary(newBen.Id);
            Assert.Equal(initialCount, bankService.Beneficiaries.Count);

            var dbBensAfter = db.GetBeneficiaries(bankService.CurrentUser!.Id);
            Assert.DoesNotContain(dbBensAfter, b => b.Id == newBen.Id);
        }
        finally
        {
            CleanupDir(path);
        }
    }

    private void CleanupDir(string filePath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
