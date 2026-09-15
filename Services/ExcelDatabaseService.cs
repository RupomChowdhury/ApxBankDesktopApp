using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ApexBank.Models;
using ClosedXML.Excel;

namespace ApexBank.Services;

public class ExcelDatabaseService
{
    private static readonly object _fileLock = new();
    private static ExcelDatabaseService? _instance;
    public static ExcelDatabaseService Instance => _instance ??= new ExcelDatabaseService();

    public string FilePath { get; private set; }

    public ExcelDatabaseService(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            FilePath = customPath;
        }
        else
        {
            string rootDir = AppDomain.CurrentDomain.BaseDirectory;
            var dirInfo = new DirectoryInfo(rootDir);
            while (dirInfo != null && !File.Exists(Path.Combine(dirInfo.FullName, "ApexBank.csproj")))
            {
                dirInfo = dirInfo.Parent;
            }

            string baseFolder = dirInfo != null ? dirInfo.FullName : rootDir;
            string dataDir = Path.Combine(baseFolder, "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            FilePath = Path.Combine(dataDir, "ApexBankDatabase.xlsx");
        }

        EnsureDatabaseCreated();
    }

    public void EnsureDatabaseCreated()
    {
        lock (_fileLock)
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(FilePath))
            {
                EnsureCardsSheetExists();
                EnsureSupportedBanksSheetExists();
                EnsureBeneficiariesSheetExists();
                EnsureNotificationsSheetExists();
                return;
            }

            using var workbook = new XLWorkbook();

            // 1. Users Sheet
            var wsUsers = workbook.Worksheets.Add("Users");
            string[] userHeaders = ["UserId", "FullName", "Username", "Email", "PasswordHash", "Salt", "CreatedAt", "LastLogin"];
            SetupHeaderRow(wsUsers, userHeaders);

            // 2. Balances Sheet (Accounts)
            var wsBalances = workbook.Worksheets.Add("Balances");
            string[] balanceHeaders = ["AccountId", "UserId", "Name", "Category", "AccountNumber", "Balance", "CurrencySymbol", "Apy", "MonthlyInflow", "MonthlyOutflow", "AccentColor", "BadgeText", "IsActive"];
            SetupHeaderRow(wsBalances, balanceHeaders);

            // 3. Transactions Sheet
            var wsTransactions = workbook.Worksheets.Add("Transactions");
            string[] txHeaders = ["Id", "AccountId", "UserId", "Timestamp", "Title", "Description", "Category", "Amount", "Type", "IconType", "Status", "ReferenceNumber"];
            SetupHeaderRow(wsTransactions, txHeaders);

            // 4. Logs Sheet
            var wsLogs = workbook.Worksheets.Add("Logs");
            string[] logHeaders = ["LogId", "Timestamp", "UserId", "Username", "Action", "Details", "Status", "MachineInfo"];
            SetupHeaderRow(wsLogs, logHeaders);

            // 5. Cards Sheet
            var wsCards = workbook.Worksheets.Add("Cards");
            string[] cardHeaders = [
                "CardId", "UserId", "AccountId", "CardHolder", "EncryptedCardNumber",
                "EncryptedCvv", "MaskedNumber", "Last4", "Expiry", "Brand", "Tier",
                "IsFrozen", "DailyLimit", "DailySpent", "ContactlessEnabled",
                "OnlinePurchasesEnabled", "InternationalPaymentsEnabled", "IsDisposable", "CreatedAt"
            ];
            SetupHeaderRow(wsCards, cardHeaders);

            // 6. SupportedBanks Sheet
            var wsBanks = workbook.Worksheets.Add("SupportedBanks");
            string[] bankHeaders = ["BankCode", "BankName", "ShortName", "SwiftCode", "DefaultRoutingNumber", "SupportedChannels", "Status", "LogoColor"];
            SetupHeaderRow(wsBanks, bankHeaders);

            // 7. Beneficiaries Sheet
            var wsBeneficiaries = workbook.Worksheets.Add("Beneficiaries");
            string[] benHeaders = ["BeneficiaryId", "UserId", "Name", "Handle", "Email", "AccountNumber", "BankName", "RoutingNumber", "Channel", "Category", "TransferCount", "AvatarInitials", "CreatedAt"];
            SetupHeaderRow(wsBeneficiaries, benHeaders);

            // 8. Notifications Sheet
            var wsNotifications = workbook.Worksheets.Add("Notifications");
            string[] notifHeaders = ["NotificationId", "UserId", "Title", "Message", "Timestamp", "Type", "IsRead", "IconType"];
            SetupHeaderRow(wsNotifications, notifHeaders);

            // Seed default user: Alexander Cross
            string salt = string.Empty;
            string defaultHash = PasswordHasher.HashPassword("Apex@2026", out salt);
            string alexUserId = "usr_alex_01";
            DateTime now = DateTime.UtcNow;

            wsUsers.Row(2).Cell(1).SetValue(alexUserId);
            wsUsers.Row(2).Cell(2).SetValue("Alexander Cross");
            wsUsers.Row(2).Cell(3).SetValue("alexander");
            wsUsers.Row(2).Cell(4).SetValue("alexander.c@apex.io");
            wsUsers.Row(2).Cell(5).SetValue(defaultHash);
            wsUsers.Row(2).Cell(6).SetValue(salt);
            wsUsers.Row(2).Cell(7).SetValue(now.ToString("o"));
            wsUsers.Row(2).Cell(8).SetValue(now.ToString("o"));

            // Seed Balances for Alexander Cross (Denominated in BDT ৳)
            var seedAccounts = new[]
            {
                ("acc_chk_01", alexUserId, "Primary Everyday Checking", "Checking", "**** 4892", 148500.00m, "৳", 0.15m, 84500.00m, 32800.00m, "#10B981", "Primary", true),
                ("acc_sav_02", alexUserId, "High-Yield Growth Reserve", "Savings", "**** 9120", 421200.00m, "৳", 4.85m, 25000.00m, 1200.00m, "#06B6D4", "4.85% APY", false),
                ("acc_vlt_03", alexUserId, "Crypto & Treasury Vault", "Investment", "**** 3301", 94300.00m, "৳", 8.20m, 12000.00m, 4500.00m, "#8B5CF6", "+12.4% MoM", false)
            };

