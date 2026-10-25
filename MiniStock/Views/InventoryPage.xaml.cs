using MiniStock.ViewModels;

namespace MiniStock.Views;

public partial class InventoryPage : ContentPage
{
    private InventoryViewModel ViewModel => (InventoryViewModel)BindingContext;

    public InventoryPage(InventoryViewModel viewModel)
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
