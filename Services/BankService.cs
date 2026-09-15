using System;
using System.Collections.Generic;
using System.Linq;
using ApexBank.Models;

namespace ApexBank.Services;

public class BankService
{
    private static BankService? _instance;
    public static BankService Instance => _instance ??= new BankService();

    private readonly ExcelDatabaseService _db;

    public event Action? OnDataChanged;

    public User? CurrentUser { get; private set; }
    public List<BankAccount> Accounts { get; } = new();
    public List<Transaction> Transactions { get; } = new();
    public List<Beneficiary> Beneficiaries { get; } = new();
    public List<NotificationItem> Notifications { get; } = new();
    public List<BankInfo> SupportedBanks { get; } = new();
    public List<VirtualCard> Cards { get; } = new();
    public VirtualCard? ActiveCard { get; private set; }

    private static readonly VirtualCard FallbackCard = new()
    {
        Id = "card_fallback",
        CardHolder = "APEX MEMBER",
        MaskedNumber = "4738 •••• •••• 0000",
        FullNumber = "4738 0000 0000 0000",
        Last4 = "0000",
        Expiry = "12/30",
        Cvv = "000",
        Brand = "VISA",
        Tier = "Infinite Platinum"
    };

    public VirtualCard Card => ActiveCard ?? Cards.FirstOrDefault() ?? FallbackCard;

    public BankAccount ActiveAccount { get; private set; } = null!;

    public BankService(ExcelDatabaseService? db = null)
    {
        _db = db ?? ExcelDatabaseService.Instance;

        // Auto-load default user from Excel database so existing tests and baseline app remain functional
        var defaultUser = _db.GetUserByUsernameOrEmail("alexander");
        if (defaultUser != null)
        {
            LoadUserData(defaultUser);
        }
        else
        {
            var anyUser = _db.GetUsers().FirstOrDefault();
            if (anyUser != null)
            {
                LoadUserData(anyUser);
            }
        }
    }

