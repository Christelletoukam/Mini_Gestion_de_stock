using Microsoft.Extensions.DependencyInjection;

namespace MiniStock
{
    public partial class App : Application
    {
        private readonly IServiceProvider _services;

        public App(IServiceProvider services)
        {
            InitializeComponent();
            _services = services;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new NavigationPage(_services.GetRequiredService<Views.LoginPage>()));
        }
    }
}