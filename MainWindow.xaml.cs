using System;
using System.Windows;
using System.Windows.Interop;
using SimRacingHub.ViewModels;
using Wpf.Ui.Controls;

using System.IO;
using System.ComponentModel;
using Newtonsoft.Json;
using SimRacingHub.Core;

namespace SimRacingHub
{
    public class WindowSettings
    {
        public double Width { get; set; } = 1000;
        public double Height { get; set; } = 700;
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public bool IsOutputMonitorExpanded { get; set; } = false;
    }

    public partial class MainWindow : FluentWindow
    {
        private const string SettingsFile = "window.json";

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            LoadWindowSettings(viewModel);
            
            this.Closing += MainWindow_Closing;
            this.Activated += (s, e) =>
            {
                if (DataContext is MainViewModel vm && vm.LockMouse)
                {
                    vm.ReapplyMouseLock();
                }
            };
            
            RootNavigation.SetServiceProvider(App.ServiceProvider);
            Loaded += (s, e) => 
            {
                RootNavigation.Navigate(typeof(Views.Pages.DashboardPage));
                if (viewModel.AutoStartHub)
                {
                    viewModel.IsActive = true;
                }
            };
        }

        private void LoadWindowSettings(MainViewModel viewModel)
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var settings = JsonConvert.DeserializeObject<WindowSettings>(File.ReadAllText(SettingsFile));
                    if (settings != null)
                    {
                        Width = settings.Width;
                        Height = settings.Height;
                        if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top))
                        {
                            WindowStartupLocation = WindowStartupLocation.Manual;
                            Left = settings.Left;
                            Top = settings.Top;
                        }
                        WindowState = settings.WindowState;
                        if (viewModel != null)
                        {
                            viewModel.IsOutputMonitorExpanded = settings.IsOutputMonitorExpanded;
                        }
                    }
                }
            }
            catch (Exception ex) 
            { 
                AppLogger.Instance.LogError("Failed to load window settings", ex);
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                var vm = DataContext as MainViewModel;
                var settings = new WindowSettings
                {
                    Width = this.RestoreBounds.Width > 0 ? this.RestoreBounds.Width : this.Width,
                    Height = this.RestoreBounds.Height > 0 ? this.RestoreBounds.Height : this.Height,
                    Left = this.RestoreBounds.Left != 0 ? this.RestoreBounds.Left : this.Left,
                    Top = this.RestoreBounds.Top != 0 ? this.RestoreBounds.Top : this.Top,
                    WindowState = this.WindowState,
                    IsOutputMonitorExpanded = vm?.IsOutputMonitorExpanded ?? false
                };
                File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(settings));
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError("Failed to save window settings", ex);
            }
            finally
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.Shutdown();
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Pass the HWND to the ViewModel for ClipCursor
            if (DataContext is MainViewModel vm)
            {
                var helper = new WindowInteropHelper(this);
                vm.WindowHandle = helper.Handle;
            }
        }
    }
}