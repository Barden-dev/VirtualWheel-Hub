using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SimRacingHub.Services;
using SimRacingHub.ViewModels;

namespace SimRacingHub
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("en-US")));

            var services = new ServiceCollection();
            
            // Services
            services.AddSingleton<ProfileManager>();
            services.AddSingleton<WindowManager>();
            services.AddSingleton<SimRacingHub.Core.VJoyDiagnosticService>();
            services.AddSingleton<SimRacingHub.Services.Telemetry.GameIntegrationRegistry>();
            services.AddSingleton<UpdateService>();
            
            // ViewModels
            services.AddSingleton<MainViewModel>();
            
            // Views
            services.AddSingleton<MainWindow>();
            services.AddTransient<Views.Pages.DashboardPage>();
            services.AddTransient<Views.Pages.SteeringPage>();
            services.AddTransient<Views.Pages.TrailBrakingPage>();
            services.AddTransient<Views.Pages.GasLogicPage>();
            services.AddTransient<Views.Pages.BrakeLogicPage>();
            services.AddTransient<Views.Pages.SlipAudioPage>();
            services.AddTransient<Views.Pages.VjoyAxesPage>();
            services.AddTransient<Views.Pages.KeyBindingsPage>();

            ServiceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
