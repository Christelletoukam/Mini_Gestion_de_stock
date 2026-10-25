namespace MiniStock
{
    public partial class MainPage : ContentPage
    {
        private ViewModels.MainPageViewModel ViewModel => (ViewModels.MainPageViewModel)BindingContext;

        public MainPage(ViewModels.MainPageViewModel viewModel)
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
}
