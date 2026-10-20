using Microsoft.Extensions.DependencyInjection;

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
        Application.Current!.Windows[0].Page = page;
        return Task.CompletedTask;
    }

    private static Page GetCurrentPage() =>
        Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("Aucune fenêtre de l’application n’est disponible.");
}
