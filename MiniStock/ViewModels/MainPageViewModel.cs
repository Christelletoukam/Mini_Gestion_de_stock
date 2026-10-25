using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using MiniStock.Services;
using MiniStock.Views;

namespace MiniStock.ViewModels;

public sealed class MainPageViewModel : ViewModelBase
{
    private readonly IStockService _stockService;
    private readonly INavigationService _navigationService;
    private string _boutique = string.Empty;
    private string _nomUtilisateur = string.Empty;
    private string _totalArticles = "0";
    private string _totalEntrees = "0";
    private string _totalSorties = "0";
    private string _nombreAlertes = "0";
    private string _valeurStock = "0 FCFA";
    private string _message = string.Empty;

    public MainPageViewModel(IStockService stockService, INavigationService navigationService)
    {
        _stockService = stockService;
        _navigationService = navigationService;
        ProductsCommand = new Command(async () => await _navigationService.PushAsync<ProductsPage>());
        MovementsCommand = new Command(async () => await _navigationService.PushAsync<MovementsPage>());
        InventoryCommand = new Command(async () => await _navigationService.PushAsync<InventoryPage>());
        SettingsCommand = new Command(async () => await _navigationService.PushAsync<SettingsPage>());
        AlertsCommand = new Command(async () => await _navigationService.PushAsync<AlertsPage>());
        NavigateTabCommand = new Command<string>(async tab => await _navigationService.NavigateToStockTabAsync(tab));
    }

    public string Boutique { get => _boutique; private set => SetProperty(ref _boutique, value); }
    public string NomUtilisateur { get => _nomUtilisateur; private set => SetProperty(ref _nomUtilisateur, value); }
    public string TotalArticles { get => _totalArticles; private set => SetProperty(ref _totalArticles, value); }
    public string TotalEntrees { get => _totalEntrees; private set => SetProperty(ref _totalEntrees, value); }
    public string TotalSorties { get => _totalSorties; private set => SetProperty(ref _totalSorties, value); }
    public string NombreAlertes { get => _nombreAlertes; private set => SetProperty(ref _nombreAlertes, value); }
    public string ValeurStock { get => _valeurStock; private set => SetProperty(ref _valeurStock, value); }
    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(AfficherMessage));
            }
        }
    }
    public bool AfficherMessage => !string.IsNullOrWhiteSpace(Message);

    public ObservableCollection<StockAlerteViewModel> ArticlesAlerte { get; } = [];
    public ICommand ProductsCommand { get; }
    public ICommand MovementsCommand { get; }
    public ICommand InventoryCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand AlertsCommand { get; }
    public ICommand NavigateTabCommand { get; }

    public async Task LoadAsync()
    {
        Message = string.Empty;
        try
        {
            var profile = await _stockService.GetProfileAsync();
            var summary = await _stockService.GetSummaryAsync();
            var alerts = await _stockService.GetAlertsAsync();

            NomUtilisateur = profile.Name;
            Boutique = profile.StoreName;
            TotalArticles = summary.ProductCount.ToString(CultureInfo.InvariantCulture);
            TotalEntrees = summary.EntryCount.ToString(CultureInfo.InvariantCulture);
            TotalSorties = summary.ExitCount.ToString(CultureInfo.InvariantCulture);
            NombreAlertes = summary.AlertCount.ToString(CultureInfo.InvariantCulture);
            ValeurStock = $"{summary.StockValue:N0} FCFA";
            ArticlesAlerte.Clear();
            foreach (var product in alerts)
            {
                var rupture = product.Quantity == 0;
                ArticlesAlerte.Add(new StockAlerteViewModel(
                    product.Name,
                    $"{product.Quantity} unité(s)",
                    rupture ? "Rupture" : "Faible",
                    rupture ? "#D9534F" : "#B7831B"));
            }
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
        catch (SqliteException)
        {
            Message = "Impossible de charger le tableau de bord. Réessayez.";
        }
    }
}

public sealed class StockAlerteViewModel
{
    public StockAlerteViewModel(string nom, string quantite, string etat, string couleur)
    {
        Nom = nom;
        Quantite = quantite;
        Etat = etat;
        Couleur = Color.FromArgb(couleur);
    }

    public string Nom { get; }
    public string Quantite { get; }
    public string Etat { get; }
    public Color Couleur { get; }
}
