using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Newtonsoft.Json;
using SimRacingHub.Core;

namespace SimRacingHub.Avalonia.Adapters
{
    /// <summary>
    /// Model for serialized window position, dimensions, and state.
    /// Compatible with existing window.json format.
    /// </summary>
    public sealed class WindowSettings
    {
        public double Width { get; set; } = 1100;
        public double Height { get; set; } = 700;
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public string WindowState { get; set; } = "Normal";
        public bool IsOutputMonitorExpanded { get; set; } = false;
    }

    /// <summary>
    /// Service for restoring and persisting Avalonia window bounds, position, and state
    /// with visible desktop bounds validation and atomic file writes.
    /// </summary>
    public static class AvaloniaWindowStateService
    {
        public const double MinWindowWidth = 900;
        public const double MinWindowHeight = 550;
        public const double MinVisibleIntersectionWidth = 120;
        public const double MinVisibleIntersectionHeight = 40;

        /// <summary>
        /// Restores window dimensions, position, and state from storage.
        /// Verifies that restored position has at least 120x40 px intersection with visible screens.
        /// If not visible, centers the window and logs a warning.
        /// </summary>
        public static WindowState RestoreWindowState(Window window, string settingsPath, out bool isOutputMonitorExpanded, bool applyWindowState = true)
        {
            ArgumentNullException.ThrowIfNull(window);
            isOutputMonitorExpanded = false;
            var restoredState = WindowState.Normal;

            try
            {
                if (string.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath))
                {
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    return WindowState.Normal;
                }

                string json = File.ReadAllText(settingsPath);
                var settings = JsonConvert.DeserializeObject<WindowSettings>(json);
                if (settings == null)
                {
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    return WindowState.Normal;
                }

                window.Width = Math.Max(MinWindowWidth, settings.Width > 0 ? settings.Width : 1100);
                window.Height = Math.Max(MinWindowHeight, settings.Height > 0 ? settings.Height : 700);

                if (!double.IsNaN(settings.Left) && !double.IsNaN(settings.Top) &&
                    !double.IsInfinity(settings.Left) && !double.IsInfinity(settings.Top))
                {
                    if (IsRectVisibleOnDesktop(window, settings.Left, settings.Top, window.Width, window.Height))
                    {
                        window.WindowStartupLocation = WindowStartupLocation.Manual;
                        window.Position = new PixelPoint((int)settings.Left, (int)settings.Top);
                    }
                    else
                    {
                        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        AppLogger.Instance.LogWarning(
                            $"Saved window position ({settings.Left:F0}, {settings.Top:F0}) is outside visible screen area. Window was centered.");
                    }
                }
                else
                {
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                if (string.Equals(settings.WindowState, "Maximized", StringComparison.OrdinalIgnoreCase))
                {
                    restoredState = WindowState.Maximized;
                }
                else
                {
                    restoredState = WindowState.Normal;
                }

                if (applyWindowState) window.WindowState = restoredState;
                isOutputMonitorExpanded = settings.IsOutputMonitorExpanded;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to load window settings from '{settingsPath}': {ex.Message}", ex);
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            return restoredState;
        }

        /// <summary>
        /// Saves current window position, dimensions, and state atomically to storage.
        /// </summary>
        public static void SaveWindowState(Window window, string settingsPath, bool isOutputMonitorExpanded = false, WindowState? windowStateOverride = null)
        {
            ArgumentNullException.ThrowIfNull(window);

            if (string.IsNullOrWhiteSpace(settingsPath))
            {
                return;
            }

            try
            {
                var position = window.Position;
                double width = window.Width > 0 && !double.IsNaN(window.Width)
                    ? window.Width
                    : (window.ClientSize.Width > 0 ? window.ClientSize.Width : 1100);
                double height = window.Height > 0 && !double.IsNaN(window.Height)
                    ? window.Height
                    : (window.ClientSize.Height > 0 ? window.ClientSize.Height : 700);

                var settings = new WindowSettings
                {
                    Width = Math.Max(MinWindowWidth, width),
                    Height = Math.Max(MinWindowHeight, height),
                    Left = position.X,
                    Top = position.Y,
                    WindowState = (windowStateOverride ?? window.WindowState) == WindowState.Maximized ? "Maximized" : "Normal",
                    IsOutputMonitorExpanded = isOutputMonitorExpanded
                };

                string? dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tempPath = settingsPath + ".tmp";
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, settingsPath, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.LogError($"Failed to save window settings to '{settingsPath}': {ex.Message}", ex);
                TryDeleteTempSettings(settingsPath);
            }
        }

        /// <summary>
        /// Validates that a rectangle intersects with at least one screen's working area
        /// with at least 120x40 px visible.
        /// </summary>
        public static bool IsRectVisibleOnDesktop(Window window, double left, double top, double width, double height)
        {
            try
            {
                var screens = window.Screens?.All;
                if (screens == null || screens.Count == 0)
                {
                    return true;
                }

                var winRect = new PixelRect((int)left, (int)top, Math.Max(1, (int)width), Math.Max(1, (int)height));

                foreach (var screen in screens)
                {
                    var area = screen.WorkingArea;
                    var intersection = winRect.Intersect(area);

                    if (intersection.Width >= MinVisibleIntersectionWidth &&
                        intersection.Height >= MinVisibleIntersectionHeight)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        private static void TryDeleteTempSettings(string settingsPath)
        {
            try
            {
                string tempPath = settingsPath + ".tmp";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }
}