            for (int i = 0; i < seedAccounts.Length; i++)
            {
                var acc = seedAccounts[i];
                var row = wsBalances.Row(i + 2);
                row.Cell(1).SetValue(acc.Item1);
                row.Cell(2).SetValue(acc.Item2);
                row.Cell(3).SetValue(acc.Item3);
                row.Cell(4).SetValue(acc.Item4);
                row.Cell(5).SetValue(acc.Item5);
                row.Cell(6).SetValue(acc.Item6);
                row.Cell(7).SetValue(acc.Item7);
                row.Cell(8).SetValue(acc.Item8);
                row.Cell(9).SetValue(acc.Item9);
                row.Cell(10).SetValue(acc.Item10);
                row.Cell(11).SetValue(acc.Item11);
                row.Cell(12).SetValue(acc.Item12);
                row.Cell(13).SetValue(acc.Item13);
            }

            // Seed Transactions for Alexander Cross (BDT ৳)
            var seedTxs = new[]
            {
                ("tx_01", "acc_chk_01", alexUserId, now.AddHours(-2), "Star Tech & Engineering", "Mechanical keyboard and 4K display accessory", "Shopping", -14500.00m, "Debit", "Bag", "Completed", "APX-849201"),
                ("tx_02", "acc_chk_01", alexUserId, now.AddHours(-14), "Stripe Payout: Apex Solutions", "Consulting engineering monthly compensation", "Income", 125000.00m, "Credit", "Income", "Completed", "APX-849119"),
                ("tx_03", "acc_chk_01", alexUserId, now.AddDays(-1), "Shwapno Supermarket", "Household groceries and organic essentials", "Groceries", -4620.00m, "Debit", "Cart", "Completed", "APX-848772"),
                ("tx_04", "acc_chk_01", alexUserId, now.AddDays(-2), "NPSB: BRAC Bank - Tanvir Hossain", "Shared dinner bill and travel split", "Transfer", -3500.00m, "Debit", "Transfer", "Completed", "NPSB-848102"),
                ("tx_05", "acc_chk_01", alexUserId, now.AddDays(-3), "DESCO Electricity Bill", "Prepaid electricity token payment", "Utilities", -2500.00m, "Debit", "Zap", "Completed", "CARD-847320"),
                ("tx_06", "acc_chk_01", alexUserId, now.AddDays(-4), "Uber Premier Dhaka", "Gulshan 2 to Hazrat Shahjalal Airport transit", "Transport", -850.00m, "Debit", "Car", "Completed", "APX-846991"),
                ("tx_07", "acc_sav_02", alexUserId, now.AddDays(-5), "Monthly High-Yield Dividend", "4.85% APY compounding yield payout", "Income", 2480.00m, "Credit", "Income", "Completed", "APX-845110"),
                ("tx_08", "acc_vlt_03", alexUserId, now.AddDays(-6), "Treasury Yield Payout", "Automated government treasury bond distribution", "Income", 5120.00m, "Credit", "Income", "Completed", "APX-844002")
            };

            for (int i = 0; i < seedTxs.Length; i++)
            {
                var tx = seedTxs[i];
                var row = wsTransactions.Row(i + 2);
                row.Cell(1).SetValue(tx.Item1);
                row.Cell(2).SetValue(tx.Item2);
                row.Cell(3).SetValue(tx.Item3);
                row.Cell(4).SetValue(tx.Item4.ToString("o"));
                row.Cell(5).SetValue(tx.Item5);
                row.Cell(6).SetValue(tx.Item6);
                row.Cell(7).SetValue(tx.Item7);
                row.Cell(8).SetValue(tx.Item8);
                row.Cell(9).SetValue(tx.Item9);
                row.Cell(10).SetValue(tx.Item10);
                row.Cell(11).SetValue(tx.Item11);
                row.Cell(12).SetValue(tx.Item12);
            }

            // Seed Logs
            var rowLog1 = wsLogs.Row(2);
            rowLog1.Cell(1).SetValue("log_01");
            rowLog1.Cell(2).SetValue(now.AddDays(-7).ToString("o"));
            rowLog1.Cell(3).SetValue("SYSTEM");
            rowLog1.Cell(4).SetValue("system");
            rowLog1.Cell(5).SetValue("DATABASE_INIT");
            rowLog1.Cell(6).SetValue("ApexBank single-file multi-sheet BDT database created with Users, Balances, Transactions, Logs, Cards, SupportedBanks, Beneficiaries, and Notifications sheets.");
            rowLog1.Cell(7).SetValue("SUCCESS");
            rowLog1.Cell(8).SetValue(Environment.MachineName);

            var rowLog2 = wsLogs.Row(3);
            rowLog2.Cell(1).SetValue("log_02");
            rowLog2.Cell(2).SetValue(now.ToString("o"));
            rowLog2.Cell(3).SetValue(alexUserId);
            rowLog2.Cell(4).SetValue("alexander");
            rowLog2.Cell(5).SetValue("USER_SEEDED");
            rowLog2.Cell(6).SetValue("Default demo account Alexander Cross provisioned with primary checking, reserve savings, and vault accounts (BDT currency).");
            rowLog2.Cell(7).SetValue("SUCCESS");
            rowLog2.Cell(8).SetValue(Environment.MachineName);

            // Seed Card for Alexander Cross
            SeedDefaultCard(wsCards, alexUserId, "acc_chk_01", "ALEXANDER CROSS", now);

            // Seed Supported Banks in Bangladesh
            SeedSupportedBanks(wsBanks);

            // Seed Beneficiaries for Alexander Cross
            SeedDefaultBeneficiaries(wsBeneficiaries, alexUserId, now);

            // Seed Notifications for Alexander Cross
            SeedDefaultNotifications(wsNotifications, alexUserId, now);

