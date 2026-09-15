using System;
using System.IO;
using System.Linq;
using ApexBank.Models;
using ApexBank.Services;
using ApexBank.ViewModels;
using ClosedXML.Excel;
using Xunit;

namespace Tests;

public class AuthAndSheetDatabaseTests
{
    [Fact]
    public void ExcelDatabase_CreatesWorkbookWithAllFourWorksheets()
    {
        // Arrange
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_Test_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);

            // Assert file exists
            Assert.True(File.Exists(testDbPath));

            // Verify worksheets in single workbook
            using var workbook = new XLWorkbook(testDbPath);
            Assert.NotNull(workbook.Worksheet("Users"));
            Assert.NotNull(workbook.Worksheet("Balances"));
            Assert.NotNull(workbook.Worksheet("Transactions"));
            Assert.NotNull(workbook.Worksheet("Logs"));

            // Verify Users sheet headers & seed user
            var wsUsers = workbook.Worksheet("Users");
            Assert.Equal("UserId", wsUsers.Cell(1, 1).GetString());
            Assert.Equal("FullName", wsUsers.Cell(1, 2).GetString());
            Assert.Equal("Username", wsUsers.Cell(1, 3).GetString());
            Assert.Equal("alexander", wsUsers.Cell(2, 3).GetString());

            // Verify Balances sheet headers & seed accounts
            var wsBalances = workbook.Worksheet("Balances");
            Assert.Equal("AccountId", wsBalances.Cell(1, 1).GetString());
            Assert.Equal("Balance", wsBalances.Cell(1, 6).GetString());
            Assert.True(wsBalances.LastRowUsed()!.RowNumber() >= 4); // headers + 3 accounts

            // Verify Transactions sheet headers
            var wsTransactions = workbook.Worksheet("Transactions");
            Assert.Equal("Id", wsTransactions.Cell(1, 1).GetString());
            Assert.Equal("Amount", wsTransactions.Cell(1, 8).GetString());

