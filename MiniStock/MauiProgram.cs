using Microsoft.Extensions.Logging;
#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
#endif
using MiniStock.Data;
using MiniStock.Services;
using MiniStock.ViewModels;
using MiniStock.Views;

namespace MiniStock
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if MAUI_DEVFLOW
            builder.AddMauiDevFlowAgent();
#endif

            builder.Services.AddSingleton<StockDatabase>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IStockService, StockService>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<MainPageViewModel>();
            builder.Services.AddTransient<ProductsViewModel>();
            builder.Services.AddTransient<MovementsViewModel>();
            builder.Services.AddTransient<InventoryViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<AlertsViewModel>();
            builder.Services.AddTransient<ProductDetailsViewModel>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ProductsPage>();
            builder.Services.AddTransient<MovementsPage>();
            builder.Services.AddTransient<InventoryPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<AlertsPage>();
            builder.Services.AddTransient<ProductDetailsPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
