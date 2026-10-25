using MiniStock.Services;
using MiniStock.ViewModels;

namespace MiniStock.Views;

public partial class ProductsPage : ContentPage
{
    private ProductsViewModel ViewModel => (ProductsViewModel)BindingContext;

    public ProductsPage(ProductsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }

    private void OnProductTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: StockProduct product })
        {
            ViewModel.OpenProductCommand.Execute(product);
        }
    }
}