            workbook.SaveAs(FilePath);
        }
    }

    #region Migrations & Sheet Initializers

    private void EnsureCardsSheetExists()
    {
        lock (_fileLock)
        {
            if (!File.Exists(FilePath)) return;

            bool needsCardsSheet = false;
            using (var workbook = OpenReadWorkbook())
            {
                if (!workbook.TryGetWorksheet("Cards", out _))
                {
                    needsCardsSheet = true;
                }
            }

            if (needsCardsSheet)
            {
                ExecuteWrite(workbook =>
                {
                    if (!workbook.TryGetWorksheet("Cards", out var wsCards))
                    {
                        wsCards = workbook.Worksheets.Add("Cards");
                        string[] cardHeaders = [
                            "CardId", "UserId", "AccountId", "CardHolder", "EncryptedCardNumber",
                            "EncryptedCvv", "MaskedNumber", "Last4", "Expiry", "Brand", "Tier",
                            "IsFrozen", "DailyLimit", "DailySpent", "ContactlessEnabled",
                            "OnlinePurchasesEnabled", "InternationalPaymentsEnabled", "IsDisposable", "CreatedAt"
                        ];
                        SetupHeaderRow(wsCards, cardHeaders);

                        var users = GetUsers();
                        var alex = users.FirstOrDefault(u => u.Username.Equals("alexander", StringComparison.OrdinalIgnoreCase));
                        string userId = alex?.Id ?? "usr_alex_01";
                        SeedDefaultCard(wsCards, userId, "acc_chk_01", "ALEXANDER CROSS", DateTime.UtcNow);
                    }
                });
            }
        }
    }

    private void EnsureSupportedBanksSheetExists()
    {
        lock (_fileLock)
        {
            if (!File.Exists(FilePath)) return;

            bool needsBanksSheet = false;
            using (var workbook = OpenReadWorkbook())
            {
                if (!workbook.TryGetWorksheet("SupportedBanks", out _))
                {
                    needsBanksSheet = true;
                }
            }

            if (needsBanksSheet)
            {
                ExecuteWrite(workbook =>
                {
                    if (!workbook.TryGetWorksheet("SupportedBanks", out var wsBanks))
                    {
                        wsBanks = workbook.Worksheets.Add("SupportedBanks");
                        string[] bankHeaders = ["BankCode", "BankName", "ShortName", "SwiftCode", "DefaultRoutingNumber", "SupportedChannels", "Status", "LogoColor"];
                        SetupHeaderRow(wsBanks, bankHeaders);
                        SeedSupportedBanks(wsBanks);
                    }
                });
            }
        }
    }

    private void EnsureBeneficiariesSheetExists()
    {
        lock (_fileLock)
        {
            if (!File.Exists(FilePath)) return;

            bool needsBeneficiariesSheet = false;
            using (var workbook = OpenReadWorkbook())
            {
                if (!workbook.TryGetWorksheet("Beneficiaries", out _))
                {
                    needsBeneficiariesSheet = true;
                }
            }

            if (needsBeneficiariesSheet)
            {
                ExecuteWrite(workbook =>
                {
                    if (!workbook.TryGetWorksheet("Beneficiaries", out var wsBen))
                    {
                        wsBen = workbook.Worksheets.Add("Beneficiaries");
                        string[] benHeaders = ["BeneficiaryId", "UserId", "Name", "Handle", "Email", "AccountNumber", "BankName", "RoutingNumber", "Channel", "Category", "TransferCount", "AvatarInitials", "CreatedAt"];
                        SetupHeaderRow(wsBen, benHeaders);

                        var users = GetUsers();
                        var alex = users.FirstOrDefault(u => u.Username.Equals("alexander", StringComparison.OrdinalIgnoreCase));
                        string userId = alex?.Id ?? "usr_alex_01";
                        SeedDefaultBeneficiaries(wsBen, userId, DateTime.UtcNow);
                    }
                });
            }
        }
    }

    private void EnsureNotificationsSheetExists()
    {
        lock (_fileLock)
        {
            if (!File.Exists(FilePath)) return;

            bool needsNotifSheet = false;
            using (var workbook = OpenReadWorkbook())
            {
                if (!workbook.TryGetWorksheet("Notifications", out _))
                {
                    needsNotifSheet = true;
                }
            }

            if (needsNotifSheet)
            {
                ExecuteWrite(workbook =>
                {
                    if (!workbook.TryGetWorksheet("Notifications", out var wsNotif))
                    {
                        wsNotif = workbook.Worksheets.Add("Notifications");
                        string[] notifHeaders = ["NotificationId", "UserId", "Title", "Message", "Timestamp", "Type", "IsRead", "IconType"];
                        SetupHeaderRow(wsNotif, notifHeaders);

                        var users = GetUsers();
                        var alex = users.FirstOrDefault(u => u.Username.Equals("alexander", StringComparison.OrdinalIgnoreCase));
                        string userId = alex?.Id ?? "usr_alex_01";
                        SeedDefaultNotifications(wsNotif, userId, DateTime.UtcNow);
                    }
                });
            }
        }
    }

    private static void SeedDefaultCard(IXLWorksheet wsCards, string userId, string accountId, string cardHolder, DateTime now)
    {
        int nextRow = (wsCards.LastRowUsed()?.RowNumber() ?? 1) + 1;
        var row = wsCards.Row(nextRow);
        row.Cell(1).SetValue("card_alex_01");
        row.Cell(2).SetValue(userId);
        row.Cell(3).SetValue(accountId);
        row.Cell(4).SetValue(cardHolder);
        row.Cell(5).SetValue(EncryptionService.Encrypt("4738 9201 5542 8829"));
        row.Cell(6).SetValue(EncryptionService.Encrypt("749"));
        row.Cell(7).SetValue("4738 •••• •••• 8829");
        row.Cell(8).SetValue("8829");
        row.Cell(9).SetValue("09/29");
        row.Cell(10).SetValue("VISA");
        row.Cell(11).SetValue("Infinite Platinum");
        row.Cell(12).SetValue(false);
        row.Cell(13).SetValue(100000.00m);
        row.Cell(14).SetValue(14500.00m);
        row.Cell(15).SetValue(true);
        row.Cell(16).SetValue(true);
        row.Cell(17).SetValue(true);
        row.Cell(18).SetValue(false);
        row.Cell(19).SetValue(now.ToString("o"));
    }

    private static void SeedSupportedBanks(IXLWorksheet ws)
    {
        var banks = new[]
        {
            ("BRAC", "BRAC Bank PLC", "BRAC Bank", "BRACBDDH", "060260724", "NPSB,BEFTN,RTGS", "ACTIVE", "#10B981"),
            ("CITY", "The City Bank PLC", "City Bank", "CIBLBDDH", "225262531", "NPSB,BEFTN,RTGS,VISA", "ACTIVE", "#06B6D4"),
            ("DBBL", "Dutch-Bangla Bank PLC", "DBBL", "DBBLBDDH", "090263414", "NPSB,BEFTN,RTGS,MFS", "ACTIVE", "#F59E0B"),
            ("EBL", "Eastern Bank PLC", "EBL", "EBLBBDDH", "095261762", "NPSB,BEFTN,RTGS,VISA", "ACTIVE", "#6366F1"),
            ("IBBL", "Islami Bank Bangladesh PLC", "IBBL", "IBBLBDDH", "125272648", "NPSB,BEFTN,RTGS", "ACTIVE", "#10B981"),
            ("SCB", "Standard Chartered Bangladesh", "SCB", "SCBLBDDX", "215260517", "NPSB,BEFTN,RTGS,VISA", "ACTIVE", "#3B82F6"),
            ("MTB", "Mutual Trust Bank PLC", "MTB", "MTBLBDDH", "145262704", "NPSB,BEFTN,RTGS", "ACTIVE", "#EC4899"),
            ("PBL", "Prime Bank PLC", "Prime Bank", "PRBLBDDH", "170261314", "NPSB,BEFTN,RTGS", "ACTIVE", "#8B5CF6"),
            ("UCB", "United Commercial Bank PLC", "UCB", "UCBLBDDH", "245261805", "NPSB,BEFTN,RTGS", "ACTIVE", "#F43F5E"),
            ("PUB", "Pubali Bank PLC", "Pubali Bank", "PUBLBDDH", "175273187", "NPSB,BEFTN,RTGS", "ACTIVE", "#059669"),
            ("DHB", "Dhaka Bank PLC", "Dhaka Bank", "DHBLBDDH", "085261453", "NPSB,BEFTN,RTGS", "ACTIVE", "#0284C7"),
            ("BKASH", "bKash Mobile Financial Services", "bKash", "BKASHBDD", "060269999", "MFS,NPSB", "ACTIVE", "#E11D48"),
            ("NAGAD", "Nagad Digital Financial Services", "Nagad", "NAGADBDD", "090268888", "MFS", "ACTIVE", "#D97706")
        };

        for (int i = 0; i < banks.Length; i++)
        {
            var b = banks[i];
            var row = ws.Row(i + 2);
            row.Cell(1).SetValue(b.Item1);
            row.Cell(2).SetValue(b.Item2);
            row.Cell(3).SetValue(b.Item3);
            row.Cell(4).SetValue(b.Item4);
            row.Cell(5).SetValue(b.Item5);
            row.Cell(6).SetValue(b.Item6);
            row.Cell(7).SetValue(b.Item7);
            row.Cell(8).SetValue(b.Item8);
        }
    }

    private static void SeedDefaultBeneficiaries(IXLWorksheet ws, string userId, DateTime now)
    {
        var payees = new[]
        {
            ("ben_01", userId, "Tanvir Hossain", "@tanvir.h", "tanvir.h@brac.com", "1501204892011001", "BRAC Bank PLC", "060260724", "NPSB", "Personal", 18, "TH", now.AddDays(-20)),
            ("ben_02", userId, "Nusrat Jahan", "@nusrat.j", "nusrat.j@city.com", "1102948201948", "The City Bank PLC", "225262531", "NPSB", "Personal", 12, "NJ", now.AddDays(-15)),
            ("ben_03", userId, "Rahim Chowdhury", "@rahim.c", "rahim.c@techbd.io", "01711998822", "bKash Mobile Financial Services", "060269999", "MFS", "Business", 9, "RC", now.AddDays(-10)),
            ("ben_04", userId, "Farhana Ahmed", "@farhana.a", "farhana.a@ebl.com", "1041070284911", "Eastern Bank PLC", "095261762", "BEFTN", "Personal", 6, "FA", now.AddDays(-5)),
            ("ben_05", userId, "Kazi Mahmud", "@kazi.m", "kazi.m@dbbl.com", "1161200394851", "Dutch-Bangla Bank PLC", "090263414", "RTGS", "Business", 4, "KM", now.AddDays(-2))
        };

        for (int i = 0; i < payees.Length; i++)
        {
            var p = payees[i];
            var row = ws.Row(i + 2);
            row.Cell(1).SetValue(p.Item1);
            row.Cell(2).SetValue(p.Item2);
            row.Cell(3).SetValue(p.Item3);
            row.Cell(4).SetValue(p.Item4);
            row.Cell(5).SetValue(p.Item5);
            row.Cell(6).SetValue(p.Item6);
            row.Cell(7).SetValue(p.Item7);
            row.Cell(8).SetValue(p.Item8);
            row.Cell(9).SetValue(p.Item9);
            row.Cell(10).SetValue(p.Item10);
            row.Cell(11).SetValue(p.Item11);
            row.Cell(12).SetValue(p.Item12);
            row.Cell(13).SetValue(p.Item13.ToString("o"));
        }
    }

    private static void SeedDefaultNotifications(IXLWorksheet ws, string userId, DateTime now)
    {
        var notifs = new[]
        {
            ("notif_01", userId, "Security Shield Active", "Two-factor hardware authorization and AES-256 ledger encryption are active.", now.AddMinutes(-10), "Security", false, "IconSecurity"),
            ("notif_02", userId, "NPSB Network Online", "Bangladesh Bank National Payment Switch Bangladesh (NPSB) 24/7 instant interbank network is operating normally.", now.AddHours(-1), "Transfer", false, "IconTransfer"),
            ("notif_03", userId, "Card Headroom Verified", "Virtual card daily spending limit verified. Instant utility bill and merchant checkouts ready.", now.AddHours(-3), "Card", false, "IconCards")
        };

        for (int i = 0; i < notifs.Length; i++)
        {
            var n = notifs[i];
            var row = ws.Row(i + 2);
            row.Cell(1).SetValue(n.Item1);
            row.Cell(2).SetValue(n.Item2);
            row.Cell(3).SetValue(n.Item3);
            row.Cell(4).SetValue(n.Item4);
            row.Cell(5).SetValue(n.Item5.ToString("o"));
            row.Cell(6).SetValue(n.Item6);
            row.Cell(7).SetValue(n.Item7);
            row.Cell(8).SetValue(n.Item8);
        }
    }

    private static void SetupHeaderRow(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.SetValue(headers[i]);
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(16, 185, 129); // Emerald
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Columns().AdjustToContents();
    }

    private XLWorkbook OpenReadWorkbook()
    {
        int retries = 6;
        int delayMs = 60;
        for (int i = 0; i < retries; i++)
        {
            try
            {
                using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var ms = new MemoryStream();
                fs.CopyTo(ms);
                ms.Position = 0;
                return new XLWorkbook(ms);
            }
            catch (IOException) when (i < retries - 1)
            {
                System.Threading.Thread.Sleep(delayMs);
            }
        }
        return new XLWorkbook(FilePath);
    }

    private void ExecuteWrite(Action<XLWorkbook> action)
    {
        lock (_fileLock)
        {
            int retries = 6;
            int delayMs = 60;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    byte[] fileBytes;
                    using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using var inMs = new MemoryStream();
                        fs.CopyTo(inMs);
                        fileBytes = inMs.ToArray();
                    }

                    using var workMs = new MemoryStream();
                    workMs.Write(fileBytes, 0, fileBytes.Length);
                    workMs.Position = 0;

                    using var workbook = new XLWorkbook(workMs);
                    action(workbook);

                    using var outMs = new MemoryStream();
                    workbook.SaveAs(outMs);
                    byte[] outBytes = outMs.ToArray();

                    using (var outFs = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        outFs.Write(outBytes, 0, outBytes.Length);
                    }
                    return;
                }
                catch (IOException) when (i < retries - 1)
                {
                    System.Threading.Thread.Sleep(delayMs);
                }
            }

            using var fallbackWb = new XLWorkbook(FilePath);
            action(fallbackWb);
            fallbackWb.Save();
        }
    }

    #endregion

    #region Users Sheet Operations

    public List<User> GetUsers()
    {
        lock (_fileLock)
        {
            var users = new List<User>();
            if (!File.Exists(FilePath)) return users;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("Users", out var ws)) return users;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string id = row.Cell(1).GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                var user = new User
                {
                    Id = id,
                    FullName = row.Cell(2).GetString(),
                    Username = row.Cell(3).GetString(),
                    Email = row.Cell(4).GetString(),
                    PasswordHash = row.Cell(5).GetString(),
                    Salt = row.Cell(6).GetString(),
                    CreatedAt = DateTime.TryParse(row.Cell(7).GetString(), null, DateTimeStyles.RoundtripKind, out var created) ? created : DateTime.UtcNow,
                    LastLogin = DateTime.TryParse(row.Cell(8).GetString(), null, DateTimeStyles.RoundtripKind, out var lastLogin) ? lastLogin : null
                };
                users.Add(user);
            }
            return users;
        }
    }

    public User? GetUserByUsernameOrEmail(string identifier)
    {
        var users = GetUsers();
        string clean = identifier.Trim();
        return users.FirstOrDefault(u =>
            u.Username.Equals(clean, StringComparison.OrdinalIgnoreCase) ||
            u.Email.Equals(clean, StringComparison.OrdinalIgnoreCase));
    }

    public void InsertUser(User user)
    {
        ExecuteWrite(workbook =>
        {
            var ws = workbook.Worksheet("Users");
            int nextRow = (ws.LastRowUsed()?.RowNumber() ?? 1) + 1;

            var row = ws.Row(nextRow);
            row.Cell(1).SetValue(user.Id);
            row.Cell(2).SetValue(user.FullName);
            row.Cell(3).SetValue(user.Username);
            row.Cell(4).SetValue(user.Email);
            row.Cell(5).SetValue(user.PasswordHash);
            row.Cell(6).SetValue(user.Salt);
            row.Cell(7).SetValue(user.CreatedAt.ToString("o"));
            row.Cell(8).SetValue(user.LastLogin.HasValue ? user.LastLogin.Value.ToString("o") : string.Empty);
        });
    }

    public void UpdateUserLastLogin(string userId, DateTime lastLogin)
    {
        ExecuteWrite(workbook =>
        {
            var ws = workbook.Worksheet("Users");
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.Cell(1).GetString().Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    row.Cell(8).SetValue(lastLogin.ToString("o"));
                    return;
                }
            }
        });
    }

    #endregion

    #region Balances Sheet Operations

    public List<BankAccount> GetAccounts(string userId)
    {
        lock (_fileLock)
        {
            var accounts = new List<BankAccount>();
            if (!File.Exists(FilePath)) return accounts;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("Balances", out var ws)) return accounts;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string id = row.Cell(1).GetString();
                string rowUserId = row.Cell(2).GetString();
                if (string.IsNullOrWhiteSpace(id) || !rowUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Enum.TryParse<AccountCategory>(row.Cell(4).GetString(), true, out var category);

                var acc = new BankAccount
                {
                    Id = id,
                    UserId = rowUserId,
                    Name = row.Cell(3).GetString(),
                    Category = category,
                    AccountNumber = row.Cell(5).GetString(),
                    Balance = ConvertToDecimal(row.Cell(6).Value),
                    CurrencySymbol = string.IsNullOrWhiteSpace(row.Cell(7).GetString()) ? "৳" : row.Cell(7).GetString(),
                    Apy = ConvertToDecimal(row.Cell(8).Value),
                    MonthlyInflow = ConvertToDecimal(row.Cell(9).Value),
                    MonthlyOutflow = ConvertToDecimal(row.Cell(10).Value),
                    AccentColor = row.Cell(11).GetString(),
                    BadgeText = row.Cell(12).GetString(),
                    IsActive = row.Cell(13).GetBoolean()
                };
                accounts.Add(acc);
            }
            return accounts;
        }
    }

    public BankAccount? FindAccountByNumber(string accountNumber)
    {
        lock (_fileLock)
        {
            if (!File.Exists(FilePath)) return null;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("Balances", out var ws)) return null;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            string cleanTarget = accountNumber.Replace("-", "").Trim();
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string accId = row.Cell(1).GetString();
                string accNum = row.Cell(5).GetString();
                string cleanRowAcc = accNum.Replace("-", "").Trim();

                if (cleanRowAcc.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
                    accNum.Equals(accountNumber.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    accId.Equals(accountNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    Enum.TryParse<AccountCategory>(row.Cell(4).GetString(), true, out var category);
                    return new BankAccount
                    {
                        Id = accId,
                        UserId = row.Cell(2).GetString(),
                        Name = row.Cell(3).GetString(),
                        Category = category,
                        AccountNumber = accNum,
                        Balance = ConvertToDecimal(row.Cell(6).Value),
                        CurrencySymbol = string.IsNullOrWhiteSpace(row.Cell(7).GetString()) ? "৳" : row.Cell(7).GetString(),
                        Apy = ConvertToDecimal(row.Cell(8).Value),
                        MonthlyInflow = ConvertToDecimal(row.Cell(9).Value),
                        MonthlyOutflow = ConvertToDecimal(row.Cell(10).Value),
                        AccentColor = row.Cell(11).GetString(),
                        BadgeText = row.Cell(12).GetString(),
                        IsActive = row.Cell(13).GetBoolean()
                    };
                }
            }
            return null;
        }
    }

    public void InsertAccount(BankAccount account)
    {
        ExecuteWrite(workbook =>
        {
            var ws = workbook.Worksheet("Balances");
            int nextRow = (ws.LastRowUsed()?.RowNumber() ?? 1) + 1;

            var row = ws.Row(nextRow);
            row.Cell(1).SetValue(account.Id);
            row.Cell(2).SetValue(account.UserId);
            row.Cell(3).SetValue(account.Name);
            row.Cell(4).SetValue(account.Category.ToString());
            row.Cell(5).SetValue(account.AccountNumber);
            row.Cell(6).SetValue(account.Balance);
            row.Cell(7).SetValue(account.CurrencySymbol);
            row.Cell(8).SetValue(account.Apy);
            row.Cell(9).SetValue(account.MonthlyInflow);
            row.Cell(10).SetValue(account.MonthlyOutflow);
            row.Cell(11).SetValue(account.AccentColor);
            row.Cell(12).SetValue(account.BadgeText);
            row.Cell(13).SetValue(account.IsActive);
        });
    }

    public void UpdateAccountBalance(string accountId, decimal newBalance, decimal inflow, decimal outflow, bool isActive)
    {
        ExecuteWrite(workbook =>
        {
            var ws = workbook.Worksheet("Balances");
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.Cell(1).GetString().Equals(accountId, StringComparison.OrdinalIgnoreCase))
                {
                    row.Cell(6).SetValue(newBalance);
                    row.Cell(9).SetValue(inflow);
                    row.Cell(10).SetValue(outflow);
                    row.Cell(13).SetValue(isActive);
                    return;
                }
            }
        });
    }

    #endregion

    #region Transactions Sheet Operations

    public List<Transaction> GetTransactions(string userId)
    {
        lock (_fileLock)
        {
            var list = new List<Transaction>();
            if (!File.Exists(FilePath)) return list;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("Transactions", out var ws)) return list;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string id = row.Cell(1).GetString();
                string rowUserId = row.Cell(3).GetString();
                if (string.IsNullOrWhiteSpace(id) || (!string.IsNullOrEmpty(userId) && !rowUserId.Equals(userId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                Enum.TryParse<TransactionType>(row.Cell(9).GetString(), true, out var txType);
                Enum.TryParse<TransactionStatus>(row.Cell(11).GetString(), true, out var txStatus);
                DateTime.TryParse(row.Cell(4).GetString(), null, DateTimeStyles.RoundtripKind, out var timestamp);

                var tx = new Transaction
                {
                    Id = id,
                    AccountId = row.Cell(2).GetString(),
                    UserId = rowUserId,
                    Timestamp = timestamp,
                    Title = row.Cell(5).GetString(),
                    Description = row.Cell(6).GetString(),
                    Category = row.Cell(7).GetString(),
                    Amount = ConvertToDecimal(row.Cell(8).Value),
                    Type = txType,
                    IconType = row.Cell(10).GetString(),
                    Status = txStatus,
                    ReferenceNumber = row.Cell(12).GetString()
                };
                list.Add(tx);
            }
            return list.OrderByDescending(t => t.Timestamp).ToList();
        }
    }

    public void InsertTransaction(Transaction tx)
    {
        ExecuteWrite(workbook =>
        {
            var ws = workbook.Worksheet("Transactions");
            int nextRow = (ws.LastRowUsed()?.RowNumber() ?? 1) + 1;

            var row = ws.Row(nextRow);
            row.Cell(1).SetValue(tx.Id);
            row.Cell(2).SetValue(tx.AccountId);
            row.Cell(3).SetValue(tx.UserId);
            row.Cell(4).SetValue(tx.Timestamp.ToString("o"));
            row.Cell(5).SetValue(tx.Title);
            row.Cell(6).SetValue(tx.Description);
            row.Cell(7).SetValue(tx.Category);
            row.Cell(8).SetValue(tx.Amount);
            row.Cell(9).SetValue(tx.Type.ToString());
            row.Cell(10).SetValue(tx.IconType);
            row.Cell(11).SetValue(tx.Status.ToString());
            row.Cell(12).SetValue(tx.ReferenceNumber);
        });
    }

    #endregion

    #region Logs Sheet Operations

    public List<AuditLog> GetLogs(string? userId = null)
    {
        lock (_fileLock)
        {
            var list = new List<AuditLog>();
            if (!File.Exists(FilePath)) return list;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("Logs", out var ws)) return list;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string id = row.Cell(1).GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                string rowUserId = row.Cell(3).GetString();
                if (!string.IsNullOrEmpty(userId) && !rowUserId.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) && !rowUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DateTime.TryParse(row.Cell(2).GetString(), null, DateTimeStyles.RoundtripKind, out var timestamp);

                var log = new AuditLog
                {
                    Id = id,
                    Timestamp = timestamp,
                    UserId = rowUserId,
                    Username = row.Cell(4).GetString(),
                    Action = row.Cell(5).GetString(),
                    Details = row.Cell(6).GetString(),
                    Status = row.Cell(7).GetString(),
                    MachineInfo = row.Cell(8).GetString()
                };
                list.Add(log);
            }
            return list.OrderByDescending(l => l.Timestamp).ToList();
        }
    }

    public void InsertLog(AuditLog log)
    {
        ExecuteWrite(workbook =>
        {
            var ws = workbook.Worksheet("Logs");
            int nextRow = (ws.LastRowUsed()?.RowNumber() ?? 1) + 1;

            var row = ws.Row(nextRow);
            row.Cell(1).SetValue(log.Id);
            row.Cell(2).SetValue(log.Timestamp.ToString("o"));
            row.Cell(3).SetValue(log.UserId);
            row.Cell(4).SetValue(log.Username);
            row.Cell(5).SetValue(log.Action);
            row.Cell(6).SetValue(log.Details);
            row.Cell(7).SetValue(log.Status);
            row.Cell(8).SetValue(log.MachineInfo);
        });
    }

    #endregion

    #region Cards Sheet Operations

    public List<VirtualCard> GetCards(string? userId = null)
    {
        lock (_fileLock)
        {
            var cards = new List<VirtualCard>();
            if (!File.Exists(FilePath)) return cards;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("Cards", out var ws)) return cards;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string cardId = row.Cell(1).GetString();
                if (string.IsNullOrWhiteSpace(cardId)) continue;

                string rowUserId = row.Cell(2).GetString();
                if (!string.IsNullOrEmpty(userId) && !rowUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string encNumber = row.Cell(5).GetString();
                string encCvv = row.Cell(6).GetString();
                string decryptedNumber = EncryptionService.Decrypt(encNumber);
                string decryptedCvv = EncryptionService.Decrypt(encCvv);

                var card = new VirtualCard
                {
                    Id = cardId,
                    UserId = rowUserId,
                    AccountId = row.Cell(3).GetString(),
                    CardHolder = row.Cell(4).GetString(),
                    FullNumber = decryptedNumber,
                    Cvv = decryptedCvv,
                    MaskedNumber = row.Cell(7).GetString(),
                    Last4 = row.Cell(8).GetString(),
                    Expiry = row.Cell(9).GetString(),
                    Brand = row.Cell(10).GetString(),
                    Tier = row.Cell(11).GetString(),
                    IsFrozen = bool.TryParse(row.Cell(12).GetString(), out var frozen) && frozen,
                    DailyLimit = decimal.TryParse(row.Cell(13).GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var limit) ? limit : 100000.00m,
                    DailySpent = decimal.TryParse(row.Cell(14).GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var spent) ? spent : 0.00m,
                    ContactlessEnabled = !bool.TryParse(row.Cell(15).GetString(), out var contactless) || contactless,
                    OnlinePurchasesEnabled = !bool.TryParse(row.Cell(16).GetString(), out var online) || online,
                    InternationalPaymentsEnabled = !bool.TryParse(row.Cell(17).GetString(), out var intl) || intl,
                    IsDisposable = bool.TryParse(row.Cell(18).GetString(), out var disp) && disp,
                    CreatedAt = DateTime.TryParse(row.Cell(19).GetString(), null, DateTimeStyles.RoundtripKind, out var created) ? created : DateTime.UtcNow
                };

                if (string.IsNullOrWhiteSpace(card.MaskedNumber) && !string.IsNullOrWhiteSpace(card.FullNumber))
                {
                    card.MaskedNumber = CardGenerator.MaskCardNumber(card.FullNumber);
                }
                if (string.IsNullOrWhiteSpace(card.Last4) && !string.IsNullOrWhiteSpace(card.FullNumber))
                {
                    card.Last4 = CardGenerator.ExtractLast4(card.FullNumber);
                }

                cards.Add(card);
            }

            return cards;
        }
    }

    public void InsertCard(VirtualCard card)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Cards", out var ws))
            {
                ws = workbook.Worksheets.Add("Cards");
                string[] cardHeaders = [
                    "CardId", "UserId", "AccountId", "CardHolder", "EncryptedCardNumber",
                    "EncryptedCvv", "MaskedNumber", "Last4", "Expiry", "Brand", "Tier",
                    "IsFrozen", "DailyLimit", "DailySpent", "ContactlessEnabled",
                    "OnlinePurchasesEnabled", "InternationalPaymentsEnabled", "IsDisposable", "CreatedAt"
                ];
                SetupHeaderRow(ws, cardHeaders);
            }

            int nextRow = (ws.LastRowUsed()?.RowNumber() ?? 1) + 1;
            var row = ws.Row(nextRow);

            row.Cell(1).SetValue(card.Id);
            row.Cell(2).SetValue(card.UserId);
            row.Cell(3).SetValue(card.AccountId);
            row.Cell(4).SetValue(card.CardHolder);
            row.Cell(5).SetValue(EncryptionService.Encrypt(card.FullNumber));
            row.Cell(6).SetValue(EncryptionService.Encrypt(card.Cvv));
            row.Cell(7).SetValue(card.MaskedNumber);
            row.Cell(8).SetValue(card.Last4);
            row.Cell(9).SetValue(card.Expiry);
            row.Cell(10).SetValue(card.Brand);
            row.Cell(11).SetValue(card.Tier);
            row.Cell(12).SetValue(card.IsFrozen);
            row.Cell(13).SetValue(card.DailyLimit);
            row.Cell(14).SetValue(card.DailySpent);
            row.Cell(15).SetValue(card.ContactlessEnabled);
            row.Cell(16).SetValue(card.OnlinePurchasesEnabled);
            row.Cell(17).SetValue(card.InternationalPaymentsEnabled);
            row.Cell(18).SetValue(card.IsDisposable);
            row.Cell(19).SetValue(card.CreatedAt.ToString("o"));
        });
    }

    public void UpdateCardFreeze(string cardId, bool isFrozen)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Cards", out var ws)) return;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.Cell(1).GetString().Equals(cardId, StringComparison.OrdinalIgnoreCase))
                {
                    row.Cell(12).SetValue(isFrozen);
                    break;
                }
            }
        });
    }

    public void UpdateCardSettings(string cardId, decimal dailyLimit, bool contactless, bool online, bool international)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Cards", out var ws)) return;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.Cell(1).GetString().Equals(cardId, StringComparison.OrdinalIgnoreCase))
                {
                    row.Cell(13).SetValue(dailyLimit);
                    row.Cell(15).SetValue(contactless);
                    row.Cell(16).SetValue(online);
                    row.Cell(17).SetValue(international);
                    break;
                }
            }
        });
    }

    public void UpdateCardSpent(string cardId, decimal additionalSpent)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Cards", out var ws)) return;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.Cell(1).GetString().Equals(cardId, StringComparison.OrdinalIgnoreCase))
                {
                    decimal currentSpent = decimal.TryParse(row.Cell(14).GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cs) ? cs : 0m;
                    row.Cell(14).SetValue(currentSpent + additionalSpent);
                    break;
                }
            }
        });
    }

    #endregion

    #region SupportedBanks Sheet Operations

    public List<BankInfo> GetSupportedBanks()
    {
        lock (_fileLock)
        {
            var banks = new List<BankInfo>();
            if (!File.Exists(FilePath)) return banks;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("SupportedBanks", out var ws)) return banks;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string code = row.Cell(1).GetString();
                string name = row.Cell(2).GetString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                banks.Add(new BankInfo
                {
                    BankCode = code,
                    BankName = name,
                    ShortName = row.Cell(3).GetString(),
                    SwiftCode = row.Cell(4).GetString(),
                    DefaultRoutingNumber = row.Cell(5).GetString(),
                    SupportedChannels = row.Cell(6).GetString(),
                    Status = row.Cell(7).GetString(),
                    LogoColor = row.Cell(8).GetString()
                });
            }
            return banks;
        }
    }

    #endregion

    #region Beneficiaries Sheet Operations

    public List<Beneficiary> GetBeneficiaries(string? userId = null)
    {
        lock (_fileLock)
        {
            var list = new List<Beneficiary>();
            if (!File.Exists(FilePath)) return list;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("Beneficiaries", out var ws)) return list;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string benId = row.Cell(1).GetString();
                if (string.IsNullOrWhiteSpace(benId)) continue;

                string rowUserId = row.Cell(2).GetString();
                if (!string.IsNullOrEmpty(userId) && !rowUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int.TryParse(row.Cell(11).GetString(), out var count);
                DateTime.TryParse(row.Cell(13).GetString(), null, DateTimeStyles.RoundtripKind, out var created);

                var ben = new Beneficiary
                {
                    Id = benId,
                    UserId = rowUserId,
                    Name = row.Cell(3).GetString(),
                    Handle = row.Cell(4).GetString(),
                    Email = row.Cell(5).GetString(),
                    AccountNumber = row.Cell(6).GetString(),
                    BankName = row.Cell(7).GetString(),
                    RoutingNumber = row.Cell(8).GetString(),
                    Channel = row.Cell(9).GetString(),
                    Category = row.Cell(10).GetString(),
                    TransferCount = count,
                    AvatarInitials = row.Cell(12).GetString(),
                    CreatedAt = created != default ? created : DateTime.UtcNow
                };

                if (string.IsNullOrWhiteSpace(ben.AvatarInitials) && !string.IsNullOrWhiteSpace(ben.Name))
                {
                    var parts = ben.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    ben.AvatarInitials = parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant() : parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
                }

                list.Add(ben);
            }

            return list.OrderByDescending(b => b.TransferCount).ToList();
        }
    }

    public void InsertBeneficiary(Beneficiary b)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Beneficiaries", out var ws))
            {
                ws = workbook.Worksheets.Add("Beneficiaries");
                string[] benHeaders = ["BeneficiaryId", "UserId", "Name", "Handle", "Email", "AccountNumber", "BankName", "RoutingNumber", "Channel", "Category", "TransferCount", "AvatarInitials", "CreatedAt"];
                SetupHeaderRow(ws, benHeaders);
            }

            int nextRow = (ws.LastRowUsed()?.RowNumber() ?? 1) + 1;
            var row = ws.Row(nextRow);

            row.Cell(1).SetValue(b.Id);
            row.Cell(2).SetValue(b.UserId);
            row.Cell(3).SetValue(b.Name);
            row.Cell(4).SetValue(b.Handle);
            row.Cell(5).SetValue(b.Email);
            row.Cell(6).SetValue(b.AccountNumber);
            row.Cell(7).SetValue(b.BankName);
            row.Cell(8).SetValue(b.RoutingNumber);
            row.Cell(9).SetValue(b.Channel);
            row.Cell(10).SetValue(b.Category);
            row.Cell(11).SetValue(b.TransferCount);
            row.Cell(12).SetValue(b.AvatarInitials);
            row.Cell(13).SetValue(b.CreatedAt.ToString("o"));
        });
    }

    public void DeleteBeneficiary(string beneficiaryId)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Beneficiaries", out var ws)) return;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.Cell(1).GetString().Equals(beneficiaryId, StringComparison.OrdinalIgnoreCase))
                {
                    row.Delete();
                    break;
                }
            }
        });
    }

    public void IncrementBeneficiaryTransferCount(string beneficiaryId)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Beneficiaries", out var ws)) return;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.Cell(1).GetString().Equals(beneficiaryId, StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(row.Cell(11).GetString(), out var count);
                    row.Cell(11).SetValue(count + 1);
                    break;
                }
            }
        });
    }

    #endregion

    #region Notifications Sheet Operations

    public List<NotificationItem> GetNotifications(string? userId = null)
    {
        lock (_fileLock)
        {
            var list = new List<NotificationItem>();
            if (!File.Exists(FilePath)) return list;

            using var workbook = OpenReadWorkbook();
            if (!workbook.TryGetWorksheet("Notifications", out var ws)) return list;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string id = row.Cell(1).GetString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                string rowUserId = row.Cell(2).GetString();
                if (!string.IsNullOrEmpty(userId) && !rowUserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DateTime.TryParse(row.Cell(5).GetString(), null, DateTimeStyles.RoundtripKind, out var ts);

                list.Add(new NotificationItem
                {
                    Id = id,
                    UserId = rowUserId,
                    Title = row.Cell(3).GetString(),
                    Message = row.Cell(4).GetString(),
                    Timestamp = ts != default ? ts : DateTime.UtcNow,
                    Type = row.Cell(6).GetString(),
                    IsRead = bool.TryParse(row.Cell(7).GetString(), out var read) && read,
                    IconType = row.Cell(8).GetString()
                });
            }

            return list.OrderByDescending(n => n.Timestamp).ToList();
        }
    }

    public void InsertNotification(NotificationItem n)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Notifications", out var ws))
            {
                ws = workbook.Worksheets.Add("Notifications");
                string[] notifHeaders = ["NotificationId", "UserId", "Title", "Message", "Timestamp", "Type", "IsRead", "IconType"];
                SetupHeaderRow(ws, notifHeaders);
            }

            int nextRow = (ws.LastRowUsed()?.RowNumber() ?? 1) + 1;
            var row = ws.Row(nextRow);

            row.Cell(1).SetValue(n.Id);
            row.Cell(2).SetValue(n.UserId);
            row.Cell(3).SetValue(n.Title);
            row.Cell(4).SetValue(n.Message);
            row.Cell(5).SetValue(n.Timestamp.ToString("o"));
            row.Cell(6).SetValue(n.Type);
            row.Cell(7).SetValue(n.IsRead);
            row.Cell(8).SetValue(n.IconType);
        });
    }

    public void MarkNotificationsAsRead(string userId)
    {
        ExecuteWrite(workbook =>
        {
            if (!workbook.TryGetWorksheet("Notifications", out var ws)) return;

            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                if (row.Cell(2).GetString().Equals(userId, StringComparison.OrdinalIgnoreCase))
                {
                    row.Cell(7).SetValue(true);
                }
            }
        });
    }

    #endregion

    private static decimal ConvertToDecimal(XLCellValue value)
    {
        if (value.IsNumber)
        {
            return (decimal)value.GetNumber();
        }
        if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return 0m;
    }
}