using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;
using MiniStock.Services;

namespace MiniStock.ViewModels;

public sealed class ProductDetailsViewModel : ViewModelBase
{
    private readonly IStockService _stockService;
    private readonly INavigationService _navigationService;
    private StockProduct? _product;
    private string _unitPrice = string.Empty;
    private string _minimumQuantity = string.Empty;
    private string _message = string.Empty;
    private string _selectedTab = "Informations";
    private bool _isEditing;
    private bool _isBusy;

    public ProductDetailsViewModel(IStockService stockService, INavigationService navigationService)
    {
        _stockService = stockService;
        _navigationService = navigationService;
        BackCommand = new Command(async () => await _navigationService.PopAsync());
        EditCommand = new Command(() =>
        {
            IsEditing = !IsEditing;
            if (IsEditing && Product is not null)
            {
                UnitPrice = Product.UnitPrice.ToString(CultureInfo.InvariantCulture);
                MinimumQuantity = Product.MinimumQuantity.ToString(CultureInfo.InvariantCulture);
            }
        });
        SaveCommand = new Command(async () => await SaveAsync(), () => !IsBusy);
        SelectTabCommand = new Command<string>(tab => SelectedTab = tab);
        NavigateTabCommand = new Command<string>(async tab => await _navigationService.NavigateToStockTabAsync(tab));
    }

    public StockProduct? Product { get => _product; private set => SetProperty(ref _product, value); }
    public string UnitPrice { get => _unitPrice; set => SetProperty(ref _unitPrice, value); }
    public string MinimumQuantity { get => _minimumQuantity; set => SetProperty(ref _minimumQuantity, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string SelectedTab
    {
        get => _selectedTab;
        private set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsShowingHistory));
                OnPropertyChanged(nameof(IsShowingInformation));
            }
        }
    }
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(IsNotEditing));
            }
        }
    }
    public bool IsNotEditing => !IsEditing;
    public bool IsShowingHistory => SelectedTab == "Historique";
    public bool IsShowingInformation => !IsShowingHistory;
    public bool HasMovements => Movements.Count > 0;
    public bool HasNoMovements => Movements.Count == 0;
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
    public ObservableCollection<StockMovement> Movements { get; } = [];
    public ICommand BackCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand NavigateTabCommand { get; }

    public async Task LoadAsync()
    {
        Message = string.Empty;
        var productIdText = Preferences.Get("selected_product_id", string.Empty);
        if (!Guid.TryParse(productIdText, out var productId))
        {
            Message = "Aucun article n’a été sélectionné.";
            return;
        }

        try
        {
            Product = (await _stockService.GetProductsAsync()).FirstOrDefault(item => item.Id == productId);
            if (Product is null)
            {
                Message = "L’article sélectionné n’existe plus.";
                return;
            }

            UnitPrice = Product.UnitPrice.ToString(CultureInfo.InvariantCulture);
            MinimumQuantity = Product.MinimumQuantity.ToString(CultureInfo.InvariantCulture);
            Movements.Clear();
            foreach (var movement in await _stockService.GetProductMovementsAsync(Product.Id))
            {
                Movements.Add(movement);
            }
            OnPropertyChanged(nameof(HasMovements));
            OnPropertyChanged(nameof(HasNoMovements));
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible de charger la fiche article. Réessayez.";
        }
    }

    private async Task SaveAsync()
    {
        if (IsBusy || Product is null)
        {
            return;
        }

        Message = string.Empty;
        if (!int.TryParse(UnitPrice, NumberStyles.None, CultureInfo.InvariantCulture, out var price) || price < 0 ||
            !int.TryParse(MinimumQuantity, NumberStyles.None, CultureInfo.InvariantCulture, out var minimum) || minimum < 0)
        {
            Message = "Saisissez un prix et un seuil d’alerte valides.";
            return;
        }

        IsBusy = true;
        try
        {
            await _stockService.UpdateProductPricingAsync(Product.Id, price, minimum);
            Product = Product with { UnitPrice = price, MinimumQuantity = minimum };
            IsEditing = false;
            Message = "Article mis à jour.";
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
            Message = "Impossible de modifier l’article. Réessayez.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
