using System;
using System.Linq;
using ApexBank.Models;
using ApexBank.Services;
using ApexBank.ViewModels;
using Xunit;

namespace Tests;

public class TransactionsLedgerTests
{
    [Fact]
    public void Dashboard_RecentTransactions_CapsAt8Items()
    {
        // Arrange
        var bankService = BankService.Instance;
        bankService.EnsureUserLoaded();

        // Ensure there are at least 10 transactions
        while (bankService.Transactions.Count < 10)
        {
            bankService.DepositFunds(bankService.Accounts.First().Id, 100m, "Test Deposit");
        }

        Assert.True(bankService.Transactions.Count > 8, "Expected more than 8 transactions for testing dashboard cap");

        // Act
        var mainVm = new MainViewModel();

        // Assert
        Assert.True(mainVm.RecentTransactions.Count <= 8);
        Assert.Equal(8, mainVm.RecentTransactions.Count);
        Assert.True(mainVm.FilteredTransactions.Count >= mainVm.RecentTransactions.Count);
    }

    [Fact]
    public void TransactionsViewModel_Sorting_OrdersAccurately()
    {
        // Arrange
        var bankService = BankService.Instance;
        bankService.EnsureUserLoaded();

        var vm = new TransactionsViewModel();
        Assert.NotEmpty(vm.FilteredTransactions);

        // Act & Assert - Amount: Highest
        vm.SetSortOptionCommand.Execute("Amount: Highest");
        var listHighest = vm.FilteredTransactions.ToList();
        for (int i = 0; i < listHighest.Count - 1; i++)
        {
            Assert.True(listHighest[i].Amount >= listHighest[i + 1].Amount);
        }

        // Act & Assert - Amount: Lowest
        vm.SetSortOptionCommand.Execute("Amount: Lowest");
        var listLowest = vm.FilteredTransactions.ToList();
        for (int i = 0; i < listLowest.Count - 1; i++)
        {
            Assert.True(listLowest[i].Amount <= listLowest[i + 1].Amount);
        }

        // Act & Assert - Payee: A to Z
        vm.SetSortOptionCommand.Execute("Payee: A to Z");
        var listAtoZ = vm.FilteredTransactions.ToList();
        for (int i = 0; i < listAtoZ.Count - 1; i++)
        {
            Assert.True(string.Compare(listAtoZ[i].Title, listAtoZ[i + 1].Title, StringComparison.OrdinalIgnoreCase) <= 0);
        }

        // Act & Assert - Payee: Z to A
        vm.SetSortOptionCommand.Execute("Payee: Z to A");
        var listZtoA = vm.FilteredTransactions.ToList();
        for (int i = 0; i < listZtoA.Count - 1; i++)
        {
            Assert.True(string.Compare(listZtoA[i].Title, listZtoA[i + 1].Title, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Act & Assert - Date: Oldest
        vm.SetSortOptionCommand.Execute("Date: Oldest");
        var listOldest = vm.FilteredTransactions.ToList();
        for (int i = 0; i < listOldest.Count - 1; i++)
        {
            Assert.True(listOldest[i].Timestamp <= listOldest[i + 1].Timestamp);
        }

        // Act & Assert - Date: Newest
        vm.SetSortOptionCommand.Execute("Date: Newest");
        var listNewest = vm.FilteredTransactions.ToList();
        for (int i = 0; i < listNewest.Count - 1; i++)
        {
            Assert.True(listNewest[i].Timestamp >= listNewest[i + 1].Timestamp);
        }
    }

    [Fact]
    public void TransactionsViewModel_FilteringAndMetrics_CalculateAccurately()
    {
        // Arrange
        var bankService = BankService.Instance;
        bankService.EnsureUserLoaded();

        var vm = new TransactionsViewModel();
        int initialCount = vm.TotalCount;
        Assert.True(initialCount > 0);

        // Act: Filter by Type Credit
        vm.SetTypeCommand.Execute("Credit");
        Assert.All(vm.FilteredTransactions, t => Assert.Equal(TransactionType.Credit, t.Type));
        Assert.True(vm.TotalInflow >= 0);
        Assert.Equal(0, vm.TotalOutflow);

        // Act: Filter by Type Debit
        vm.SetTypeCommand.Execute("Debit");
        Assert.All(vm.FilteredTransactions, t => Assert.Equal(TransactionType.Debit, t.Type));
        Assert.Equal(0, vm.TotalInflow);
        Assert.True(vm.TotalOutflow >= 0);

        // Act: Reset Filters
        vm.ResetFiltersCommand.Execute(null);
        Assert.Equal("All", vm.SelectedType);
        Assert.Equal("All", vm.SelectedCategory);
        Assert.Equal("Date: Newest", vm.SelectedSortOption);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(initialCount, vm.FilteredCount);
        Assert.Equal(vm.TotalInflow - vm.TotalOutflow, vm.NetVolume);

        // Act: Search Filter
        var sampleTx = vm.FilteredTransactions.First();
        vm.SearchText = sampleTx.Title[..Math.Min(4, sampleTx.Title.Length)];
        Assert.NotEmpty(vm.FilteredTransactions);
        Assert.All(vm.FilteredTransactions, t =>
            Assert.True(
                t.Title.Contains(vm.SearchText, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(vm.SearchText, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Contains(vm.SearchText, StringComparison.OrdinalIgnoreCase) ||
                t.ReferenceNumber.Contains(vm.SearchText, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    [Fact]
    public void InApp_Navigation_And_LedgerIntegration_WorkProperly()
    {
        // Arrange
        var bankService = BankService.Instance;
        bankService.EnsureUserLoaded();
        var mainVm = new MainViewModel();

        // Initially on Dashboard
        Assert.True(mainVm.IsDashboardView);
        Assert.False(mainVm.IsTransactionsView);

        // Act: Navigate to Transactions view via NavigateCommand
        mainVm.NavigateCommand.Execute("Transactions");

        // Assert: Switched cleanly inside app
        Assert.Equal("Transactions", mainVm.CurrentView);
        Assert.True(mainVm.IsTransactionsView);
        Assert.False(mainVm.IsDashboardView);

        // Verify Ledger property is active and populated
        Assert.NotNull(mainVm.Ledger);
        Assert.NotEmpty(mainVm.Ledger.FilteredTransactions);

        // Act: Test OpenTransactionsWindowCommand also navigates in-app
        mainVm.CurrentView = "Dashboard";
        Assert.True(mainVm.IsDashboardView);
        mainVm.OpenTransactionsWindowCommand.Execute(null);
        Assert.True(mainVm.IsTransactionsView);
        Assert.Equal("Transactions", mainVm.CurrentView);
    }
}
