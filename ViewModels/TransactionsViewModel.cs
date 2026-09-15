using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ApexBank.Models;
using ApexBank.Services;

namespace ApexBank.ViewModels;

public class TransactionsViewModel : ViewModelBase
{
    private readonly BankService _bankService;

    private string _searchText = string.Empty;
    private string _selectedCategory = "All";
    private string _selectedType = "All";
    private string _selectedSortOption = "Date: Newest";
    private string _selectedAccountId = "All";
    private Transaction? _selectedTransaction;

    public ObservableCollection<BankAccount> Accounts { get; } = new();
    public ObservableCollection<Transaction> FilteredTransactions { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string SelectedAccountId
    {
        get => _selectedAccountId;
        set
        {
            if (SetProperty(ref _selectedAccountId, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public Transaction? SelectedTransaction
    {
        get => _selectedTransaction;
        set => SetProperty(ref _selectedTransaction, value);
    }

    public int TotalCount => _bankService.Transactions.Count;
    public int FilteredCount => FilteredTransactions.Count;
    public decimal TotalInflow => FilteredTransactions.Where(t => t.Type == TransactionType.Credit).Sum(t => t.Amount);
    public decimal TotalOutflow => FilteredTransactions.Where(t => t.Type == TransactionType.Debit).Sum(t => Math.Abs(t.Amount));
    public decimal NetVolume => TotalInflow - TotalOutflow;

    public ICommand SetCategoryCommand { get; }
    public ICommand SetTypeCommand { get; }
    public ICommand SetSortOptionCommand { get; }
    public ICommand SetAccountCommand { get; }
    public ICommand ResetFiltersCommand { get; }
    public ICommand SelectTransactionCommand { get; }
    public ICommand CloseDetailsCommand { get; }
    public ICommand OpenDatabaseFileCommand { get; }

    public TransactionsViewModel()
    {
        _bankService = BankService.Instance;

        SetCategoryCommand = new RelayCommand(p => { if (p is string cat) SelectedCategory = cat; });
        SetTypeCommand = new RelayCommand(p => { if (p is string type) SelectedType = type; });
        SetSortOptionCommand = new RelayCommand(p => { if (p is string sort) SelectedSortOption = sort; });
        SetAccountCommand = new RelayCommand(p => { if (p is string accId) SelectedAccountId = accId; });

        ResetFiltersCommand = new RelayCommand(() =>
        {
            _searchText = string.Empty;
            _selectedCategory = "All";
            _selectedType = "All";
            _selectedSortOption = "Date: Newest";
            _selectedAccountId = "All";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(SelectedType));
            OnPropertyChanged(nameof(SelectedSortOption));
            OnPropertyChanged(nameof(SelectedAccountId));

            ApplyFilterAndSort();
        });

        SelectTransactionCommand = new RelayCommand(p => { if (p is Transaction tx) SelectedTransaction = tx; });
        CloseDetailsCommand = new RelayCommand(() => SelectedTransaction = null);

        OpenDatabaseFileCommand = new RelayCommand(() =>
        {
            try
            {
                string path = _bankService.GetDatabaseFilePath();
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
            }
            catch
            {
                // Silently ignore or catch
            }
        });

        _bankService.OnDataChanged += ReloadData;
        ReloadData();
    }

    public void ReloadData()
    {
        Accounts.Clear();
        foreach (var acc in _bankService.Accounts)
        {
            Accounts.Add(acc);
        }

        ApplyFilterAndSort();
    }

    public void ApplyFilterAndSort()
    {
        FilteredTransactions.Clear();
        var query = _bankService.Transactions.AsEnumerable();

        // 1. Text Search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string lower = SearchText.Trim().ToLowerInvariant();
            query = query.Where(t =>
                (!string.IsNullOrEmpty(t.Title) && t.Title.ToLowerInvariant().Contains(lower)) ||
                (!string.IsNullOrEmpty(t.Description) && t.Description.ToLowerInvariant().Contains(lower)) ||
                (!string.IsNullOrEmpty(t.Category) && t.Category.ToLowerInvariant().Contains(lower)) ||
                (!string.IsNullOrEmpty(t.ReferenceNumber) && t.ReferenceNumber.ToLowerInvariant().Contains(lower)));
        }

        // 2. Category Filter
        if (SelectedCategory != "All")
        {
            query = query.Where(t => t.Category != null && t.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        // 3. Type Filter
        if (SelectedType != "All")
        {
            if (SelectedType.Equals("Credit", StringComparison.OrdinalIgnoreCase) || SelectedType.Equals("Income", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.Type == TransactionType.Credit);
            }
            else if (SelectedType.Equals("Debit", StringComparison.OrdinalIgnoreCase) || SelectedType.Equals("Expense", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => t.Type == TransactionType.Debit);
            }
        }

        // 4. Account Filter
        if (SelectedAccountId != "All")
        {
            query = query.Where(t => t.AccountId != null && t.AccountId.Equals(SelectedAccountId, StringComparison.OrdinalIgnoreCase));
        }

        // 5. Sorting
        query = SelectedSortOption switch
        {
            "Date: Oldest" => query.OrderBy(t => t.Timestamp),
            "Amount: Highest" => query.OrderByDescending(t => t.Amount),
            "Amount: Lowest" => query.OrderBy(t => t.Amount),
            "Payee: A to Z" => query.OrderBy(t => t.Title),
            "Payee: Z to A" => query.OrderByDescending(t => t.Title),
            _ => query.OrderByDescending(t => t.Timestamp) // Default "Date: Newest"
        };

        foreach (var tx in query)
        {
            FilteredTransactions.Add(tx);
        }

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(TotalInflow));
        OnPropertyChanged(nameof(TotalOutflow));
        OnPropertyChanged(nameof(NetVolume));
    }
}