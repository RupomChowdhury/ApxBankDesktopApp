using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ApexBank.ViewModels;

namespace ApexBank.WinUI;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "APX Bank PLC - Modern Digital Banking";

        // Setup Windows 11 Fluent Mica glass backdrop
        this.SystemBackdrop = new MicaBackdrop();

        // Extend content into title bar and register custom title bar
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);

        // Bind ViewModel to root element
        RootGrid.DataContext = ViewModel;

        // Synchronize NavigationView selection with ViewModel.CurrentView
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Set initial selected nav item
        SyncNavSelection();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.CurrentView))
        {
            SyncNavSelection();
        }
    }

    private void SyncNavSelection()
    {
        if (MainNavView == null) return;

        foreach (var item in MainNavView.MenuItems.OfType<NavigationViewItem>())
        {
            if (string.Equals(item.Tag as string, ViewModel.CurrentView, StringComparison.OrdinalIgnoreCase))
            {
                if (!ReferenceEquals(MainNavView.SelectedItem, item))
                {
                    MainNavView.SelectedItem = item;
                }
                break;
            }
        }
    }

    private void MainNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item && item.Tag is string viewTag)
        {
            if (ViewModel.CurrentView != viewTag)
            {
                ViewModel.NavigateCommand.Execute(viewTag);
            }
        }
    }

    private void LoginPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            ViewModel.LoginPassword = pb.Password;
        }
    }

    private void RegPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            ViewModel.RegPassword = pb.Password;
        }
    }

    private void RegConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            ViewModel.RegConfirmPassword = pb.Password;
        }
    }
}

