using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ApexBank.ViewModels;

namespace ApexBank.WinUI.Views;

public sealed partial class CardsView : UserControl
{
    public CardsView()
    {
        this.InitializeComponent();
    }

    private void OnlinePurchases_Toggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is ToggleSwitch ts)
        {
            if (vm.Card != null && vm.Card.OnlinePurchasesEnabled != ts.IsOn)
            {
                vm.ToggleOnlinePurchasesCommand.Execute(null);
            }
        }
    }

    private void Contactless_Toggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is ToggleSwitch ts)
        {
            if (vm.Card != null && vm.Card.ContactlessEnabled != ts.IsOn)
            {
                vm.ToggleContactlessCommand.Execute(null);
            }
        }
    }

    private void International_Toggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is ToggleSwitch ts)
        {
            if (vm.Card != null && vm.Card.InternationalPaymentsEnabled != ts.IsOn)
            {
                vm.ToggleInternationalCommand.Execute(null);
            }
        }
    }
}

