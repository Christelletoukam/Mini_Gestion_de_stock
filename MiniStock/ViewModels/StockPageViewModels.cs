using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;
using MiniStock.Models;
using MiniStock.Services;
using MiniStock.Views;

namespace MiniStock.ViewModels;

public sealed class ProductsViewModel : ViewModelBase
{
    private readonly IStockService _stockService;
    private readonly INavigationService _navigationService;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _unitPrice = string.Empty;
    private string _quantity = string.Empty;
    private string _minimumQuantity = string.Empty;
    private string _message = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedFilter = "Tous";
    private bool _showAddForm;
    private bool _isBusy;

    public ProductsViewModel(IStockService stockService, INavigationService navigationService)
    {
        _stockService = stockService;
        _navigationService = navigationService;
        AddCommand = new Command(async () => await AddAsync(), () => !IsBusy);
        NavigateTabCommand = new Command<string>(async tab => await _navigationService.NavigateToStockTabAsync(tab));
        ToggleAddFormCommand = new Command(() => ShowAddForm = !ShowAddForm);
        OpenProductCommand = new Command<StockProduct>(async product =>
        {
            if (product is null)
            {
                return;
            }

            Preferences.Set("selected_product_id", product.Id.ToString("D"));
            await _navigationService.PushAsync<ProductDetailsPage>();
        });
        SelectFilterCommand = new Command<string>(filter =>
        {
            SelectedFilter = filter;
            RefreshFilteredProducts();
        });
    }

