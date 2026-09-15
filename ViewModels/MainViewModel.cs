using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if WINUI
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#else
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
#endif
using System.Windows.Input;
using ApexBank.Models;
using ApexBank.Services;

namespace ApexBank.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly BankService _bankService;
    private readonly DispatcherTimer _toastTimer;

    // Authentication State
    private bool _isAuthenticated;
    private bool _isRegisterMode;
    private string _loginUsername = "alexander";
    private string _loginPassword = string.Empty;
    private string _loginErrorMessage = string.Empty;

    private string _regFullName = string.Empty;
    private string _regUsername = string.Empty;
    private string _regEmail = string.Empty;
    private string _regPassword = string.Empty;
    private string _regConfirmPassword = string.Empty;
    private string _regErrorMessage = string.Empty;

    // Main Navigation State
    private string _currentView = "Dashboard";
    private BankAccount _selectedAccount;
    private string _searchText = string.Empty;
    private string _selectedCategoryFilter = "All";
    private Transaction? _selectedTransaction;
    private bool _isCardNumberRevealed;
    private bool _isDepositDialogOpen;
    private decimal _depositAmount = 50000m;
    private string _depositSource = "Instant Bank Deposit";

    // Dashboard Quick Send
    private string _transferRecipientName = "Tanvir Hossain";
    private decimal _transferAmount = 5000m;
    private string _transferCategory = "Transfer";
    private string _transferNote = "Shared dinner & travel split";
    private Beneficiary? _selectedBeneficiary;

    // Transfer Hub State & Sub-Tabs
    private string _selectedTransferTab = "SameBank"; // SameBank, ExternalBD, CardPay
    private string _sameBankMode = "Own"; // Own, Peer
    private BankAccount? _selectedTargetAccount;
    private string _sameBankRecipientInput = string.Empty;
    private decimal _sameBankAmount = 5000m;
    private string _sameBankNote = "Personal fund transfer";

    // External Bangladeshi Bank Transfer Form
    private string _selectedBdChannel = "NPSB";
    private BankInfo? _selectedBdBank;
    private string _bdRecipientAccount = string.Empty;
    private string _bdRecipientName = string.Empty;
    private string _bdRoutingNumber = "060260724";
    private decimal _bdAmount = 10000m;
    private string _bdNote = "Business payment";
    private bool _bdSaveBeneficiary = true;

    // Pay via Card Form
    private VirtualCard? _selectedPaymentCard;
    private string _selectedBiller = "DESCO Prepaid Electricity";
    private string _billerAccountNo = "1049281920";
    private decimal _cardPayAmount = 2500m;
    private string _cardPayCvv = string.Empty;
    private string _cardPayCategory = "Utilities";
    private string _cardPayNote = "Monthly electricity recharge";

    // Beneficiary Modal
    private bool _isAddBeneficiaryOpen;
    private string _newBenName = string.Empty;
    private string _newBenAccount = string.Empty;
    private BankInfo? _newBenBank;
    private string _newBenRouting = string.Empty;
    private string _newBenChannel = "NPSB";
    private string _newBenCategory = "Personal";

    // Notification Center Flyout
    private bool _isNotificationsOpen;

    // Toast State
    private string _toastMessage = string.Empty;
    private string _toastType = "Success";
    private bool _isToastVisible;

    // Observable Collections
    public ObservableCollection<BankAccount> Accounts { get; } = new();
    public ObservableCollection<Transaction> FilteredTransactions { get; } = new();
    public ObservableCollection<Transaction> RecentTransactions { get; } = new();
    public ObservableCollection<Beneficiary> Beneficiaries { get; } = new();
    public ObservableCollection<SpendingCategory> SpendingCategories { get; } = new();
    public ObservableCollection<AuditLog> AuditLogs { get; } = new();
    public ObservableCollection<VirtualCard> Cards { get; } = new();
    public ObservableCollection<BankInfo> SupportedBanks { get; } = new();
    public ObservableCollection<NotificationItem> Notifications { get; } = new();
    public ObservableCollection<string> BdChannels { get; } = new() { "NPSB", "BEFTN", "RTGS", "MFS", "VISA" };
    public ObservableCollection<string> BillersList { get; } = new()
    {
        "DESCO Prepaid Electricity",
        "DPDC Electricity",
        "Titas Gas Transmission",
        "Dhaka WASA Water",
        "Dot Internet Broadband",
        "Grameenphone FlexiPlan",
        "Robi Axiata Bill",
        "Banglalink Enterprise",
        "Starlink Global High-Speed",
        "Amazon Web Services Cloud",
        "Apple Services & iCloud",
        "Netflix 4K Ultra Plan"
    };

    public ObservableCollection<string> BillerCategories => BillersList;

    public VirtualCard Card => _bankService.Card;

    public bool IsDashboardView => CurrentView == "Dashboard";
    public bool IsTransfersView => CurrentView == "Transfers";
    public bool IsCardsView => CurrentView == "Cards";
    public bool IsAnalyticsView => CurrentView == "Analytics";
    public bool IsTransactionsView => CurrentView == "Transactions";

    public TransactionsViewModel Ledger { get; } = new();

    #region Auth Properties

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set => SetProperty(ref _isRegisterMode, value);
    }

    public string LoginUsername
    {
        get => _loginUsername;
        set => SetProperty(ref _loginUsername, value);
    }

    public string LoginPassword
    {
        get => _loginPassword;
        set => SetProperty(ref _loginPassword, value);
    }

    public string LoginErrorMessage
    {
        get => _loginErrorMessage;
        set => SetProperty(ref _loginErrorMessage, value);
    }

    public string RegFullName
    {
        get => _regFullName;
        set => SetProperty(ref _regFullName, value);
    }

    public string RegUsername
    {
        get => _regUsername;
        set => SetProperty(ref _regUsername, value);
    }

    public string RegEmail
    {
        get => _regEmail;
        set => SetProperty(ref _regEmail, value);
    }

    public string RegPassword
    {
        get => _regPassword;
        set => SetProperty(ref _regPassword, value);
    }

    public string RegConfirmPassword
    {
        get => _regConfirmPassword;
        set => SetProperty(ref _regConfirmPassword, value);
    }

    public string RegErrorMessage
    {
        get => _regErrorMessage;
        set => SetProperty(ref _regErrorMessage, value);
    }

    public User? CurrentUser => _bankService.CurrentUser;
    public string CurrentUserName => CurrentUser?.FullName ?? "Alexander Cross";
    public string CurrentUserEmail => CurrentUser?.Email ?? "alexander.c@apex.io";
    public string CurrentUserInitials
    {
        get
        {
            if (CurrentUser == null || string.IsNullOrWhiteSpace(CurrentUser.FullName))
                return "AC";
            var parts = CurrentUser.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        }
    }

    #endregion

    #region Main View & Account Selection

    public string CurrentView
    {
        get => _currentView;
        set
        {
            if (SetProperty(ref _currentView, value))
            {
                OnPropertyChanged(nameof(IsDashboardView));
                OnPropertyChanged(nameof(IsTransfersView));
                OnPropertyChanged(nameof(IsCardsView));
                OnPropertyChanged(nameof(IsAnalyticsView));
                OnPropertyChanged(nameof(IsTransactionsView));
            }
        }
    }

    public BankAccount SelectedAccount
    {
        get => _selectedAccount ?? Accounts.FirstOrDefault(a => a.IsActive) ?? _bankService?.ActiveAccount ?? _bankService?.Accounts.FirstOrDefault()!;
        set
        {
            if (value == null) return;
            if (SetProperty(ref _selectedAccount, value))
            {
                _bankService.SetActiveAccount(value);
                ApplyFilter();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public Transaction? SelectedTransaction
    {
        get => _selectedTransaction;
        set => SetProperty(ref _selectedTransaction, value);
    }

    #endregion

    #region Card Studio Properties & Interactive Rules

    public bool IsCardNumberRevealed
    {
        get => _isCardNumberRevealed;
        set
        {
            if (SetProperty(ref _isCardNumberRevealed, value))
            {
                OnPropertyChanged(nameof(DisplayedCardNumber));
                OnPropertyChanged(nameof(DisplayedCvv));
                OnPropertyChanged(nameof(RevealButtonText));
            }
        }
    }

    public string DisplayedCardNumber => IsCardNumberRevealed ? (Card?.FullNumber ?? "") : (Card?.MaskedNumber ?? "");
    public string DisplayedCvv => IsCardNumberRevealed ? (Card?.Cvv ?? "") : "•••";
    public string RevealButtonText => IsCardNumberRevealed ? "Hide Details" : "Reveal Card";

    public double CardDailyLimitValue
    {
        get => (double)(Card?.DailyLimit ?? 100000m);
        set
        {
            if (Card != null && Card.DailyLimit != (decimal)value)
            {
                Card.DailyLimit = (decimal)value;
                _bankService.UpdateCardSecuritySettings(Card.Id, dailyLimit: (decimal)value);
                OnPropertyChanged(nameof(CardDailyLimitValue));
                OnPropertyChanged(nameof(CardDailyLimitText));
            }
        }
    }

    public string CardDailyLimitText => $"DAILY LIMIT: ৳{Card?.DailyLimit:N2}";

    #endregion

    #region Transfer Hub Properties (Same-Bank, External BD, Card Pay)

    public string SelectedTransferTab
    {
        get => _selectedTransferTab;
        set
        {
            if (SetProperty(ref _selectedTransferTab, value))
            {
                OnPropertyChanged(nameof(IsSameBankTab));
                OnPropertyChanged(nameof(IsExternalBdTab));
                OnPropertyChanged(nameof(IsCardPayTab));
            }
        }
    }

    public bool IsSameBankTab => SelectedTransferTab == "SameBank";
    public bool IsExternalBdTab => SelectedTransferTab == "ExternalBD";
    public bool IsCardPayTab => SelectedTransferTab == "CardPay";

    public string SameBankMode
    {
        get => _sameBankMode;
        set
        {
            if (SetProperty(ref _sameBankMode, value))
            {
                OnPropertyChanged(nameof(IsSameBankOwnAccounts));
                OnPropertyChanged(nameof(IsSameBankPeer));
            }
        }
    }

    public bool IsSameBankOwnAccounts => SameBankMode == "Own";
    public bool IsSameBankPeer => SameBankMode == "Peer";

    public BankAccount? SelectedTargetAccount
    {
        get => _selectedTargetAccount ?? Accounts.FirstOrDefault(a => a.Id != SelectedAccount?.Id);
        set => SetProperty(ref _selectedTargetAccount, value);
    }

    public string SameBankRecipientInput
    {
        get => _sameBankRecipientInput;
        set => SetProperty(ref _sameBankRecipientInput, value);
    }

    public decimal SameBankAmount
    {
        get => _sameBankAmount;
        set
        {
            if (SetProperty(ref _sameBankAmount, value))
            {
                OnPropertyChanged(nameof(SameBankTotalDebit));
            }
        }
    }

    public string SameBankNote
    {
        get => _sameBankNote;
        set => SetProperty(ref _sameBankNote, value);
    }

    public decimal SameBankFee => 0.00m;
    public decimal SameBankTotalDebit => SameBankAmount;

    // External BD Transfer Properties
    public string SelectedBdChannel
    {
        get => _selectedBdChannel;
        set
        {
            if (SetProperty(ref _selectedBdChannel, value))
            {
                OnPropertyChanged(nameof(BdFee));
                OnPropertyChanged(nameof(BdTotalDebit));
                OnPropertyChanged(nameof(BdFeeNotice));
            }
        }
    }

    public BankInfo? SelectedBdBank
    {
        get => _selectedBdBank ?? SupportedBanks.FirstOrDefault();
        set
        {
            if (SetProperty(ref _selectedBdBank, value) && value != null)
            {
                if (!string.IsNullOrWhiteSpace(value.DefaultRoutingNumber))
                {
                    BdRoutingNumber = value.DefaultRoutingNumber;
                }
            }
        }
    }

    public string BdRecipientAccount
    {
        get => _bdRecipientAccount;
        set => SetProperty(ref _bdRecipientAccount, value);
    }

    public string BdRecipientName
    {
        get => _bdRecipientName;
        set => SetProperty(ref _bdRecipientName, value);
    }

    public string BdRoutingNumber
    {
        get => _bdRoutingNumber;
        set => SetProperty(ref _bdRoutingNumber, value);
    }

    public decimal BdAmount
    {
        get => _bdAmount;
        set
        {
            if (SetProperty(ref _bdAmount, value))
            {
                OnPropertyChanged(nameof(BdTotalDebit));
            }
        }
    }

    public string BdNote
    {
        get => _bdNote;
        set => SetProperty(ref _bdNote, value);
    }

    public bool BdSaveBeneficiary
    {
        get => _bdSaveBeneficiary;
        set => SetProperty(ref _bdSaveBeneficiary, value);
    }

    public decimal BdFee => BankService.GetTransferFee(SelectedBdChannel);
    public decimal BdTotalDebit => BdAmount + BdFee;
    public string BdFeeNotice => $"Channel Fee: ৳{BdFee:N2} | Total: ৳{BdTotalDebit:N2}";

    // Pay via Card Properties
    public VirtualCard? SelectedPaymentCard
    {
        get => _selectedPaymentCard ?? Card;
        set => SetProperty(ref _selectedPaymentCard, value);
    }

    public string SelectedBiller
    {
        get => _selectedBiller;
        set => SetProperty(ref _selectedBiller, value);
    }

    public string BillerAccountNo
    {
        get => _billerAccountNo;
        set => SetProperty(ref _billerAccountNo, value);
    }

    public decimal CardPayAmount
    {
        get => _cardPayAmount;
        set
        {
            if (SetProperty(ref _cardPayAmount, value))
            {
                OnPropertyChanged(nameof(CardPayTotalDebit));
            }
        }
    }

    public string CardPayCvv
    {
        get => _cardPayCvv;
        set => SetProperty(ref _cardPayCvv, value);
    }

    public string CardPayCategory
    {
        get => _cardPayCategory;
        set => SetProperty(ref _cardPayCategory, value);
    }

    public string CardPayNote
    {
        get => _cardPayNote;
        set => SetProperty(ref _cardPayNote, value);
    }

    public decimal CardPayFee => 10.00m;
    public decimal CardPayTotalDebit => CardPayAmount + CardPayFee;

    #endregion

    #region Add Beneficiary Modal Properties

    public bool IsAddBeneficiaryOpen
    {
        get => _isAddBeneficiaryOpen;
        set => SetProperty(ref _isAddBeneficiaryOpen, value);
    }

    public string NewBenName
    {
        get => _newBenName;
        set => SetProperty(ref _newBenName, value);
    }

    public string NewBenAccount
    {
        get => _newBenAccount;
        set => SetProperty(ref _newBenAccount, value);
    }

    public BankInfo? NewBenBank
    {
        get => _newBenBank ?? SupportedBanks.FirstOrDefault();
        set
        {
            if (SetProperty(ref _newBenBank, value) && value != null)
            {
                NewBenRouting = value.DefaultRoutingNumber;
            }
        }
    }

    public string NewBenRouting
    {
        get => _newBenRouting;
        set => SetProperty(ref _newBenRouting, value);
    }

    public string NewBenChannel
    {
        get => _newBenChannel;
        set => SetProperty(ref _newBenChannel, value);
    }

    public string NewBenCategory
    {
        get => _newBenCategory;
        set => SetProperty(ref _newBenCategory, value);
    }

    #endregion

    #region Notification Center Properties

    public bool IsNotificationsOpen
    {
        get => _isNotificationsOpen;
        set => SetProperty(ref _isNotificationsOpen, value);
    }

    public int UnreadNotificationCount => Notifications.Count(n => !n.IsRead);
    public bool HasUnreadNotifications => UnreadNotificationCount > 0;

    #endregion

    #region Financial Metrics & Net Worth

    public decimal TotalNetWorth => _bankService.GetTotalNetWorth();
    public decimal TotalMonthlyInflow => _bankService.GetTotalMonthlyInflow();
    public decimal TotalMonthlyOutflow => _bankService.GetTotalMonthlyOutflow();
    public decimal NetMonthlySavings => TotalMonthlyInflow - TotalMonthlyOutflow;
    public double SavingsRate => TotalMonthlyInflow > 0 ? (double)((NetMonthlySavings / TotalMonthlyInflow) * 100m) : 0;

    #endregion

    #region Toast & Dialog Properties

    public bool IsDepositDialogOpen
    {
        get => _isDepositDialogOpen;
        set => SetProperty(ref _isDepositDialogOpen, value);
    }

    public decimal DepositAmount
    {
        get => _depositAmount;
        set => SetProperty(ref _depositAmount, value);
    }

    public string DepositSource
    {
        get => _depositSource;
        set => SetProperty(ref _depositSource, value);
    }

    public string TransferRecipientName
    {
        get => _transferRecipientName;
        set => SetProperty(ref _transferRecipientName, value);
    }

    public decimal TransferAmount
    {
        get => _transferAmount;
        set => SetProperty(ref _transferAmount, value);
    }

    public string TransferCategory
    {
        get => _transferCategory;
        set => SetProperty(ref _transferCategory, value);
    }

    public string TransferNote
    {
        get => _transferNote;
        set => SetProperty(ref _transferNote, value);
    }

    public Beneficiary? SelectedBeneficiary
    {
        get => _selectedBeneficiary;
        set
        {
            if (SetProperty(ref _selectedBeneficiary, value) && value != null)
            {
                TransferRecipientName = value.Name;
                BdRecipientName = value.Name;
                BdRecipientAccount = value.AccountNumber;
                BdRoutingNumber = value.RoutingNumber;
                SelectedBdChannel = string.IsNullOrWhiteSpace(value.Channel) ? "NPSB" : value.Channel;
                var matchBank = SupportedBanks.FirstOrDefault(b => b.BankName.Equals(value.BankName, StringComparison.OrdinalIgnoreCase));
                if (matchBank != null) SelectedBdBank = matchBank;
            }
        }
    }

    public string ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    public string ToastType
    {
        get => _toastType;
        set => SetProperty(ref _toastType, value);
    }

    public bool IsToastVisible
    {
        get => _isToastVisible;
        set => SetProperty(ref _isToastVisible, value);
    }

    #endregion

    // Commands Declaration
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand SwitchAuthModeCommand { get; }
    public ICommand QuickDemoLoginCommand { get; }
    public ICommand OpenDatabaseFileCommand { get; }
    public ICommand RefreshLogsCommand { get; }

    public ICommand NavigateCommand { get; }
    public ICommand SelectAccountCommand { get; }
    public ICommand SetFilterCategoryCommand { get; }
    public ICommand ToggleRevealCardCommand { get; }
    public ICommand ToggleFreezeCardCommand { get; }
    public ICommand QuickAmountCommand { get; }
    public ICommand SelectBeneficiaryCommand { get; }
    public ICommand ExecuteTransferCommand { get; }

    // Multi-Rail Transfer Commands
    public ICommand SetTransferTabCommand { get; }
    public ICommand SetSameBankModeCommand { get; }
    public ICommand ExecuteSameBankTransferCommand { get; }
    public ICommand ExecuteBdTransferCommand { get; }
    public ICommand ExecuteCardPayCommand { get; }

    // Card Security Toggles
    public ICommand ToggleOnlinePurchasesCommand { get; }
    public ICommand ToggleContactlessCommand { get; }
    public ICommand ToggleInternationalCommand { get; }
    public ICommand GenerateNewCardCommand { get; }
    public ICommand SelectCardCommand { get; }

    // Beneficiary Commands
    public ICommand OpenAddBeneficiaryCommand { get; }
    public ICommand CloseAddBeneficiaryCommand { get; }
    public ICommand SaveBeneficiaryCommand { get; }
    public ICommand DeleteBeneficiaryCommand { get; }

    // Notification Center Commands
    public ICommand ToggleNotificationsCommand { get; }
    public ICommand CloseNotificationsCommand { get; }
    public ICommand MarkAllNotificationsReadCommand { get; }

    // Deposit & Receipt Commands
    public ICommand OpenDepositDialogCommand { get; }
    public ICommand CloseDepositDialogCommand { get; }
    public ICommand ExecuteDepositCommand { get; }
    public ICommand DownloadReceiptPdfCommand { get; }

    // Search & Misc Commands
    public ICommand ClearSearchCommand { get; }
    public ICommand SelectTransactionCommand { get; }
    public ICommand CloseSelectedTransactionCommand { get; }
    public ICommand OpenTransactionsWindowCommand { get; }

    public MainViewModel()
    {
        _bankService = BankService.Instance;
        _bankService.EnsureUserLoaded();
        _selectedAccount = _bankService.ActiveAccount;

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _toastTimer.Tick += (s, e) =>
        {
            IsToastVisible = false;
            _toastTimer.Stop();
        };

        // Authentication Commands
        LoginCommand = new RelayCommand(p =>
        {
            string password = (p is PasswordBox pb) ? pb.Password : LoginPassword;
            if (string.IsNullOrWhiteSpace(LoginUsername))
            {
                LoginErrorMessage = "Please enter your username or email.";
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                LoginErrorMessage = "Please enter your password.";
                return;
            }

            var result = _bankService.Login(LoginUsername, password);
            if (result.Success)
            {
                LoginErrorMessage = string.Empty;
                LoginPassword = string.Empty;
                IsAuthenticated = true;
                RefreshData();
                ShowToast(result.Message, "Success");
            }
            else
            {
                LoginErrorMessage = result.Message;
                ShowToast(result.Message, "Error");
            }
        });

        RegisterCommand = new RelayCommand(p =>
        {
            if (string.IsNullOrWhiteSpace(RegFullName))
            {
                RegErrorMessage = "Full Name is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(RegUsername))
            {
                RegErrorMessage = "Username is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(RegEmail) || !RegEmail.Contains('@'))
            {
                RegErrorMessage = "A valid email is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(RegPassword) || RegPassword.Length < 6)
            {
                RegErrorMessage = "Password must be at least 6 characters.";
                return;
            }

            if (RegPassword != RegConfirmPassword)
            {
                RegErrorMessage = "Passwords do not match.";
                return;
            }

            var result = _bankService.Register(RegFullName, RegUsername, RegEmail, RegPassword);
            if (result.Success)
            {
                RegErrorMessage = string.Empty;
                RegPassword = string.Empty;
                RegConfirmPassword = string.Empty;
                IsAuthenticated = true;
                RefreshData();
                ShowToast(result.Message, "Success");
            }
            else
            {
                RegErrorMessage = result.Message;
                ShowToast(result.Message, "Error");
            }
        });

        LogoutCommand = new RelayCommand(() =>
        {
            _bankService.Logout();
            IsAuthenticated = false;
            CurrentView = "Dashboard";
            ShowToast("You have been securely signed out.", "Info");
        });

        SwitchAuthModeCommand = new RelayCommand(() =>
        {
            IsRegisterMode = !IsRegisterMode;
            LoginErrorMessage = string.Empty;
            RegErrorMessage = string.Empty;
        });

        QuickDemoLoginCommand = new RelayCommand(() =>
        {
            var result = _bankService.Login("alexander", "Apex@2026");
            if (result.Success)
            {
                LoginErrorMessage = string.Empty;
                IsAuthenticated = true;
                RefreshData();
                ShowToast("Logged in as Demo User: Alexander Cross (BDT)", "Success");
            }
            else
            {
                ShowToast(result.Message, "Error");
            }
        });

        OpenDatabaseFileCommand = new RelayCommand(() =>
        {
            try
            {
                string path = _bankService.GetDatabaseFilePath();
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                    ShowToast("Opened live Excel database file.", "Info");
                }
                else
                {
                    ShowToast("Database file not yet created.", "Warning");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Unable to open file: {ex.Message}", "Error");
            }
        });

        RefreshLogsCommand = new RelayCommand(() =>
        {
            AuditLogs.Clear();
            foreach (var log in _bankService.GetAuditLogs())
            {
                AuditLogs.Add(log);
            }
            ShowToast("Audit logs synchronized from Excel.", "Info");
        });

        // Navigation
        NavigateCommand = new RelayCommand(p =>
        {
            if (p is string viewName)
            {
                CurrentView = viewName;
            }
        });

        SelectAccountCommand = new RelayCommand(p =>
        {
            if (p is BankAccount acc)
            {
                SelectedAccount = acc;
            }
        });

        SetFilterCategoryCommand = new RelayCommand(p =>
        {
            if (p is string category)
            {
                SelectedCategoryFilter = category;
            }
        });

        // Card Commands
        ToggleRevealCardCommand = new RelayCommand(() =>
        {
            IsCardNumberRevealed = !IsCardNumberRevealed;
            ShowToast(IsCardNumberRevealed ? "Sensitive card details unmasked" : "Card details masked", "Info");
        });

        ToggleFreezeCardCommand = new RelayCommand(() =>
        {
            _bankService.ToggleCardFreeze();
            OnPropertyChanged(nameof(Card));
            ShowToast(Card.IsFrozen ? "Card frozen. Transactions locked." : "Card unfrozen and active.", Card.IsFrozen ? "Warning" : "Success");
        });

        SelectCardCommand = new RelayCommand(p =>
        {
            if (p is VirtualCard vc)
            {
                _bankService.SetActiveCard(vc);
                SelectedPaymentCard = vc;
                OnPropertyChanged(nameof(Card));
                OnPropertyChanged(nameof(DisplayedCardNumber));
                OnPropertyChanged(nameof(DisplayedCvv));
                OnPropertyChanged(nameof(CardDailyLimitValue));
                OnPropertyChanged(nameof(CardDailyLimitText));
                ShowToast($"Active card: {vc.MaskedNumber}", "Info");
            }
        });

        GenerateNewCardCommand = new RelayCommand(() =>
        {
            var result = _bankService.GenerateNewCard();
            if (result.Success)
            {
                RefreshData();
                ShowToast(result.Message, "Success");
            }
            else
            {
                ShowToast(result.Message, "Error");
            }
        });

        // Interactive Card Security Toggles
        ToggleOnlinePurchasesCommand = new RelayCommand(() =>
        {
            if (Card == null) return;
            bool newState = !Card.OnlinePurchasesEnabled;
            _bankService.UpdateCardSecuritySettings(Card.Id, online: newState);
            OnPropertyChanged(nameof(Card));
            ShowToast($"Online checkouts {(newState ? "ENABLED" : "DISABLED")} for card.", newState ? "Success" : "Warning");
        });

        ToggleContactlessCommand = new RelayCommand(() =>
        {
            if (Card == null) return;
            bool newState = !Card.ContactlessEnabled;
            _bankService.UpdateCardSecuritySettings(Card.Id, contactless: newState);
            OnPropertyChanged(nameof(Card));
            ShowToast($"Contactless NFC tap-to-pay {(newState ? "ENABLED" : "DISABLED")}.", newState ? "Success" : "Warning");
        });

        ToggleInternationalCommand = new RelayCommand(() =>
        {
            if (Card == null) return;
            bool newState = !Card.InternationalPaymentsEnabled;
            _bankService.UpdateCardSecuritySettings(Card.Id, international: newState);
            OnPropertyChanged(nameof(Card));
            ShowToast($"International transactions {(newState ? "ENABLED" : "DISABLED")}.", newState ? "Success" : "Warning");
        });

        // Transfer Sub-Tabs
        SetTransferTabCommand = new RelayCommand(p =>
        {
            if (p is string tab)
            {
                SelectedTransferTab = tab;
            }
        });

        SetSameBankModeCommand = new RelayCommand(p =>
        {
            if (p is string mode)
            {
                SameBankMode = mode;
            }
        });

        // Execute Same-Bank Transfer (৳0.00 Fee)
        ExecuteSameBankTransferCommand = new RelayCommand(() =>
        {
            try
            {
                var account = SelectedAccount ?? Accounts.FirstOrDefault();
                if (account == null)
                {
                    ShowToast("Please select a source account.", "Error");
                    return;
                }

                string destination = SameBankMode == "Own"
                    ? (SelectedTargetAccount?.Id ?? string.Empty)
                    : SameBankRecipientInput.Trim();

                if (string.IsNullOrWhiteSpace(destination))
                {
                    ShowToast("Please select a destination account or specify a recipient.", "Error");
                    return;
                }

                var result = _bankService.TransferSameBank(account.Id, destination, SameBankAmount, SameBankNote);
                if (result.Success)
                {
                    ShowToast(result.Message, "Success");
                    SameBankNote = string.Empty;
                }
                else
                {
                    ShowToast(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Transfer error: {ex.Message}", "Error");
            }
        });

        // Execute External Bangladeshi Bank Transfer (NPSB/BEFTN/RTGS/MFS/VISA)
        ExecuteBdTransferCommand = new RelayCommand(() =>
        {
            try
            {
                var account = SelectedAccount ?? Accounts.FirstOrDefault();
                if (account == null)
                {
                    ShowToast("Please select a source account.", "Error");
                    return;
                }

                if (SelectedBdBank == null)
                {
                    ShowToast("Please select a destination bank.", "Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(BdRecipientAccount))
                {
                    ShowToast("Please enter a recipient account or wallet number.", "Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(BdRecipientName))
                {
                    ShowToast("Please enter the recipient full name.", "Error");
                    return;
                }

                var result = _bankService.TransferExternalBangladesh(
                    account.Id,
                    SelectedBdBank.BankName,
                    SelectedBdChannel,
                    BdRecipientAccount.Trim(),
                    BdRecipientName.Trim(),
                    BdRoutingNumber.Trim(),
                    BdAmount,
                    BdNote,
                    BdSaveBeneficiary
                );

                if (result.Success)
                {
                    ShowToast(result.Message, "Success");
                    BdNote = string.Empty;
                }
                else
                {
                    ShowToast(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"External transfer error: {ex.Message}", "Error");
            }
        });

        // Execute Pay via Card Command
        ExecuteCardPayCommand = new RelayCommand(() =>
        {
            try
            {
                var card = SelectedPaymentCard ?? Card;
                if (card == null)
                {
                    ShowToast("No active card selected.", "Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(BillerAccountNo))
                {
                    ShowToast("Please enter your biller customer/reference number.", "Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(CardPayCvv))
                {
                    ShowToast("Please enter the card's 3-digit CVV code.", "Error");
                    return;
                }

                var result = _bankService.PayViaCard(
                    card.Id,
                    SelectedBiller,
                    BillerAccountNo.Trim(),
                    CardPayAmount,
                    CardPayCvv.Trim(),
                    CardPayCategory,
                    CardPayNote
                );

                if (result.Success)
                {
                    ShowToast(result.Message, "Success");
                    CardPayCvv = string.Empty;
                    CardPayNote = string.Empty;
                }
                else
                {
                    ShowToast(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Card payment error: {ex.Message}", "Error");
            }
        });

        // Beneficiary Commands
        OpenAddBeneficiaryCommand = new RelayCommand(() =>
        {
            NewBenName = string.Empty;
            NewBenAccount = string.Empty;
            NewBenBank = SupportedBanks.FirstOrDefault();
            NewBenRouting = NewBenBank?.DefaultRoutingNumber ?? "060260724";
            NewBenChannel = "NPSB";
            IsAddBeneficiaryOpen = true;
        });

        CloseAddBeneficiaryCommand = new RelayCommand(() => IsAddBeneficiaryOpen = false);

        SaveBeneficiaryCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(NewBenName) || string.IsNullOrWhiteSpace(NewBenAccount))
            {
                ShowToast("Name and account number are required.", "Error");
                return;
            }

            var ben = new Beneficiary
            {
                Name = NewBenName.Trim(),
                AccountNumber = NewBenAccount.Trim(),
                BankName = NewBenBank?.BankName ?? "BRAC Bank PLC",
                RoutingNumber = string.IsNullOrWhiteSpace(NewBenRouting) ? (NewBenBank?.DefaultRoutingNumber ?? "060260724") : NewBenRouting.Trim(),
                Channel = NewBenChannel,
                Category = NewBenCategory,
                TransferCount = 0
            };

            var parts = ben.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            ben.AvatarInitials = parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant() : parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();

            _bankService.AddBeneficiary(ben);
            IsAddBeneficiaryOpen = false;
            ShowToast($"Beneficiary '{ben.Name}' saved to Excel database.", "Success");
        });

        DeleteBeneficiaryCommand = new RelayCommand(p =>
        {
            if (p is Beneficiary ben)
            {
                _bankService.RemoveBeneficiary(ben.Id);
                ShowToast($"Beneficiary '{ben.Name}' removed.", "Info");
            }
        });

        // Notification Center Commands
        ToggleNotificationsCommand = new RelayCommand(() =>
        {
            IsNotificationsOpen = !IsNotificationsOpen;
            if (IsNotificationsOpen)
            {
                _bankService.MarkAllNotificationsAsRead();
                OnPropertyChanged(nameof(UnreadNotificationCount));
                OnPropertyChanged(nameof(HasUnreadNotifications));
            }
        });

        CloseNotificationsCommand = new RelayCommand(() => IsNotificationsOpen = false);

        MarkAllNotificationsReadCommand = new RelayCommand(() =>
        {
            _bankService.MarkAllNotificationsAsRead();
            OnPropertyChanged(nameof(UnreadNotificationCount));
            OnPropertyChanged(nameof(HasUnreadNotifications));
            ShowToast("All notifications marked as read.", "Info");
        });

        // Quick Amount & Beneficiary Selector
        QuickAmountCommand = new RelayCommand(p =>
        {
            if (decimal.TryParse(p?.ToString(), out var amt))
            {
                TransferAmount = amt;
                SameBankAmount = amt;
                BdAmount = amt;
                CardPayAmount = amt;
            }
        });

        SelectBeneficiaryCommand = new RelayCommand(p =>
        {
            if (p is Beneficiary ben)
            {
                SelectedBeneficiary = ben;
                TransferRecipientName = ben.Name;
                BdRecipientName = ben.Name;
                BdRecipientAccount = ben.AccountNumber;
                BdRoutingNumber = ben.RoutingNumber;
                SelectedBdChannel = string.IsNullOrWhiteSpace(ben.Channel) ? "NPSB" : ben.Channel;
                var matchBank = SupportedBanks.FirstOrDefault(b => b.BankName.Equals(ben.BankName, StringComparison.OrdinalIgnoreCase));
                if (matchBank != null) SelectedBdBank = matchBank;
                SelectedTransferTab = "ExternalBD";
                ShowToast($"Selected payee: {ben.Name} ({ben.BankName})", "Info");
            }
        });

        // Dashboard Transfer Command
        ExecuteTransferCommand = new RelayCommand(() =>
        {
            try
            {
                var account = SelectedAccount ?? Accounts.FirstOrDefault();
                if (account == null)
                {
                    ShowToast("Please select an account first.", "Error");
                    return;
                }

                var result = _bankService.TransferSameBank(account.Id, TransferRecipientName.Trim(), TransferAmount, TransferNote);
                if (result.Success)
                {
                    ShowToast(result.Message, "Success");
                    TransferNote = string.Empty;
                }
                else
                {
                    ShowToast(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Transfer error: {ex.Message}", "Error");
            }
        });

        // Deposit Dialog Commands
        OpenDepositDialogCommand = new RelayCommand(() => IsDepositDialogOpen = true);
        CloseDepositDialogCommand = new RelayCommand(() => IsDepositDialogOpen = false);

        ExecuteDepositCommand = new RelayCommand(() =>
        {
            try
            {
                var account = SelectedAccount ?? Accounts.FirstOrDefault();
                if (account == null)
                {
                    ShowToast("Please select a target account.", "Error");
                    return;
                }

                if (DepositAmount <= 0)
                {
                    ShowToast("Please enter an amount greater than ৳0.00.", "Error");
                    return;
                }

                var result = _bankService.DepositFunds(account.Id, DepositAmount, DepositSource);
                if (result.Success)
                {
                    ShowToast(result.Message, "Success");
                    IsDepositDialogOpen = false;
                }
                else
                {
                    ShowToast(result.Message, "Error");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Deposit error: {ex.Message}", "Error");
            }
        });

        // Download Receipt Command
        DownloadReceiptPdfCommand = new RelayCommand(() =>
        {
            try
            {
                if (SelectedTransaction == null)
                {
                    ShowToast("No transaction selected for receipt generation.", "Warning");
                    return;
                }

                string receiptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Receipts");
                if (!Directory.Exists(receiptsDir)) Directory.CreateDirectory(receiptsDir);

                string filePath = Path.Combine(receiptsDir, $"APXBankReceipt_{SelectedTransaction.ReferenceNumber}.txt");
                string receiptContent = $"""
                ============================================================
                             APX BANK PLC OFFICIAL TRANSACTION RECEIPT
                ============================================================
                Reference Number : {SelectedTransaction.ReferenceNumber}
                Date & Time      : {SelectedTransaction.Timestamp:yyyy-MM-dd HH:mm:ss} UTC
                Status           : {SelectedTransaction.Status} (Settled via Central Switch)
                ------------------------------------------------------------
                Sender / User    : {CurrentUserName} ({CurrentUserEmail})
                Payee / Title    : {SelectedTransaction.Title}
                Description      : {SelectedTransaction.Description}
                Category         : {SelectedTransaction.Category}
                Amount           : ৳{Math.Abs(SelectedTransaction.Amount):N2} BDT
                Type             : {SelectedTransaction.Type}
                ------------------------------------------------------------
                Cryptographic Signature: SHA256:{Guid.NewGuid():N}
                Database Sheet   : Transactions (ApexBankDatabase.xlsx)
                ============================================================
                Thank you for banking with APX Bank PLC.
                """;

                File.WriteAllText(filePath, receiptContent);
                ShowToast($"Digital receipt exported: {Path.GetFileName(filePath)}", "Success");
            }
            catch (Exception ex)
            {
                ShowToast($"Unable to generate receipt: {ex.Message}", "Error");
            }
        });

        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        SelectTransactionCommand = new RelayCommand(p => { if (p is Transaction tx) SelectedTransaction = tx; });
        CloseSelectedTransactionCommand = new RelayCommand(() => SelectedTransaction = null);
        OpenTransactionsWindowCommand = new RelayCommand(() => CurrentView = "Transactions");

        _bankService.OnDataChanged += RefreshData;
        RefreshData();
    }

    private void RefreshData()
    {
        // Accounts
        if (Accounts.Count != _bankService.Accounts.Count)
        {
            Accounts.Clear();
            foreach (var acc in _bankService.Accounts) Accounts.Add(acc);
        }
        else
        {
            for (int i = 0; i < _bankService.Accounts.Count; i++)
            {
                if (Accounts[i] != _bankService.Accounts[i]) Accounts[i] = _bankService.Accounts[i];
            }
        }

        // Beneficiaries
        Beneficiaries.Clear();
        foreach (var ben in _bankService.Beneficiaries) Beneficiaries.Add(ben);

        // Supported Banks
        if (SupportedBanks.Count != _bankService.SupportedBanks.Count)
        {
            SupportedBanks.Clear();
            foreach (var b in _bankService.SupportedBanks) SupportedBanks.Add(b);
        }

        // Notifications
        Notifications.Clear();
        foreach (var n in _bankService.Notifications) Notifications.Add(n);

        // Spending Categories
        SpendingCategories.Clear();
        foreach (var cat in _bankService.GetSpendingBreakdown()) SpendingCategories.Add(cat);

        // Audit Logs
        AuditLogs.Clear();
        foreach (var log in _bankService.GetAuditLogs()) AuditLogs.Add(log);

        // Cards
        Cards.Clear();
        foreach (var c in _bankService.Cards) Cards.Add(c);

        if (_selectedAccount == null || !Accounts.Contains(_selectedAccount))
        {
            _selectedAccount = Accounts.FirstOrDefault(a => a.IsActive) ?? Accounts.FirstOrDefault()!;
        }

        OnPropertyChanged(nameof(CurrentUser));
        OnPropertyChanged(nameof(CurrentUserName));
        OnPropertyChanged(nameof(CurrentUserEmail));
        OnPropertyChanged(nameof(CurrentUserInitials));
        OnPropertyChanged(nameof(TotalNetWorth));
        OnPropertyChanged(nameof(TotalMonthlyInflow));
        OnPropertyChanged(nameof(TotalMonthlyOutflow));
        OnPropertyChanged(nameof(NetMonthlySavings));
        OnPropertyChanged(nameof(SavingsRate));
        OnPropertyChanged(nameof(Cards));
        OnPropertyChanged(nameof(Card));
        OnPropertyChanged(nameof(SelectedAccount));
        OnPropertyChanged(nameof(DisplayedCardNumber));
        OnPropertyChanged(nameof(DisplayedCvv));
        OnPropertyChanged(nameof(CardDailyLimitValue));
        OnPropertyChanged(nameof(CardDailyLimitText));
        OnPropertyChanged(nameof(UnreadNotificationCount));
        OnPropertyChanged(nameof(HasUnreadNotifications));
        OnPropertyChanged(nameof(BdFee));
        OnPropertyChanged(nameof(BdTotalDebit));
        OnPropertyChanged(nameof(BdFeeNotice));

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredTransactions.Clear();
        RecentTransactions.Clear();
        var query = _bankService.Transactions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var lower = SearchText.Trim().ToLowerInvariant();
            query = query.Where(t => (!string.IsNullOrEmpty(t.Title) && t.Title.ToLowerInvariant().Contains(lower)) ||
                                     (!string.IsNullOrEmpty(t.Description) && t.Description.ToLowerInvariant().Contains(lower)) ||
                                     (!string.IsNullOrEmpty(t.Category) && t.Category.ToLowerInvariant().Contains(lower)) ||
                                     (!string.IsNullOrEmpty(t.ReferenceNumber) && t.ReferenceNumber.ToLowerInvariant().Contains(lower)));
        }

        if (SelectedCategoryFilter != "All")
        {
            query = query.Where(t => t.Category != null && t.Category.Equals(SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.ToList();
        foreach (var tx in list) FilteredTransactions.Add(tx);
        foreach (var tx in list.Take(8)) RecentTransactions.Add(tx);

        OnPropertyChanged(nameof(FilteredTransactions));
        OnPropertyChanged(nameof(RecentTransactions));
    }

    public void ShowToast(string message, string type)
    {
        ToastMessage = message;
        ToastType = type;
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }
}