            // Verify Logs sheet headers
            var wsLogs = workbook.Worksheet("Logs");
            Assert.Equal("LogId", wsLogs.Cell(1, 1).GetString());
            Assert.Equal("Action", wsLogs.Cell(1, 5).GetString());
        }
        finally
        {
            if (File.Exists(testDbPath))
            {
                File.Delete(testDbPath);
            }
        }
    }

    [Fact]
    public void RegistrationAndLogin_WorkCorrectlyAndPersistToExcel()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_Test_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);
            var bankService = new BankService(db);

            // 1. Register a new user
            string username = $"testuser_{Guid.NewGuid():N}"[..12];
            string email = $"{username}@apex.io";
            var regResult = bankService.Register("Elena Rostova", username, email, "StrongPassword@123");

            Assert.True(regResult.Success);
            Assert.NotNull(bankService.CurrentUser);
            Assert.Equal(username, bankService.CurrentUser.Username);
            Assert.Equal("Elena Rostova", bankService.CurrentUser.FullName);

            // Verify user was written to 'Users' sheet
            var userInDb = db.GetUserByUsernameOrEmail(username);
            Assert.NotNull(userInDb);
            Assert.Equal(email, userInDb.Email);

            // Verify accounts were created in 'Balances' sheet
            var accountsInDb = db.GetAccounts(userInDb.Id);
            Assert.NotEmpty(accountsInDb);
            Assert.True(accountsInDb.Sum(a => a.Balance) > 0);

            // Verify audit log in 'Logs' sheet
            var logsInDb = db.GetLogs(userInDb.Id);
            Assert.Contains(logsInDb, l => l.Action == "USER_REGISTERED");

            // 2. Test Login with wrong password
            var failResult = bankService.Login(username, "WrongPassword");
            Assert.False(failResult.Success);

            // 3. Test Login with valid credentials
            var loginResult = bankService.Login(username, "StrongPassword@123");
            Assert.True(loginResult.Success);
            Assert.Equal(username, bankService.CurrentUser.Username);

            // Verify LOGIN_SUCCESS was recorded in 'Logs' sheet
            var updatedLogs = db.GetLogs(userInDb.Id);
            Assert.Contains(updatedLogs, l => l.Action == "LOGIN_SUCCESS");
        }
        finally
        {
            if (File.Exists(testDbPath))
            {
                File.Delete(testDbPath);
            }
        }
    }

    [Fact]
    public void TransferAndDeposit_UpdateBalancesAndTransactionsAndLogsSheets()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_Test_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);
            var bankService = new BankService(db);

            // Log in as default demo user
            var loginRes = bankService.Login("alexander", "Apex@2026");
            Assert.True(loginRes.Success);

            var account = bankService.Accounts.First(a => a.IsActive);
            decimal initialBalance = account.Balance;

            // Execute Transfer
            decimal transferAmount = 250m;
            var transferRes = bankService.TransferFunds(account.Id, "Sarah Connor", transferAmount, "Transfer", "Test transfer");
            Assert.True(transferRes.Success);

            // Verify balance in memory
            Assert.Equal(initialBalance - transferAmount, account.Balance);

            // Verify balance in Excel 'Balances' sheet
            var dbAccounts = db.GetAccounts(bankService.CurrentUser!.Id);
            var dbAcc = dbAccounts.First(a => a.Id == account.Id);
            Assert.Equal(initialBalance - transferAmount, dbAcc.Balance);

            // Verify transaction in Excel 'Transactions' sheet
            var dbTxs = db.GetTransactions(bankService.CurrentUser!.Id);
            var latestTx = dbTxs.First();
            Assert.Equal(-transferAmount, latestTx.Amount);
            Assert.Contains("Sarah Connor", latestTx.Title);

            // Verify log in Excel 'Logs' sheet
            var dbLogs = db.GetLogs(bankService.CurrentUser!.Id);
            Assert.Contains(dbLogs, l => l.Action == "TRANSFER_EXECUTED");

            // Execute Deposit
            decimal depositAmount = 500m;
            var depositRes = bankService.DepositFunds(account.Id, depositAmount, "Direct Deposit");
            Assert.True(depositRes.Success);

            // Verify balance in Excel
            var reloadedAccounts = db.GetAccounts(bankService.CurrentUser!.Id);
            var reloadedAcc = reloadedAccounts.First(a => a.Id == account.Id);
            Assert.Equal(initialBalance - transferAmount + depositAmount, reloadedAcc.Balance);

            // Verify deposit log in Excel 'Logs' sheet
            var reloadedLogs = db.GetLogs(bankService.CurrentUser!.Id);
            Assert.Contains(reloadedLogs, l => l.Action == "DEPOSIT_COMPLETED");
        }
        finally
        {
            if (File.Exists(testDbPath))
            {
                File.Delete(testDbPath);
            }
        }
    }

    [Fact]
    public void ViewModel_AuthCommands_ManageStateProperly()
    {
        var vm = new MainViewModel();

        // Test mode switch
        Assert.False(vm.IsRegisterMode);
        vm.SwitchAuthModeCommand.Execute(null);
        Assert.True(vm.IsRegisterMode);
        vm.SwitchAuthModeCommand.Execute(null);
        Assert.False(vm.IsRegisterMode);

        // Test Quick Demo Login
        vm.QuickDemoLoginCommand.Execute(null);
        Assert.True(vm.IsAuthenticated);
        Assert.NotNull(vm.CurrentUser);
        Assert.Equal("Alexander Cross", vm.CurrentUserName);
        Assert.NotEmpty(vm.AuditLogs);

        // Test Logout
        vm.LogoutCommand.Execute(null);
        Assert.False(vm.IsAuthenticated);
    }

    [Fact]
    public void MultipleUsers_HaveIsolatedBalances_AndNeverConflict()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"ApexBank_MultiUser_{Guid.NewGuid():N}.xlsx");
        try
        {
            var db = new ExcelDatabaseService(testDbPath);
            var bankService = new BankService(db);

            // 1. Register and Log in Alice
            var regAlice = bankService.Register("Alice Smith", "alice", "alice@test.com", "Password@123");
            Assert.True(regAlice.Success);
            var loginAlice = bankService.Login("alice", "Password@123");
            Assert.True(loginAlice.Success);
            string aliceId = bankService.CurrentUser!.Id;

            // Alice deposits ৳1,200 (Starter checking is ৳100,000)
            var aliceChecking = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);
            string aliceChkId = aliceChecking.Id;
            var depResult = bankService.DepositFunds(aliceChkId, 1200m, "Salary");
            Assert.True(depResult.Success);
            Assert.Equal(101200m, aliceChecking.Balance);

            // 2. Register and Log in Bob
            bankService.Logout();
            var regBob = bankService.Register("Bob Jones", "bob", "bob@test.com", "Password@456");
            Assert.True(regBob.Success);
            var loginBob = bankService.Login("bob", "Password@456");
            Assert.True(loginBob.Success);
            string bobId = bankService.CurrentUser!.Id;
            Assert.NotEqual(aliceId, bobId);

            // Bob's checking account is completely distinct from Alice's
            var bobChecking = bankService.Accounts.First(a => a.Category == AccountCategory.Checking);
            string bobChkId = bobChecking.Id;
            Assert.NotEqual(aliceChkId, bobChkId);
            Assert.Equal(100000m, bobChecking.Balance); // Fresh starter balance (৳100,000), not affected by Alice's deposit

            // Bob spends ৳400
            var bobTransfer = bankService.TransferFunds(bobChkId, "Landlord", 400m, "Rent", "Office rent");
            Assert.True(bobTransfer.Success);
            Assert.Equal(99600m, bobChecking.Balance);

            // 3. Verify the Excel spreadsheet 'Balances' sheet contains both users' rows with independent balances
            using (var workbook = new ClosedXML.Excel.XLWorkbook(testDbPath))
            {
                var wsBalances = workbook.Worksheet("Balances");
                var aliceRows = wsBalances.RowsUsed().Where(r => r.Cell(2).GetString() == aliceId).ToList();
                var bobRows = wsBalances.RowsUsed().Where(r => r.Cell(2).GetString() == bobId).ToList();

                Assert.NotEmpty(aliceRows);
                Assert.NotEmpty(bobRows);

                // Alice's checking row in Excel
                var aliceChkRow = aliceRows.First(r => r.Cell(1).GetString() == aliceChkId);
                Assert.Equal(101200m, decimal.Parse(aliceChkRow.Cell(6).GetString()));

                // Bob's checking row in Excel
                var bobChkRow = bobRows.First(r => r.Cell(1).GetString() == bobChkId);
                Assert.Equal(99600m, decimal.Parse(bobChkRow.Cell(6).GetString()));
            }

            // 4. Log back in as Alice and confirm zero leakage from Bob
            bankService.Logout();
            var reLoginAlice = bankService.Login("alice", "Password@123");
            Assert.True(reLoginAlice.Success);
            var reloadedAliceChk = bankService.Accounts.First(a => a.Id == aliceChkId);
            Assert.Equal(101200m, reloadedAliceChk.Balance);
            Assert.DoesNotContain(bankService.Transactions, t => t.Title.Contains("Landlord"));
            Assert.Contains(bankService.Transactions, t => t.Title.Contains("Salary"));

            // 5. Log back in as Bob and confirm zero leakage from Alice
            bankService.Logout();
            var reLoginBob = bankService.Login("bob", "Password@456");
            Assert.True(reLoginBob.Success);
            var reloadedBobChk = bankService.Accounts.First(a => a.Id == bobChkId);
            Assert.Equal(99600m, reloadedBobChk.Balance);
            Assert.Contains(bankService.Transactions, t => t.Title.Contains("Landlord"));
            Assert.DoesNotContain(bankService.Transactions, t => t.Title.Contains("Salary"));
        }
        finally
        {
            if (File.Exists(testDbPath))
            {
                File.Delete(testDbPath);
            }
        }
    }
}