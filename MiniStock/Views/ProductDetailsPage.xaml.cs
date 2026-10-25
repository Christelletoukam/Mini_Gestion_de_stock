using MiniStock.ViewModels;

namespace MiniStock.Views;

public partial class ProductDetailsPage : ContentPage
{
    private ProductDetailsViewModel ViewModel => (ProductDetailsViewModel)BindingContext;

    public ProductDetailsPage(ProductDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadAsync();
    }
}
