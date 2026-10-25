using Microsoft.Extensions.DependencyInjection;
using MiniStock.Views;

namespace MiniStock.Services;

public sealed class NavigationService(IServiceProvider services) : INavigationService
{
    public Task PushAsync<TPage>() where TPage : Page
    {
        var page = services.GetRequiredService<TPage>();
        return GetCurrentPage().Navigation.PushAsync(page);
    }

    public Task PopAsync() => GetCurrentPage().Navigation.PopAsync();

    public Task SetRootPageAsync<TPage>() where TPage : Page
    {
        var page = services.GetRequiredService<TPage>();
        Application.Current!.Windows[0].Page = new NavigationPage(page);
        return Task.CompletedTask;
    }

    public Task NavigateToStockTabAsync(string tab) => tab switch
    {
        "Home" => SetRootPageAsync<MainPage>(),
        "Articles" => SetRootPageAsync<ProductsPage>(),
        "Mouvements" => SetRootPageAsync<MovementsPage>(),
        "Inventaire" => SetRootPageAsync<InventoryPage>(),
        "Réglages" => SetRootPageAsync<SettingsPage>(),
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "Onglet de navigation inconnu.")
    };

    private static Page GetCurrentPage() =>
        Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("Aucune fenêtre de l’application n’est disponible.");
}
