using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;
using MiniStock.Services;
using MiniStock.Views;

namespace MiniStock.ViewModels;

public sealed class AlertsViewModel : ViewModelBase
{
    private readonly IStockService _stockService;
    private readonly INavigationService _navigationService;
    private string _selectedFilter = "Toutes";
    private string _message = string.Empty;

    public AlertsViewModel(IStockService stockService, INavigationService navigationService)
    {
        _stockService = stockService;
        _navigationService = navigationService;
        SelectFilterCommand = new Command<string>(filter =>
        {
            SelectedFilter = filter;
            RefreshAlerts();
        });
        OpenProductCommand = new Command<StockProduct>(async product => await OpenProductAsync(product));
        BackCommand = new Command(async () => await _navigationService.PopAsync());
        NavigateTabCommand = new Command<string>(async tab => await _navigationService.NavigateToStockTabAsync(tab));
    }

    public ObservableCollection<StockProduct> Alerts { get; } = [];
    public ObservableCollection<StockProduct> FilteredAlerts { get; } = [];
    public string SelectedFilter { get => _selectedFilter; private set => SetProperty(ref _selectedFilter, value); }
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
    public bool HasNoAlerts => FilteredAlerts.Count == 0;
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public int RuptureCount => Alerts.Count(product => product.Quantity == 0);
    public int LowStockCount => Alerts.Count(product => product.Quantity > 0);
    public ICommand SelectFilterCommand { get; }
    public ICommand OpenProductCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand NavigateTabCommand { get; }

    public async Task LoadAsync()
    {
        Message = string.Empty;
        try
        {
            Alerts.Clear();
            foreach (var product in await _stockService.GetAlertsAsync())
            {
                Alerts.Add(product);
            }

            RefreshAlerts();
            OnPropertyChanged(nameof(RuptureCount));
            OnPropertyChanged(nameof(LowStockCount));
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible de charger les alertes. Réessayez.";
        }
    }

    private void RefreshAlerts()
    {
        FilteredAlerts.Clear();
        foreach (var product in Alerts.Where(product => SelectedFilter switch
                 {
                     "Rupture" => product.Quantity == 0,
                     "Stock faible" => product.Quantity > 0,
                     _ => true
                 }))
        {
            FilteredAlerts.Add(product);
        }

        OnPropertyChanged(nameof(HasNoAlerts));
    }

    private async Task OpenProductAsync(StockProduct? product)
    {
        if (product is null)
        {
            return;
        }

        Preferences.Set("selected_product_id", product.Id.ToString("D"));
        await _navigationService.PushAsync<ProductDetailsPage>();
    }
}