    public static VirtualCard CreatePersonalizedCard(User user, string accountId, string brand = "VISA", string tier = "Infinite Platinum", bool isDisposable = false)
    {
        string cardNumber = CardGenerator.GenerateCardNumber(brand);
        string cvv = CardGenerator.GenerateCvv();
        string expiry = CardGenerator.GenerateExpiry(isDisposable ? 1 : 4);
        string last4 = CardGenerator.ExtractLast4(cardNumber);
        string masked = CardGenerator.MaskCardNumber(cardNumber);

        return new VirtualCard
        {
            Id = $"card_{Guid.NewGuid().ToString("N")[..8]}",
            UserId = user.Id,
            AccountId = accountId,
            CardHolder = user.FullName.ToUpperInvariant(),
            FullNumber = cardNumber,
            Cvv = cvv,
            MaskedNumber = masked,
            Last4 = last4,
            Expiry = expiry,
            Brand = brand,
            Tier = tier,
            IsFrozen = false,
            DailyLimit = isDisposable ? 50000.00m : 100000.00m,
            DailySpent = 0.00m,
            ContactlessEnabled = true,
            OnlinePurchasesEnabled = true,
            InternationalPaymentsEnabled = true,
            IsDisposable = isDisposable,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void LoadUserData(User user)
    {
        CurrentUser = user;

        // 1. Load Accounts from 'Balances' sheet in Excel
        Accounts.Clear();
        var userAccounts = _db.GetAccounts(user.Id);

        if (userAccounts.Count == 0)
        {
            var starterChecking = new BankAccount
            {
                Id = $"acc_chk_{Guid.NewGuid().ToString("N")[..6]}",
                UserId = user.Id,
                Name = "Primary Everyday Checking",
                Category = AccountCategory.Checking,
                AccountNumber = $"**** {Random.Shared.Next(1000, 9999)}",
                Balance = 150000.00m,
                CurrencySymbol = "৳",
                Apy = 0.15m,
                MonthlyInflow = 150000.00m,
                MonthlyOutflow = 0.00m,
                AccentColor = "#10B981",
                BadgeText = "Primary",
                IsActive = true
            };

            var starterSavings = new BankAccount
            {
                Id = $"acc_sav_{Guid.NewGuid().ToString("N")[..6]}",
                UserId = user.Id,
                Name = "High-Yield Growth Reserve",
                Category = AccountCategory.Savings,
                AccountNumber = $"**** {Random.Shared.Next(1000, 9999)}",
                Balance = 350000.00m,
                CurrencySymbol = "৳",
                Apy = 4.85m,
                MonthlyInflow = 350000.00m,
                MonthlyOutflow = 0.00m,
                AccentColor = "#06B6D4",
                BadgeText = "4.85% APY",
                IsActive = false
            };

            _db.InsertAccount(starterChecking);
            _db.InsertAccount(starterSavings);

            Accounts.Add(starterChecking);
            Accounts.Add(starterSavings);
        }
        else
        {
            Accounts.AddRange(userAccounts);
        }

        ActiveAccount = Accounts.FirstOrDefault(a => a.IsActive) ?? Accounts.FirstOrDefault()!;

        // 2. Load Cards from 'Cards' sheet in Excel
        Cards.Clear();
        var userCards = _db.GetCards(user.Id);
        if (userCards.Count == 0)
        {
            var primaryCard = CreatePersonalizedCard(user, ActiveAccount?.Id ?? "acc_chk_01", "VISA", "Infinite Platinum", isDisposable: false);
            _db.InsertCard(primaryCard);
            Cards.Add(primaryCard);
        }
        else
        {
            Cards.AddRange(userCards);
        }

        ActiveCard = Cards.FirstOrDefault(c => !c.IsFrozen) ?? Cards.FirstOrDefault();

        // 3. Load Transactions from 'Transactions' sheet in Excel
        Transactions.Clear();
        var userTxs = _db.GetTransactions(user.Id);
        Transactions.AddRange(userTxs);

        // 4. Load Beneficiaries from 'Beneficiaries' sheet in Excel
        Beneficiaries.Clear();
        var userBens = _db.GetBeneficiaries(user.Id);
        if (userBens.Count == 0)
        {
            var seedBen1 = new Beneficiary
            {
                Id = "ben_01",
                UserId = user.Id,
                Name = "Tanvir Hossain",
                Handle = "@tanvir.h",
                Email = "tanvir.h@brac.com",
                AccountNumber = "1501204892011001",
                BankName = "BRAC Bank PLC",
                RoutingNumber = "060260724",
                Channel = "NPSB",
                Category = "Personal",
                TransferCount = 14,
                AvatarInitials = "TH"
            };
            var seedBen2 = new Beneficiary
            {
                Id = "ben_02",
                UserId = user.Id,
                Name = "Nusrat Jahan",
                Handle = "@nusrat.j",
                Email = "nusrat.j@city.com",
                AccountNumber = "1102948201948",
                BankName = "The City Bank PLC",
                RoutingNumber = "225262531",
                Channel = "NPSB",
                Category = "Personal",
                TransferCount = 9,
                AvatarInitials = "NJ"
            };
            var seedBen3 = new Beneficiary
            {
                Id = "ben_03",
                UserId = user.Id,
                Name = "Rahim Chowdhury",
                Handle = "@rahim.c",
                Email = "rahim.c@techbd.io",
                AccountNumber = "01711998822",
                BankName = "bKash Mobile Financial Services",
                RoutingNumber = "060269999",
                Channel = "MFS",
                Category = "Business",
                TransferCount = 7,
                AvatarInitials = "RC"
            };
            _db.InsertBeneficiary(seedBen1);
            _db.InsertBeneficiary(seedBen2);
            _db.InsertBeneficiary(seedBen3);
            Beneficiaries.Add(seedBen1);
            Beneficiaries.Add(seedBen2);
            Beneficiaries.Add(seedBen3);
        }
        else
        {
            Beneficiaries.AddRange(userBens);
        }

        // 5. Load Supported Banks from 'SupportedBanks' sheet in Excel
        SupportedBanks.Clear();
        var banks = _db.GetSupportedBanks();
        SupportedBanks.AddRange(banks);

        // 6. Load Notifications from 'Notifications' sheet in Excel
        Notifications.Clear();
        var notifs = _db.GetNotifications(user.Id);
        Notifications.AddRange(notifs);

        OnDataChanged?.Invoke();
    }

    #region Authentication & Registration

    public (bool Success, string Message) Register(string fullName, string username, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (false, "Full Name is required.");
        }

        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
        {
            return (false, "Username must be at least 3 characters.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
        {
            return (false, "Please enter a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            return (false, "Password must be at least 6 characters.");
        }

        string cleanUsername = username.Trim().ToLowerInvariant();
        string cleanEmail = email.Trim().ToLowerInvariant();

        var existingUser = _db.GetUserByUsernameOrEmail(cleanUsername) ?? _db.GetUserByUsernameOrEmail(cleanEmail);
        if (existingUser != null)
        {
            return (false, "A user with this username or email already exists.");
        }

        string passwordHash = PasswordHasher.HashPassword(password, out string salt);

        var newUser = new User
        {
            Id = $"usr_{Guid.NewGuid().ToString("N")[..8]}",
            FullName = fullName.Trim(),
            Username = cleanUsername,
            Email = cleanEmail,
            PasswordHash = passwordHash,
            Salt = salt,
            CreatedAt = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow
        };

        _db.InsertUser(newUser);

        // Starter Accounts in BDT (৳)
        var checking = new BankAccount
        {
            Id = $"acc_chk_{Guid.NewGuid().ToString("N")[..6]}",
            UserId = newUser.Id,
            Name = "Primary Everyday Checking",
            Category = AccountCategory.Checking,
            AccountNumber = $"**** {Random.Shared.Next(1000, 9999)}",
            Balance = 100000.00m,
            CurrencySymbol = "৳",
            Apy = 0.25m,
            MonthlyInflow = 100000.00m,
            MonthlyOutflow = 0.00m,
            AccentColor = "#10B981",
            BadgeText = "Primary",
            IsActive = true
        };

        var savings = new BankAccount
        {
            Id = $"acc_sav_{Guid.NewGuid().ToString("N")[..6]}",
            UserId = newUser.Id,
            Name = "High-Yield Growth Reserve",
            Category = AccountCategory.Savings,
            AccountNumber = $"**** {Random.Shared.Next(1000, 9999)}",
            Balance = 250000.00m,
            CurrencySymbol = "৳",
            Apy = 4.85m,
            MonthlyInflow = 250000.00m,
            MonthlyOutflow = 0.00m,
            AccentColor = "#06B6D4",
            BadgeText = "4.85% APY",
            IsActive = false
        };

        _db.InsertAccount(checking);
        _db.InsertAccount(savings);

        // Welcome bonus transaction in BDT
        var welcomeTx = new Transaction
        {
            Id = $"tx_{Guid.NewGuid().ToString("N")[..6]}",
            AccountId = checking.Id,
            UserId = newUser.Id,
            Timestamp = DateTime.UtcNow,
            Title = "ApexBank Welcome Grant",
            Description = "Initial deposit grant for new member registration",
            Category = "Income",
            Amount = 100000.00m,
            Type = TransactionType.Credit,
            IconType = "Income",
            Status = TransactionStatus.Completed,
            ReferenceNumber = $"APX-{Random.Shared.Next(100000, 999999)}"
        };
        _db.InsertTransaction(welcomeTx);

        // Personalized Primary Card with BDT limit
        var userCard = CreatePersonalizedCard(newUser, checking.Id, "VISA", "Infinite Platinum", isDisposable: false);
        _db.InsertCard(userCard);

        // Initial welcome notification
        var welcomeNotif = new NotificationItem
        {
            UserId = newUser.Id,
            Title = "Welcome to ApexBank",
            Message = "Your digital multi-channel BDT banking account is provisioned and ready.",
            Timestamp = DateTime.UtcNow,
            Type = "System",
            IsRead = false,
            IconType = "IconSecurity"
        };
        _db.InsertNotification(welcomeNotif);

        // Audit Log
        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = newUser.Id,
            Username = newUser.Username,
            Action = "USER_REGISTERED",
            Details = $"Account registered for {newUser.FullName} with starter checking (৳100,000.00) and savings (৳250,000.00).",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        LoadUserData(newUser);
        return (true, $"Registration successful! Welcome to ApexBank, {newUser.FullName}.");
    }

    public (bool Success, string Message) Login(string usernameOrEmail, string password)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail))
        {
            return (false, "Please enter your username or email.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Please enter your password.");
        }

        var user = _db.GetUserByUsernameOrEmail(usernameOrEmail);
        if (user == null)
        {
            _db.InsertLog(new AuditLog
            {
                Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
                Timestamp = DateTime.UtcNow,
                UserId = "UNKNOWN",
                Username = usernameOrEmail.Trim(),
                Action = "LOGIN_FAILED",
                Details = "Authentication failed: User identifier not found in Users sheet.",
                Status = "FAILED",
                MachineInfo = Environment.MachineName
            });
            return (false, "Invalid username or password.");
        }

        bool isValid = PasswordHasher.VerifyPassword(password, user.PasswordHash, user.Salt);
        if (!isValid)
        {
            _db.InsertLog(new AuditLog
            {
                Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
                Timestamp = DateTime.UtcNow,
                UserId = user.Id,
                Username = user.Username,
                Action = "LOGIN_FAILED",
                Details = "Authentication failed: Invalid password supplied.",
                Status = "FAILED",
                MachineInfo = Environment.MachineName
            });
            return (false, "Invalid username or password.");
        }

        user.LastLogin = DateTime.UtcNow;
        _db.UpdateUserLastLogin(user.Id, user.LastLogin.Value);

        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = user.Id,
            Username = user.Username,
            Action = "LOGIN_SUCCESS",
            Details = $"User {user.FullName} logged in successfully.",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        LoadUserData(user);
        return (true, $"Welcome back, {user.FullName}!");
    }

    public void EnsureUserLoaded()
    {
        if (CurrentUser == null)
        {
            var defaultUser = _db.GetUserByUsernameOrEmail("alexander") ?? _db.GetUsers().FirstOrDefault();
            if (defaultUser != null)
            {
                LoadUserData(defaultUser);
            }
        }
    }

    public void Logout()
    {
        if (CurrentUser != null)
        {
            _db.InsertLog(new AuditLog
            {
                Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
                Timestamp = DateTime.UtcNow,
                UserId = CurrentUser.Id,
                Username = CurrentUser.Username,
                Action = "USER_LOGOUT",
                Details = $"User {CurrentUser.Username} signed out.",
                Status = "SUCCESS",
                MachineInfo = Environment.MachineName
            });
        }

        CurrentUser = null;
        Cards.Clear();
        ActiveCard = null;
        Beneficiaries.Clear();
        Notifications.Clear();
        OnDataChanged?.Invoke();
    }

    #endregion

    #region Transfer Rails & Payment Engine

    public static decimal GetTransferFee(string channel)
    {
        return channel switch
        {
            "SameBank" or "Internal" or "ApexNetwork" => 0.00m,
            "NPSB" => 10.00m,       // Bangladesh Bank NPSB switch charge: ৳10
            "BEFTN" => 5.00m,       // BEFTN batch clearing charge: ৳5
            "RTGS" => 100.00m,      // RTGS high-value settlement charge: ৳100
            "MFS" or "bKash" or "Nagad" => 15.00m, // MFS disbursement charge: ৳15
            "VISA" or "CardDirect" => 20.00m,     // VISA Card Direct routing charge: ৳20
            "CardPay" => 10.00m,    // Utility & bill payment charge: ৳10
            _ => 10.00m
        };
    }

    /// <summary>
    /// Same-Bank Transfer (Between user's own accounts or to other ApexBank accounts/users).
    /// Always 0.00 BDT transfer fee.
    /// </summary>
    public (bool Success, string Message) TransferSameBank(string sourceAccountId, string destinationAccountIdOrUser, decimal amount, string note)
    {
        if (amount <= 0)
        {
            return (false, "Please enter a transfer amount greater than ৳0.00");
        }

        var sourceAccount = Accounts.FirstOrDefault(a => a.Id == sourceAccountId) ?? ActiveAccount ?? Accounts.FirstOrDefault();
        if (sourceAccount == null)
        {
            return (false, "Source account not found. Please select an account.");
        }

        if (sourceAccount.Balance < amount)
        {
            return (false, $"Insufficient funds in {sourceAccount.Name}. Available: ৳{sourceAccount.Balance:N2}");
        }

        string destClean = destinationAccountIdOrUser?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(destClean))
        {
            return (false, "Please specify a destination account or ApexBank username.");
        }

        // Case A: Transfer between user's own accounts
        var targetOwnAccount = Accounts.FirstOrDefault(a =>
            a.Id.Equals(destClean, StringComparison.OrdinalIgnoreCase) ||
            a.Name.Equals(destClean, StringComparison.OrdinalIgnoreCase) ||
            a.AccountNumber.Equals(destClean, StringComparison.OrdinalIgnoreCase));

        if (targetOwnAccount != null)
        {
            if (targetOwnAccount.Id == sourceAccount.Id)
            {
                return (false, "Source and destination cannot be the same account.");
            }

            // Deduct from source
            sourceAccount.Balance -= amount;
            sourceAccount.MonthlyOutflow += amount;
            _db.UpdateAccountBalance(sourceAccount.Id, sourceAccount.Balance, sourceAccount.MonthlyInflow, sourceAccount.MonthlyOutflow, sourceAccount.IsActive);

            // Credit destination
            targetOwnAccount.Balance += amount;
            targetOwnAccount.MonthlyInflow += amount;
            _db.UpdateAccountBalance(targetOwnAccount.Id, targetOwnAccount.Balance, targetOwnAccount.MonthlyInflow, targetOwnAccount.MonthlyOutflow, targetOwnAccount.IsActive);

            // Dual ledger transactions
            string refNo = $"INT-{Random.Shared.Next(100000, 999999)}";
            var debitTx = new Transaction
            {
                Id = $"tx_{Guid.NewGuid().ToString("N")[..6]}",
                AccountId = sourceAccount.Id,
                UserId = CurrentUser?.Id ?? "usr_alex_01",
                Timestamp = DateTime.Now,
                Title = $"Internal Transfer to {targetOwnAccount.Name}",
                Description = string.IsNullOrWhiteSpace(note) ? "Transferred between personal accounts" : note,
                Category = "Transfer",
                Amount = -amount,
                Type = TransactionType.Debit,
                IconType = "Transfer",
                Status = TransactionStatus.Completed,
                ReferenceNumber = refNo
            };

            var creditTx = new Transaction
            {
                Id = $"tx_{Guid.NewGuid().ToString("N")[..6]}",
                AccountId = targetOwnAccount.Id,
                UserId = CurrentUser?.Id ?? "usr_alex_01",
                Timestamp = DateTime.Now,
                Title = $"Internal Transfer from {sourceAccount.Name}",
                Description = string.IsNullOrWhiteSpace(note) ? "Transferred between personal accounts" : note,
                Category = "Transfer",
                Amount = amount,
                Type = TransactionType.Credit,
                IconType = "Transfer",
                Status = TransactionStatus.Completed,
                ReferenceNumber = refNo
            };

            Transactions.Insert(0, debitTx);
            Transactions.Insert(0, creditTx);
            _db.InsertTransaction(debitTx);
            _db.InsertTransaction(creditTx);

            AddNotification("Internal Transfer Completed", $"Transferred ৳{amount:N2} to {targetOwnAccount.Name} with ৳0.00 fee. Ref: {refNo}", "Transfer");

            _db.InsertLog(new AuditLog
            {
                Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
                Timestamp = DateTime.UtcNow,
                UserId = CurrentUser?.Id ?? "usr_alex_01",
                Username = CurrentUser?.Username ?? "alexander",
                Action = "INTERNAL_TRANSFER",
                Details = $"Moved ৳{amount:N2} from {sourceAccount.Name} to {targetOwnAccount.Name}. Zero transaction fee. Ref: {refNo}",
                Status = "SUCCESS",
                MachineInfo = Environment.MachineName
            });

            OnDataChanged?.Invoke();
            return (true, $"Transferred ৳{amount:N2} to {targetOwnAccount.Name} successfully with zero fee!");
        }

        // Case B: Transfer to another user on ApexBank network
        BankAccount? recipientPrimary = _db.FindAccountByNumber(destClean);
        User? recipientUser = null;

        if (recipientPrimary != null)
        {
            recipientUser = _db.GetUsers().FirstOrDefault(u => u.Id.Equals(recipientPrimary.UserId, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            recipientUser = _db.GetUserByUsernameOrEmail(destClean);
            if (recipientUser != null)
            {
                var recipientAccounts = _db.GetAccounts(recipientUser.Id);
                recipientPrimary = recipientAccounts.FirstOrDefault(a => a.IsActive) ?? recipientAccounts.FirstOrDefault();
            }
        }

        string recipientAccountId = recipientPrimary?.Id ?? string.Empty;
        string recipientDisplayName = recipientUser?.FullName ?? (recipientPrimary != null ? $"Account {recipientPrimary.AccountNumber}" : destClean);

        if (recipientPrimary != null)
        {
            // Credit recipient
            recipientPrimary.Balance += amount;
            recipientPrimary.MonthlyInflow += amount;
            _db.UpdateAccountBalance(recipientPrimary.Id, recipientPrimary.Balance, recipientPrimary.MonthlyInflow, recipientPrimary.MonthlyOutflow, recipientPrimary.IsActive);

            // Insert credit transaction for recipient
            var recipientTx = new Transaction
            {
                Id = $"tx_{Guid.NewGuid().ToString("N")[..6]}",
                AccountId = recipientPrimary.Id,
                UserId = recipientPrimary.UserId,
                Timestamp = DateTime.Now,
                Title = $"Received from {CurrentUser?.FullName ?? "ApexBank Member"}",
                Description = string.IsNullOrWhiteSpace(note) ? "Instant intra-bank network transfer" : note,
                Category = "Income",
                Amount = amount,
                Type = TransactionType.Credit,
                IconType = "Income",
                Status = TransactionStatus.Completed,
                ReferenceNumber = $"APX-{Random.Shared.Next(100000, 999999)}"
            };
            _db.InsertTransaction(recipientTx);
        }

        // Deduct from sender
        sourceAccount.Balance -= amount;
        sourceAccount.MonthlyOutflow += amount;
        _db.UpdateAccountBalance(sourceAccount.Id, sourceAccount.Balance, sourceAccount.MonthlyInflow, sourceAccount.MonthlyOutflow, sourceAccount.IsActive);

        string senderRef = $"APX-{Random.Shared.Next(100000, 999999)}";
        var senderTx = new Transaction
        {
            Id = $"tx_{Guid.NewGuid().ToString("N")[..6]}",
            AccountId = sourceAccount.Id,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Timestamp = DateTime.Now,
            Title = $"Apex Network to {recipientDisplayName}",
            Description = string.IsNullOrWhiteSpace(note) ? "Intra-bank transfer (Zero fee)" : note,
            Category = "Transfer",
            Amount = -amount,
            Type = TransactionType.Debit,
            IconType = "Transfer",
            Status = TransactionStatus.Completed,
            ReferenceNumber = senderRef
        };

        Transactions.Insert(0, senderTx);
        _db.InsertTransaction(senderTx);

        // Update beneficiary transfer count if matches
        var matchingBen = Beneficiaries.FirstOrDefault(b => b.Name.Equals(destClean, StringComparison.OrdinalIgnoreCase) || b.AccountNumber.Equals(destClean, StringComparison.OrdinalIgnoreCase));
        if (matchingBen != null)
        {
            matchingBen.TransferCount++;
            _db.IncrementBeneficiaryTransferCount(matchingBen.Id);
        }

        AddNotification("Apex Network Transfer", $"Sent ৳{amount:N2} to {recipientDisplayName}. Zero fee applied. Ref: {senderRef}", "Transfer");

        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Username = CurrentUser?.Username ?? "alexander",
            Action = "TRANSFER_EXECUTED",
            Details = $"Sent ৳{amount:N2} from {sourceAccount.Name} to {recipientDisplayName}. Charge: ৳0.00 (FREE). Ref: {senderRef}",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        OnDataChanged?.Invoke();
        return (true, $"Successfully transferred ৳{amount:N2} to {recipientDisplayName} with zero fee!");
    }

    /// <summary>
    /// External Bangladeshi Bank Transfer via NPSB, BEFTN, RTGS, MFS, or VISA Direct.
    /// Channel-specific BDT fee applied.
    /// </summary>
    public (bool Success, string Message) TransferExternalBangladesh(
        string sourceAccountId,
        string bankName,
        string channel,
        string recipientAccount,
        string recipientName,
        string routingNumber,
        decimal amount,
        string note,
        bool saveBeneficiary)
    {
        if (amount <= 0)
        {
            return (false, "Please enter an amount greater than ৳0.00");
        }

        if (string.IsNullOrWhiteSpace(bankName))
        {
            return (false, "Please select a destination bank.");
        }

        if (string.IsNullOrWhiteSpace(recipientAccount))
        {
            return (false, "Recipient account or wallet number is required.");
        }

        if (string.IsNullOrWhiteSpace(recipientName))
        {
            return (false, "Recipient name is required.");
        }

        string cleanChannel = string.IsNullOrWhiteSpace(channel) ? "NPSB" : channel.ToUpperInvariant();

        // Bangladesh Bank Rail Rule Enforcements
        if (cleanChannel == "RTGS" && amount < 100000m)
        {
            return (false, "Bangladesh Bank RTGS rule: Minimum transfer amount is ৳100,000.00 for RTGS.");
        }

        if (cleanChannel == "NPSB" && amount > 300000m)
        {
            return (false, "Bangladesh Bank NPSB rule: Maximum single transfer limit is ৳300,000.00 per transaction.");
        }

        var sourceAccount = Accounts.FirstOrDefault(a => a.Id == sourceAccountId) ?? ActiveAccount ?? Accounts.FirstOrDefault();
        if (sourceAccount == null)
        {
            return (false, "Source account not found. Please select an account.");
        }

        decimal fee = GetTransferFee(cleanChannel);
        decimal totalDebit = amount + fee;

        if (sourceAccount.Balance < totalDebit)
        {
            return (false, $"Insufficient funds in {sourceAccount.Name}. Total required: ৳{totalDebit:N2} (Amount: ৳{amount:N2} + Fee: ৳{fee:N2}). Current balance: ৳{sourceAccount.Balance:N2}");
        }

        // Deduct from account
        sourceAccount.Balance -= totalDebit;
        sourceAccount.MonthlyOutflow += totalDebit;
        _db.UpdateAccountBalance(sourceAccount.Id, sourceAccount.Balance, sourceAccount.MonthlyInflow, sourceAccount.MonthlyOutflow, sourceAccount.IsActive);

        string refNo = $"{cleanChannel}-{Random.Shared.Next(100000, 999999)}";
        string memo = string.IsNullOrWhiteSpace(note) ? "Interbank funds transfer" : note;
        string detailedDesc = $"Acc: {recipientAccount} | Routing: {routingNumber} | Fee: ৳{fee:N2}. {memo}";

        var tx = new Transaction
        {
            Id = $"tx_{Guid.NewGuid().ToString("N")[..6]}",
            AccountId = sourceAccount.Id,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Timestamp = DateTime.Now,
            Title = $"{cleanChannel}: {bankName} - {recipientName}",
            Description = detailedDesc,
            Category = "Transfer",
            Amount = -totalDebit,
            Type = TransactionType.Debit,
            IconType = "Transfer",
            Status = TransactionStatus.Completed,
            ReferenceNumber = refNo
        };

        Transactions.Insert(0, tx);
        _db.InsertTransaction(tx);

        // Beneficiary handling
        var existingBen = Beneficiaries.FirstOrDefault(b => b.AccountNumber.Equals(recipientAccount, StringComparison.OrdinalIgnoreCase));
        if (existingBen != null)
        {
            existingBen.TransferCount++;
            _db.IncrementBeneficiaryTransferCount(existingBen.Id);
        }
        else if (saveBeneficiary)
        {
            var newBen = new Beneficiary
            {
                Id = $"ben_{Guid.NewGuid().ToString("N")[..8]}",
                UserId = CurrentUser?.Id ?? "usr_alex_01",
                Name = recipientName.Trim(),
                Handle = $"@{recipientName.Trim().ToLowerInvariant().Replace(" ", "")}",
                Email = $"{recipientName.Trim().ToLowerInvariant().Replace(" ", "")}@bank.bd",
                AccountNumber = recipientAccount.Trim(),
                BankName = bankName.Trim(),
                RoutingNumber = routingNumber.Trim(),
                Channel = cleanChannel,
                Category = "External Bank",
                TransferCount = 1,
                CreatedAt = DateTime.UtcNow
            };

            var parts = newBen.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            newBen.AvatarInitials = parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant() : parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();

            Beneficiaries.Insert(0, newBen);
            _db.InsertBeneficiary(newBen);
        }

        AddNotification($"{cleanChannel} Transfer Dispatched", $"Sent ৳{amount:N2} to {recipientName} ({bankName}). Transfer Fee: ৳{fee:N2}. Ref: {refNo}", "Transfer");

        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Username = CurrentUser?.Username ?? "alexander",
            Action = $"{cleanChannel}_TRANSFER_SENT",
            Details = $"Dispatched ৳{amount:N2} via {cleanChannel} to {recipientName} at {bankName}. Routing: {routingNumber}. Channel fee: ৳{fee:N2}. Total: ৳{totalDebit:N2}. Ref: {refNo}",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        OnDataChanged?.Invoke();
        return (true, $"Dispatched ৳{amount:N2} via {cleanChannel} to {recipientName} at {bankName} successfully! Fee: ৳{fee:N2}");
    }

    /// <summary>
    /// Pay via Card for Utility Bills & Merchant Checkouts.
    /// Requires active, unfrozen card with online purchases enabled and valid CVV.
    /// </summary>
    public (bool Success, string Message) PayViaCard(
        string cardId,
        string billerName,
        string billerAccount,
        decimal amount,
        string cvv,
        string category,
        string note)
    {
        if (amount <= 0)
        {
            return (false, "Please enter an amount greater than ৳0.00");
        }

        var card = Cards.FirstOrDefault(c => c.Id == cardId) ?? ActiveCard ?? Cards.FirstOrDefault();
        if (card == null)
        {
            return (false, "No active payment card found.");
        }

        if (card.IsFrozen)
        {
            return (false, "Card authorization declined: This card is currently FROZEN. Please unfreeze in Cards Studio.");
        }

        if (!card.OnlinePurchasesEnabled)
        {
            return (false, "Card authorization declined: Online/e-commerce checkouts are disabled for this card.");
        }

        if (string.IsNullOrWhiteSpace(cvv) || !card.Cvv.Trim().Equals(cvv.Trim()))
        {
            _db.InsertLog(new AuditLog
            {
                Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
                Timestamp = DateTime.UtcNow,
                UserId = CurrentUser?.Id ?? "usr_alex_01",
                Username = CurrentUser?.Username ?? "alexander",
                Action = "CARD_AUTH_FAILED",
                Details = $"Card checkout for {billerName} failed: Invalid CVV entered for card ending {card.Last4}.",
                Status = "FAILED",
                MachineInfo = Environment.MachineName
            });
            return (false, "Card authorization declined: Invalid 3-digit CVV security code.");
        }

        // Daily Limit Check
        decimal availableLimit = card.DailyLimit - card.DailySpent;
        if (amount > availableLimit)
        {
            return (false, $"Daily spending limit exceeded. Available headroom: ৳{availableLimit:N2} (Requested: ৳{amount:N2}).");
        }

        // Linked Account Check
        var linkedAccount = Accounts.FirstOrDefault(a => a.Id == card.AccountId) ?? ActiveAccount ?? Accounts.FirstOrDefault();
        if (linkedAccount == null)
        {
            return (false, "Linked checking account not found for card.");
        }

        decimal fee = GetTransferFee("CardPay"); // ৳10
        decimal totalDebit = amount + fee;

        if (linkedAccount.Balance < totalDebit)
        {
            return (false, $"Insufficient balance in linked account {linkedAccount.Name}. Available: ৳{linkedAccount.Balance:N2}, required: ৳{totalDebit:N2}.");
        }

        // Deduct from linked account
        linkedAccount.Balance -= totalDebit;
        linkedAccount.MonthlyOutflow += totalDebit;
        _db.UpdateAccountBalance(linkedAccount.Id, linkedAccount.Balance, linkedAccount.MonthlyInflow, linkedAccount.MonthlyOutflow, linkedAccount.IsActive);

        // Update Daily Spent on Card in Excel
        card.DailySpent += amount;
        _db.UpdateCardSpent(card.Id, amount);

        string refNo = $"CARD-{Random.Shared.Next(100000, 999999)}";
        string cleanCategory = string.IsNullOrWhiteSpace(category) ? "Utilities" : category;
        string detailedDesc = $"Biller: {billerName} | Ref: {billerAccount} | Card: {card.MaskedNumber} | Fee: ৳{fee:N2}. {note}";

        var tx = new Transaction
        {
            Id = $"tx_{Guid.NewGuid().ToString("N")[..6]}",
            AccountId = linkedAccount.Id,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Timestamp = DateTime.Now,
            Title = $"Card Payment: {billerName}",
            Description = detailedDesc,
            Category = cleanCategory,
            Amount = -totalDebit,
            Type = TransactionType.Debit,
            IconType = "Zap",
            Status = TransactionStatus.Completed,
            ReferenceNumber = refNo
        };

        Transactions.Insert(0, tx);
        _db.InsertTransaction(tx);

        AddNotification("Card Payment Approved", $"Paid ৳{amount:N2} to {billerName} via card ending {card.Last4}. Processing Fee: ৳{fee:N2}. Ref: {refNo}", "Card");

        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Username = CurrentUser?.Username ?? "alexander",
            Action = "CARD_PAYMENT_AUTHORIZED",
            Details = $"Paid ৳{amount:N2} to {billerName} (Ref: {billerAccount}) with card {card.MaskedNumber}. Fee: ৳{fee:N2}. Ref: {refNo}",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        OnDataChanged?.Invoke();
        return (true, $"Paid ৳{amount:N2} to {billerName} successfully via card {card.MaskedNumber}! Fee: ৳{fee:N2}");
    }

    /// <summary>
    /// Legacy fallback for general transfers and unit test compatibility.
    /// </summary>
    public (bool Success, string Message) TransferFunds(string sourceAccountId, string recipientName, decimal amount, string category, string note)
    {
        return TransferSameBank(sourceAccountId, recipientName, amount, note);
    }

    public (bool Success, string Message) DepositFunds(string targetAccountId, decimal amount, string source)
    {
        if (amount <= 0)
        {
            return (false, "Please enter an amount greater than ৳0.00");
        }

        var targetAccount = Accounts.FirstOrDefault(a => a.Id == targetAccountId) ?? ActiveAccount ?? Accounts.FirstOrDefault();
        if (targetAccount == null)
        {
            return (false, "Target account could not be determined. Please select an account.");
        }

        targetAccount.Balance += amount;
        targetAccount.MonthlyInflow += amount;

        _db.UpdateAccountBalance(targetAccount.Id, targetAccount.Balance, targetAccount.MonthlyInflow, targetAccount.MonthlyOutflow, targetAccount.IsActive);

        var tx = new Transaction
        {
            Id = $"tx_{Guid.NewGuid().ToString("N")[..6]}",
            AccountId = targetAccount.Id,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Timestamp = DateTime.Now,
            Title = $"Deposit via {source}",
            Description = "Instant electronic funds deposit (BDT)",
            Category = "Income",
            Amount = amount,
            Type = TransactionType.Credit,
            IconType = "Income",
            Status = TransactionStatus.Completed,
            ReferenceNumber = $"APX-{Random.Shared.Next(100000, 999999)}"
        };

        Transactions.Insert(0, tx);
        _db.InsertTransaction(tx);

        AddNotification("Funds Deposited", $"Deposited ৳{amount:N2} into {targetAccount.Name} via {source}.", "System");

        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Username = CurrentUser?.Username ?? "alexander",
            Action = "DEPOSIT_COMPLETED",
            Details = $"Deposited ৳{amount:N2} into {targetAccount.Name} via {source}. Ref: {tx.ReferenceNumber}",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        OnDataChanged?.Invoke();
        return (true, $"Deposited ৳{amount:N2} into {targetAccount.Name} successfully!");
    }

    #endregion

    #region Card Security Rules & Controls

    public void ToggleCardFreeze(string? cardId = null)
    {
        var targetCard = !string.IsNullOrWhiteSpace(cardId) 
            ? Cards.FirstOrDefault(c => c.Id == cardId) ?? ActiveCard ?? Cards.FirstOrDefault()
            : ActiveCard ?? Cards.FirstOrDefault();
        if (targetCard == null) return;

        targetCard.IsFrozen = !targetCard.IsFrozen;
        _db.UpdateCardFreeze(targetCard.Id, targetCard.IsFrozen);

        AddNotification(
            targetCard.IsFrozen ? "Card Locked" : "Card Unfrozen",
            $"Card {targetCard.MaskedNumber} is now {(targetCard.IsFrozen ? "FROZEN (transactions locked)" : "ACTIVE")}.",
            "Card"
        );

        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Username = CurrentUser?.Username ?? "alexander",
            Action = "CARD_FREEZE_TOGGLE",
            Details = $"Card {targetCard.MaskedNumber} freeze state changed to {(targetCard.IsFrozen ? "FROZEN" : "ACTIVE")}.",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        OnDataChanged?.Invoke();
    }

    public void UpdateCardSecuritySettings(string cardId, decimal? dailyLimit = null, bool? contactless = null, bool? online = null, bool? international = null)
    {
        var targetCard = Cards.FirstOrDefault(c => c.Id == cardId) ?? ActiveCard ?? Cards.FirstOrDefault();
        if (targetCard == null) return;

        if (dailyLimit.HasValue) targetCard.DailyLimit = dailyLimit.Value;
        if (contactless.HasValue) targetCard.ContactlessEnabled = contactless.Value;
        if (online.HasValue) targetCard.OnlinePurchasesEnabled = online.Value;
        if (international.HasValue) targetCard.InternationalPaymentsEnabled = international.Value;

        _db.UpdateCardSettings(targetCard.Id, targetCard.DailyLimit, targetCard.ContactlessEnabled, targetCard.OnlinePurchasesEnabled, targetCard.InternationalPaymentsEnabled);

        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Username = CurrentUser?.Username ?? "alexander",
            Action = "CARD_SETTINGS_UPDATED",
            Details = $"Updated security rules for card {targetCard.MaskedNumber}: Limit=৳{targetCard.DailyLimit:N2}, Online={targetCard.OnlinePurchasesEnabled}, Contactless={targetCard.ContactlessEnabled}, Intl={targetCard.InternationalPaymentsEnabled}.",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        OnDataChanged?.Invoke();
    }

    public void SetActiveCard(VirtualCard card)
    {
        ActiveCard = card;
        OnDataChanged?.Invoke();
    }

    public void SetActiveAccount(BankAccount account)
    {
        ActiveAccount = account;
        foreach (var a in Accounts)
        {
            a.IsActive = (a.Id == account.Id);
            _db.UpdateAccountBalance(a.Id, a.Balance, a.MonthlyInflow, a.MonthlyOutflow, a.IsActive);
        }
        OnDataChanged?.Invoke();
    }

    public (bool Success, string Message, VirtualCard? Card) GenerateNewCard(string? accountId = null, string brand = "VISA", string tier = "Disposable Virtual")
    {
        if (CurrentUser == null)
        {
            return (false, "You must be signed in to generate a card.", null);
        }

        string targetAccountId = accountId ?? ActiveAccount?.Id ?? Accounts.FirstOrDefault()?.Id ?? "acc_chk_01";
        var newCard = CreatePersonalizedCard(CurrentUser, targetAccountId, brand, tier, isDisposable: true);

        _db.InsertCard(newCard);
        Cards.Insert(0, newCard);
        ActiveCard = newCard;

        AddNotification("Virtual Card Provisioned", $"New {tier} card ({newCard.MaskedNumber}) generated with AES-256 encrypted storage.", "Card");

        _db.InsertLog(new AuditLog
        {
            Id = $"log_{Guid.NewGuid().ToString("N")[..6]}",
            Timestamp = DateTime.UtcNow,
            UserId = CurrentUser.Id,
            Username = CurrentUser.Username,
            Action = "CARD_GENERATED",
            Details = $"Generated new {tier} {brand} card ({newCard.MaskedNumber}) for {CurrentUser.FullName}.",
            Status = "SUCCESS",
            MachineInfo = Environment.MachineName
        });

        OnDataChanged?.Invoke();
        return (true, $"New {tier} card ({newCard.MaskedNumber}) generated securely!", newCard);
    }

    #endregion

    #region Beneficiaries & Notifications Operations

    public void AddBeneficiary(Beneficiary b)
    {
        b.UserId = CurrentUser?.Id ?? "usr_alex_01";
        Beneficiaries.Insert(0, b);
        _db.InsertBeneficiary(b);
        OnDataChanged?.Invoke();
    }

    public void RemoveBeneficiary(string beneficiaryId)
    {
        var existing = Beneficiaries.FirstOrDefault(b => b.Id == beneficiaryId);
        if (existing != null)
        {
            Beneficiaries.Remove(existing);
            _db.DeleteBeneficiary(beneficiaryId);
            OnDataChanged?.Invoke();
        }
    }

    public void AddNotification(string title, string message, string type = "System")
    {
        var notif = new NotificationItem
        {
            UserId = CurrentUser?.Id ?? "usr_alex_01",
            Title = title,
            Message = message,
            Timestamp = DateTime.UtcNow,
            Type = type,
            IsRead = false,
            IconType = type switch
            {
                "Security" => "IconSecurity",
                "Transfer" => "IconTransfer",
                "Card" => "IconCards",
                _ => "IconBell"
            }
        };

        Notifications.Insert(0, notif);
        _db.InsertNotification(notif);
        OnDataChanged?.Invoke();
    }

    public void MarkAllNotificationsAsRead()
    {
        if (CurrentUser != null)
        {
            foreach (var n in Notifications)
            {
                n.IsRead = true;
            }
            _db.MarkNotificationsAsRead(CurrentUser.Id);
            OnDataChanged?.Invoke();
        }
    }

    public List<BankInfo> GetSupportedBanks() => _db.GetSupportedBanks();

    #endregion

    #region Analytics & Net Worth

    public List<AuditLog> GetAuditLogs() => _db.GetLogs(CurrentUser?.Id);

    public string GetDatabaseFilePath() => _db.FilePath;

    public List<SpendingCategory> GetSpendingBreakdown()
    {
        var expenses = Transactions
            .Where(t => t.Type == TransactionType.Debit)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(x => Math.Abs(x.Amount)) })
            .ToList();

        var totalSpent = expenses.Sum(e => e.Total);
        if (totalSpent == 0) totalSpent = 1;

        var result = new List<SpendingCategory>();
        var colors = new Dictionary<string, (string Color, string Icon)>
        {
            { "Shopping", ("#EC4899", "Bag") },
            { "Groceries", ("#10B981", "Cart") },
            { "Transfer", ("#6366F1", "Transfer") },
            { "Utilities", ("#F59E0B", "Zap") },
            { "Entertainment", ("#8B5CF6", "Film") },
            { "Transport", ("#06B6D4", "Car") },
            { "Income", ("#10B981", "Income") },
            { "General", ("#3B82F6", "Layers") }
        };

        foreach (var item in expenses)
        {
            var meta = colors.TryGetValue(item.Category, out var val) ? val : ("#3B82F6", "Layers");
            result.Add(new SpendingCategory
            {
                Name = item.Category,
                Amount = item.Total,
                Percentage = (double)(item.Total / totalSpent * 100m),
                ColorHex = meta.Item1,
                IconKey = meta.Item2
            });
        }

        return result.OrderByDescending(r => r.Amount).ToList();
    }

    public decimal GetTotalNetWorth() => Accounts.Sum(a => a.Balance);

    public decimal GetTotalMonthlyInflow() => Transactions.Where(t => t.Type == TransactionType.Credit && t.Timestamp >= DateTime.UtcNow.AddDays(-30)).Sum(t => t.Amount);

    public decimal GetTotalMonthlyOutflow() => Transactions.Where(t => t.Type == TransactionType.Debit && t.Timestamp >= DateTime.UtcNow.AddDays(-30)).Sum(t => Math.Abs(t.Amount));

    #endregion
}