    public ObservableCollection<StockProduct> Products { get; } = [];
    public ObservableCollection<StockProduct> FilteredProducts { get; } = [];

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Category { get => _category; set => SetProperty(ref _category, value); }
    public string UnitPrice { get => _unitPrice; set => SetProperty(ref _unitPrice, value); }
    public string Quantity { get => _quantity; set => SetProperty(ref _quantity, value); }
    public string MinimumQuantity { get => _minimumQuantity; set => SetProperty(ref _minimumQuantity, value); }
    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshFilteredProducts();
            }
        }
    }
    public string SelectedFilter { get => _selectedFilter; private set => SetProperty(ref _selectedFilter, value); }
    public bool ShowAddForm { get => _showAddForm; private set => SetProperty(ref _showAddForm, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value) && AddCommand is Command command)
            {
                command.ChangeCanExecute();
            }
        }
    }
    public ICommand AddCommand { get; }
    public ICommand NavigateTabCommand { get; }
    public ICommand ToggleAddFormCommand { get; }
    public ICommand SelectFilterCommand { get; }
    public ICommand OpenProductCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            Products.Clear();
            foreach (var product in await _stockService.GetProductsAsync())
            {
                Products.Add(product);
            }
            RefreshFilteredProducts();
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible de charger les articles. Réessayez.";
        }
    }

    private async Task AddAsync()
    {
        if (IsBusy)
        {
            return;
        }

        Message = string.Empty;
        if (string.IsNullOrWhiteSpace(Name) ||
            !int.TryParse(UnitPrice, NumberStyles.None, CultureInfo.InvariantCulture, out var price) || price < 0 ||
            !int.TryParse(Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity < 0 ||
            !int.TryParse(MinimumQuantity, NumberStyles.None, CultureInfo.InvariantCulture, out var minimum) || minimum < 0)
        {
            Message = "Saisissez un nom et des quantités ou prix valides (entiers positifs).";
            return;
        }

        IsBusy = true;
        try
        {
            await _stockService.AddProductAsync(Name, Category, price, quantity, minimum);
            Name = string.Empty;
            Category = string.Empty;
            UnitPrice = string.Empty;
            Quantity = string.Empty;
            MinimumQuantity = string.Empty;
            ShowAddForm = false;
            await LoadAsync();
        }
        catch (ArgumentException exception)
        {
            Message = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible d’enregistrer l’article. Vérifiez les informations et réessayez.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshFilteredProducts()
    {
        var search = SearchText.Trim();
        var filtered = Products.Where(product =>
            (string.IsNullOrEmpty(search) ||
             product.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
             (product.Category?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)) &&
            (SelectedFilter switch
            {
                "En stock" => product.Quantity > product.MinimumQuantity,
                "Faible" => product.Quantity > 0 && product.Quantity <= product.MinimumQuantity,
                "Rupture" => product.Quantity == 0,
                _ => true
            }));

        FilteredProducts.Clear();
        foreach (var product in filtered)
        {
            FilteredProducts.Add(product);
        }

        OnPropertyChanged(nameof(HasNoProducts));
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(LowStockCount));
        OnPropertyChanged(nameof(OutOfStockCount));
    }

    public int AvailableCount => Products.Count(product => product.Quantity > product.MinimumQuantity);
    public int LowStockCount => Products.Count(product => product.Quantity > 0 && product.Quantity <= product.MinimumQuantity);
    public int OutOfStockCount => Products.Count(product => product.Quantity == 0);
    public bool HasNoProducts => FilteredProducts.Count == 0;
}

public sealed class MovementsViewModel : ViewModelBase
{
    private readonly IStockService _stockService;
    private readonly INavigationService _navigationService;
    private StockProduct? _selectedProduct;
    private string _selectedType = "Entrée";
    private string _quantity = string.Empty;
    private string _reason = string.Empty;
    private string _message = string.Empty;
    private bool _isBusy;

    public MovementsViewModel(IStockService stockService, INavigationService navigationService)
    {
        _stockService = stockService;
        _navigationService = navigationService;
        SaveCommand = new Command(async () => await SaveAsync(), () => !IsBusy);
        NavigateTabCommand = new Command<string>(async tab => await _navigationService.NavigateToStockTabAsync(tab));
        SelectMovementTypeCommand = new Command<string>(type =>
        {
            SelectedType = type;
            OnPropertyChanged(nameof(SubmitButtonText));
            OnPropertyChanged(nameof(StockAfter));
        });
    }

    public ObservableCollection<StockProduct> Products { get; } = [];
    public ObservableCollection<string> MovementTypes { get; } = ["Entrée", "Sortie"];
    public ObservableCollection<StockMovement> Movements { get; } = [];
    public bool HasNoMovements => Movements.Count == 0;
    public StockProduct? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(CurrentStock));
                OnPropertyChanged(nameof(StockAfter));
            }
        }
    }
    public string SelectedType { get => _selectedType; private set => SetProperty(ref _selectedType, value); }
    public string Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(StockAfter));
            }
        }
    }
    public string CurrentStock => SelectedProduct?.Quantity.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string MovementDate => DateTime.Now.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));
    public string StockAfter
    {
        get
        {
            var quantity = int.TryParse(Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            var current = SelectedProduct?.Quantity ?? 0;
            var result = SelectedType == "Entrée" ? current + quantity : Math.Max(0, current - quantity);
            return $"{result} unité(s)";
        }
    }
    public string SubmitButtonText => SelectedType == "Entrée" ? "✓  Enregistrer l’entrée" : "✓  Enregistrer la sortie";
    public StockMovement? LatestMovement => Movements.FirstOrDefault();
    public bool HasMovements => Movements.Count > 0;
    public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value) && SaveCommand is Command command)
            {
                command.ChangeCanExecute();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand NavigateTabCommand { get; }
    public ICommand SelectMovementTypeCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            Products.Clear();
            foreach (var product in await _stockService.GetProductsAsync())
            {
                Products.Add(product);
            }

            SelectedProduct = Products.FirstOrDefault();
            Movements.Clear();
            foreach (var movement in await _stockService.GetMovementsAsync())
            {
                Movements.Add(movement);
            }
            OnPropertyChanged(nameof(HasNoMovements));
            OnPropertyChanged(nameof(LatestMovement));
            OnPropertyChanged(nameof(HasMovements));
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible de charger les mouvements. Réessayez.";
        }
    }

    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        Message = string.Empty;
        if (SelectedProduct is null ||
            !int.TryParse(Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) ||
            quantity <= 0)
        {
            Message = "Choisissez un article et saisissez une quantité positive.";
            return;
        }

        IsBusy = true;
        try
        {
            var type = SelectedType == "Entrée" ? TypeMouvementStock.Entree : TypeMouvementStock.Sortie;
            await _stockService.RegisterMovementAsync(SelectedProduct.Id, type, quantity, Reason);
            Quantity = string.Empty;
            Reason = string.Empty;
            await LoadAsync();
            Message = "Mouvement enregistré.";
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (ArgumentException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible d’enregistrer le mouvement. Réessayez.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class InventoryViewModel : ViewModelBase
{
    private readonly IStockService _stockService;
    private readonly INavigationService _navigationService;
    private string _message = string.Empty;
    private string _searchText = string.Empty;
    private bool _isBusy;

    public InventoryViewModel(IStockService stockService, INavigationService navigationService)
    {
        _stockService = stockService;
        _navigationService = navigationService;
        SaveCommand = new Command(async () => await SaveAsync(), () => !IsBusy);
        NavigateTabCommand = new Command<string>(async tab => await _navigationService.NavigateToStockTabAsync(tab));
        BackCommand = new Command(async () => await _navigationService.NavigateToStockTabAsync("Home"));
    }

    public ObservableCollection<InventoryLineViewModel> Products { get; } = [];
    public ObservableCollection<InventoryLineViewModel> FilteredProducts { get; } = [];
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshFilteredProducts();
            }
        }
    }
    public int CompletedCount => Products.Count(product => !string.IsNullOrWhiteSpace(product.PhysicalQuantity));
    public int CompletionPercent => Products.Count == 0 ? 0 : CompletedCount * 100 / Products.Count;
    public double CompletionProgress => Products.Count == 0 ? 0 : (double)CompletedCount / Products.Count;
    public string CompletionText => $"{CompletedCount} article(s) compté(s) sur {Products.Count}";
    public string RemainingText => Products.Count == CompletedCount
        ? "Tous les articles sont comptés"
        : $"{Products.Count - CompletedCount} article(s) à compter";
    public string InventoryDate => DateTime.Now.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value) && SaveCommand is Command command)
            {
                command.ChangeCanExecute();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand NavigateTabCommand { get; }
    public ICommand BackCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            Products.Clear();
            foreach (var product in await _stockService.GetProductsAsync())
            {
                var line = new InventoryLineViewModel(product);
                line.PropertyChanged += (_, _) => OnInventoryLineChanged();
                Products.Add(line);
            }
            RefreshFilteredProducts();
            OnInventoryLineChanged();
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible de charger les articles pour l’inventaire. Réessayez.";
        }
    }

    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        Message = string.Empty;
        var physicalQuantities = new Dictionary<Guid, int>();
        foreach (var product in Products.Where(product => !string.IsNullOrWhiteSpace(product.PhysicalQuantity)))
        {
            if (!int.TryParse(product.PhysicalQuantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) ||
                quantity < 0)
            {
                Message = $"Saisissez une quantité physique valide pour « {product.Name} ».";
                return;
            }

            physicalQuantities.Add(product.Id, quantity);
        }

        if (physicalQuantities.Count == 0)
        {
            Message = "Saisissez au moins une quantité physique.";
            return;
        }

        IsBusy = true;
        try
        {
            await _stockService.SaveInventoryAsync(physicalQuantities);
            await LoadAsync();
            Message = "Inventaire enregistré et stock mis à jour.";
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (ArgumentException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible d’enregistrer l’inventaire. Réessayez.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnInventoryLineChanged()
    {
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(CompletionPercent));
        OnPropertyChanged(nameof(CompletionProgress));
        OnPropertyChanged(nameof(CompletionText));
        OnPropertyChanged(nameof(RemainingText));
    }

    private void RefreshFilteredProducts()
    {
        var search = SearchText.Trim();
        FilteredProducts.Clear();
        foreach (var product in Products.Where(product =>
                     string.IsNullOrEmpty(search) ||
                     product.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
        {
            FilteredProducts.Add(product);
        }
    }
}

public sealed class InventoryLineViewModel(StockProduct product) : ViewModelBase
{
    private string _physicalQuantity = string.Empty;

    public Guid Id => product.Id;
    public string Name => product.Name;
    public int ExpectedQuantity => product.Quantity;
    public string PhysicalQuantity
    {
        get => _physicalQuantity;
        set
        {
            if (SetProperty(ref _physicalQuantity, value))
            {
                OnPropertyChanged(nameof(DifferenceLabel));
                OnPropertyChanged(nameof(DifferenceColor));
                OnPropertyChanged(nameof(RowBackground));
            }
        }
    }
    public string DifferenceLabel => string.IsNullOrWhiteSpace(PhysicalQuantity) ||
                                     !int.TryParse(PhysicalQuantity, NumberStyles.None, CultureInfo.InvariantCulture, out var actual)
        ? "—"
        : (actual - ExpectedQuantity).ToString("+0;-0;0", CultureInfo.InvariantCulture);
    public Color DifferenceColor => DifferenceLabel switch
    {
        "0" => Color.FromArgb("#35A65A"),
        "—" => Color.FromArgb("#7C8D9B"),
        _ => Color.FromArgb("#D9534F")
    };
    public Color RowBackground => DifferenceLabel != "—" && DifferenceLabel != "0"
        ? Color.FromArgb("#E7F3FB")
        : Colors.White;
}

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IStockService _stockService;
    private readonly INavigationService _navigationService;
    private StockProfile? _profile;
    private string _message = string.Empty;

    public SettingsViewModel(IStockService stockService, INavigationService navigationService)
    {
        _stockService = stockService;
        _navigationService = navigationService;
        NavigateTabCommand = new Command<string>(async tab => await _navigationService.NavigateToStockTabAsync(tab));
        LogoutCommand = new Command(async () =>
        {
            Preferences.Remove("utilisateur_id");
            await _navigationService.SetRootPageAsync<LoginPage>();
        });
    }

    public string Name => _profile?.Name ?? string.Empty;
    public string Email => _profile?.Email ?? string.Empty;
    public string StoreName => _profile?.StoreName ?? string.Empty;
    public string Phone => _profile?.Phone ?? string.Empty;
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public ICommand LogoutCommand { get; }
    public ICommand NavigateTabCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            _profile = await _stockService.GetProfileAsync();
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Email));
            OnPropertyChanged(nameof(StoreName));
            OnPropertyChanged(nameof(Phone));
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible de charger les informations du compte. Réessayez.";
        }
    }
}
