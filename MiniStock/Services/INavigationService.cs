namespace MiniStock.Services;

public interface INavigationService
{
    Task PushAsync<TPage>() where TPage : Page;

    Task PopAsync();

    Task SetRootPageAsync<TPage>() where TPage : Page;
}
