using MiniStock.ViewModels;

namespace MiniStock.Views;

public partial class SettingsPage : ContentPage
{
    private SettingsViewModel ViewModel => (SettingsViewModel)BindingContext;

    public SettingsPage(SettingsViewModel viewModel)
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
