using MiniStock.Services;
using MiniStock.ViewModels;

namespace MiniStock.Views;

public partial class AlertsPage : ContentPage
{
    private AlertsViewModel ViewModel => (AlertsViewModel)BindingContext;

    public AlertsPage(AlertsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    private void OnProductSelected(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: StockProduct product })
        {
            ViewModel.OpenProductCommand.Execute(product);
        }
    }
}
