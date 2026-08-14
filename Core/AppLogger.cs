using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimRacingHub.Core
{
    public class AppLogger : ObservableObject
    {
        public static AppLogger Instance { get; } = new AppLogger();

        private string _lastStatusMessage;
        public string LastStatusMessage
        {
            get => _lastStatusMessage;
            set => SetProperty(ref _lastStatusMessage, value);
        }

        public void LogError(string message, Exception ex = null)
        {
            string logText = ex != null ? $"{message}: {ex.Message}" : message;
            Debug.WriteLine($"[ERROR] {logText}");
            
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
            {
                LastStatusMessage = message;
            });
        }
        
        public void LogInfo(string message)
        {
            Debug.WriteLine($"[INFO] {message}");
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
            {
                LastStatusMessage = message;
            });
        }

        public void LogWarning(string message)
        {
            Debug.WriteLine($"[WARN] {message}");
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
            {
                LastStatusMessage = message;
            });
        }
        
        public void ClearStatus()
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => 
            {
                LastStatusMessage = null;
            });
        }
    }
}
