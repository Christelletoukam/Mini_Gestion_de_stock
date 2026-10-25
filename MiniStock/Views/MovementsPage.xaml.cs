using MiniStock.ViewModels;

namespace MiniStock.Views;

public partial class MovementsPage : ContentPage
{
    private MovementsViewModel ViewModel => (MovementsViewModel)BindingContext;

    public MovementsPage(MovementsViewModel viewModel)
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
