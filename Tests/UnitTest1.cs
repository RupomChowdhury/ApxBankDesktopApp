using System;
using System.Linq;
using ApexBank.ViewModels;
using Xunit;

namespace Tests;

public class TransferTests
{
    [Fact]
    public void MultipleTransfers_DoNotCrashAndPreserveSelectedAccount()
    {
        // Arrange
        var vm = new MainViewModel();
        Assert.NotNull(vm.SelectedAccount);
        var initialBalance = vm.SelectedAccount.Balance;

        // Act: Execute 5 consecutive transfers
        for (int i = 1; i <= 5; i++)
        {
            vm.TransferAmount = 50m;
            vm.TransferRecipientName = "Sarah Connor";
            vm.TransferCategory = "Transfer";
            vm.TransferNote = $"Transfer #{i}";

            // Execute command
            vm.ExecuteTransferCommand.Execute(null);

            // Assert each time
            Assert.NotNull(vm.SelectedAccount);
            Assert.Equal("Success", vm.ToastType);
            Assert.Contains("Successfully transferred", vm.ToastMessage);
        }

        // Final balance check: 5 * $50 = $250 deducted
        Assert.Equal(initialBalance - 250m, vm.SelectedAccount.Balance);
    }

    [Fact]
    public void MultipleDeposits_DoNotCrashAndPreserveSelectedAccount()
    {
        // Arrange
        var vm = new MainViewModel();
        Assert.NotNull(vm.SelectedAccount);
        var initialBalance = vm.SelectedAccount.Balance;

        // Act: Execute 3 consecutive deposits
        for (int i = 1; i <= 3; i++)
        {
            vm.DepositAmount = 100m;
            vm.ExecuteDepositCommand.Execute(null);

            Assert.NotNull(vm.SelectedAccount);
            Assert.Equal("Success", vm.ToastType);
        }

        Assert.Equal(initialBalance + 300m, vm.SelectedAccount.Balance);
    }

    [Fact]
    public void TransferWithInvalidData_ShowsErrorToastAndDoesNotCrash()
    {
        // Arrange
        var vm = new MainViewModel();
        
        // Amount <= 0
        vm.TransferAmount = 0m;
        vm.ExecuteTransferCommand.Execute(null);
        Assert.Equal("Error", vm.ToastType);

        // Negative Amount
        vm.TransferAmount = -50m;
        vm.ExecuteTransferCommand.Execute(null);
        Assert.Equal("Error", vm.ToastType);

        // Empty recipient
        vm.TransferAmount = 100m;
        vm.TransferRecipientName = "";
        vm.ExecuteTransferCommand.Execute(null);
        Assert.Equal("Error", vm.ToastType);
    }

    [Fact]
    public void WpfComboBoxNullSimulation_DoesNotCauseCrash()
    {
        var vm = new MainViewModel();

        // Simulate WPF TwoWay binding attempting to push null into SelectedAccount
        vm.SelectedAccount = null!;
        Assert.NotNull(vm.SelectedAccount);

        // Perform transfer - should succeed smoothly without NullReferenceException
        vm.TransferAmount = 10m;
        vm.TransferRecipientName = "Sarah Connor";
        vm.ExecuteTransferCommand.Execute(null);

        Assert.Equal("Success", vm.ToastType);
        Assert.NotNull(vm.SelectedAccount);
    }

    [Fact]
    public void CardRules_ToggleCommands_UpdateRulesAndNotify()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.Card);

        bool initialOnline = vm.Card.OnlinePurchasesEnabled;
        vm.ToggleOnlinePurchasesCommand.Execute(null);
        Assert.Equal(!initialOnline, vm.Card.OnlinePurchasesEnabled);
        Assert.Contains("Online checkouts", vm.ToastMessage);

        bool initialContactless = vm.Card.ContactlessEnabled;
        vm.ToggleContactlessCommand.Execute(null);
        Assert.Equal(!initialContactless, vm.Card.ContactlessEnabled);
        Assert.Contains("Contactless NFC", vm.ToastMessage);

        bool initialInternational = vm.Card.InternationalPaymentsEnabled;
        vm.ToggleInternationalCommand.Execute(null);
        Assert.Equal(!initialInternational, vm.Card.InternationalPaymentsEnabled);
        Assert.Contains("International transactions", vm.ToastMessage);
    }

    [Fact]
    public void DashboardMetrics_Properties_CalculateAccurately()
    {
        var vm = new MainViewModel();
        Assert.True(vm.TotalNetWorth > 0);
        Assert.True(vm.TotalMonthlyInflow >= 0);
        Assert.True(vm.TotalMonthlyOutflow >= 0);
        Assert.Equal(vm.TotalMonthlyInflow - vm.TotalMonthlyOutflow, vm.NetMonthlySavings);

        // Test navigation views
        vm.CurrentView = "Dashboard";
        Assert.True(vm.IsDashboardView);
        Assert.False(vm.IsCardsView);

        vm.CurrentView = "Cards";
        Assert.True(vm.IsCardsView);
        Assert.False(vm.IsDashboardView);

        vm.CurrentView = "Transfers";
        Assert.True(vm.IsTransfersView);

        vm.CurrentView = "Transactions";
        Assert.True(vm.IsTransactionsView);

        vm.CurrentView = "Analytics";
        Assert.True(vm.IsAnalyticsView);
    }
}

