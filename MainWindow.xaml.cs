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
        public double Width { get; set; } = 1100;
        public double Height { get; set; } = 700;
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public bool IsOutputMonitorExpanded { get; set; } = false;
    }

    public partial class MainWindow : FluentWindow
    {
        private static readonly string SettingsFile = StoragePaths.WindowSettingsPath;

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
                        Width = Math.Max(1100, settings.Width);
                        Height = Math.Max(600, settings.Height);
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

        private bool IsValidProfileDrag(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string ext = Path.GetExtension(files[0]).ToLowerInvariant();
                    return ext == ".vwh" || ext == ".json";
                }
            }
            return false;
        }

        private void MainWindow_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (IsValidProfileDrag(e))
            {
                e.Effects = DragDropEffects.Copy;
                DropOverlay.Visibility = Visibility.Visible;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindow_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (IsValidProfileDrag(e))
            {
                e.Effects = DragDropEffects.Copy;
                if (DropOverlay.Visibility != Visibility.Visible)
                {
                    DropOverlay.Visibility = Visibility.Visible;
                }
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                if (DropOverlay.Visibility != Visibility.Collapsed)
                {
                    DropOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void MainWindow_PreviewDragLeave(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_PreviewDrop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;

            if (IsValidProfileDrag(e))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    if (DataContext is MainViewModel vm)
                    {
                        var pkg = vm.ProfileSharingService.ImportFromFile(filePath);
                        if (pkg != null)
                        {
                            vm.ProcessImportPackage(pkg);
                        }
                        else
                        {
                            AppLogger.Instance.LogWarning($"Failed to parse dropped preset file: {Path.GetFileName(filePath)}");
                            var box = new Wpf.Ui.Controls.MessageBox
                            {
                                Title = "Invalid Preset File",
                                Content = $"The file '{Path.GetFileName(filePath)}' could not be parsed as a valid vWheel Hub profile preset.",
                                CloseButtonText = "OK"
                            };
                            _ = box.ShowDialogAsync();
                        }
                    }
                }
                e.Handled = true;
            }
        }
    }
}