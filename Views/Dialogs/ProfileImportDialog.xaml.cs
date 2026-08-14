using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using SimRacingHub.Models;
using SimRacingHub.Services;
using Wpf.Ui.Controls;

namespace SimRacingHub.Views.Dialogs
{
    public enum ProfileImportAction
    {
        None,
        SaveToTargetSlot,
        ApplyToCurrentContext
    }

    public partial class ProfileImportDialog : FluentWindow
    {
        private readonly ProfileContext _currentContext;
        private readonly ProfileSharingService _sharingService;
        private readonly bool _isInitialized = false;
        
        public ProfileSharePackage Package { get; private set; }
        public ProfileImportAction ResultAction { get; private set; } = ProfileImportAction.None;

        public ProfileImportDialog(ProfileContext currentContext, ProfileSharingService sharingService, ProfileSharePackage initialPackage = null)
        {
            _currentContext = currentContext ?? new ProfileContext("Universal");
            _sharingService = sharingService ?? new ProfileSharingService();

            InitializeComponent();
            _isInitialized = true;

            if (initialPackage != null)
            {
                LoadPackage(initialPackage);
            }
            else
            {
                ResetPreview();
            }
        }

        private void LoadPackage(ProfileSharePackage package)
        {
            if (!_isInitialized) return;

            if (package == null || package.Settings == null)
            {
                ResetPreview();
                return;
            }

            Package = package;

            // Header information
            if (PresetNameText != null)
            {
                PresetNameText.Text = !string.IsNullOrWhiteSpace(package.Name) ? package.Name : "Imported Preset";
            }

            string dateStr = package.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd");
            if (SubtitleText != null)
            {
                if (!string.IsNullOrWhiteSpace(package.Author))
                {
                    SubtitleText.Text = $"Created by {package.Author.Trim()} • {dateStr}";
                }
                else
                {
                    SubtitleText.Text = $"Created on {dateStr}";
                }
            }

            if (VersionBadgeText != null)
            {
                VersionBadgeText.Text = !string.IsNullOrWhiteSpace(package.AppVersion) ? $"v{package.AppVersion}" : "v1.0.0";
            }

            // Description
            if (DescriptionBorder != null && DescriptionText != null)
            {
                if (!string.IsNullOrWhiteSpace(package.Description))
                {
                    DescriptionBorder.Visibility = Visibility.Visible;
                    DescriptionText.Text = package.Description;
                }
                else
                {
                    DescriptionBorder.Visibility = Visibility.Collapsed;
                }
            }

            // Version comparison
            if (VersionWarningBorder != null)
            {
                if (!string.IsNullOrWhiteSpace(package.AppVersion) && Version.TryParse(package.AppVersion, out var presetVer) && Version.TryParse(UpdateService.CurrentVersion, out var curVer))
                {
                    VersionWarningBorder.Visibility = presetVer > curVer ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    VersionWarningBorder.Visibility = Visibility.Collapsed;
                }
            }

            // Target Context
            string targetPath = package.TargetContext?.DisplayPath ?? "Universal";
            if (TargetContextText != null)
            {
                TargetContextText.Text = targetPath;
            }

            bool isSpecificTarget = package.TargetContext != null && 
                                   !string.IsNullOrEmpty(package.TargetContext.Game) && 
                                   !package.TargetContext.Game.Equals("Universal", StringComparison.OrdinalIgnoreCase);

            if (SaveToTargetSlotButton != null)
            {
                if (isSpecificTarget)
                {
                    string targetCarName = !string.IsNullOrEmpty(package.TargetContext.Car) 
                        ? package.TargetContext.Car 
                        : (!string.IsNullOrEmpty(package.TargetContext.CarClass) ? package.TargetContext.CarClass : package.TargetContext.Game);

                    SaveToTargetSlotButton.Visibility = Visibility.Visible;
                    SaveToTargetSlotButton.IsEnabled = true;
                    SaveToTargetSlotButton.Content = $"Save to Target Slot ({targetCarName})";
                    SaveToTargetSlotButton.ToolTip = $"Saves directly into Profiles/{package.TargetContext.DisplayPath} for auto-detection";
                }
                else
                {
                    SaveToTargetSlotButton.Visibility = Visibility.Collapsed;
                    SaveToTargetSlotButton.IsEnabled = false;
                }
            }

            if (ApplyToCurrentContextButton != null)
            {
                string curName = _currentContext?.Car ?? _currentContext?.CarClass ?? _currentContext?.Game ?? "Current";
                ApplyToCurrentContextButton.Content = $"Apply to Current Configuration ({curName})";
                ApplyToCurrentContextButton.IsEnabled = true;
                if (!isSpecificTarget)
                {
                    ApplyToCurrentContextButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                }
            }

            // Categorized Parameters Preview
            var p = package.Settings;

            // 1. Steering
            if (ParamSteerSensText != null)
                ParamSteerSensText.Text = $"Sensitivity: {(p.MouseSensitivity ?? 1.0):0.00} ({p.PollingRate ?? 1000} Hz)";
            
            if (ParamSteerGammaText != null)
                ParamSteerGammaText.Text = $"Steer Gamma: {(p.SteeringGamma ?? 1.0):0.00}" + ((p.SteeringGamma ?? 1.0) == 1.0 ? " (Linear)" : "");
            
            if (ParamSteerSpeedSensText != null)
            {
                ParamSteerSpeedSensText.Text = p.EnableSpeedSensSteering == true 
                    ? $"Speed-Sens: {((p.SpeedSensMinMultiplier ?? 0.5) * 100):0}% at {p.SpeedSensMaxSpeed ?? 200:0} km/h" 
                    : "Speed-Sens: Disabled";
            }
            
            if (ParamSteerLockText != null)
                ParamSteerLockText.Text = p.UseSteeringLockClamp == true ? $"Steering Lock: {p.CarSteeringLock ?? 900}°" : "Steering Lock: Auto";

            // 2. Pedals
            if (ParamThrottleDynamicsText != null)
                ParamThrottleDynamicsText.Text = $"Throttle: Fast {p.GasAttackFast ?? 666:0} / Slow {p.GasAttackSlow ?? 222:0}";
            
            if (ParamThrottleGammaText != null)
                ParamThrottleGammaText.Text = $"Throttle Gamma: {(p.GasGamma ?? 1.0):0.00}";
            
            if (ParamBrakeDynamicsText != null)
                ParamBrakeDynamicsText.Text = $"Brake: Fast {p.BrakeAttackFast ?? 1000:0} / Slow {p.BrakeAttackSlow ?? 250:0}";
            
            if (ParamBrakeGammaText != null)
                ParamBrakeGammaText.Text = $"Brake Gamma: {(p.BrakeGamma ?? 1.0):0.00}";

            // 3. Telemetry & Assists
            if (ParamAbsText != null)
                ParamAbsText.Text = p.UseAbsHelper == true ? $"ABS Helper: Active ({(p.AbsSlipThreshold ?? 0.05) * 100:0}%)" : "ABS Helper: Disabled";
            
            if (ParamTcText != null)
                ParamTcText.Text = p.UseTcHelper == true ? $"TC Helper: Active ({(p.TcSlipThreshold ?? 0.05) * 100:0}%)" : "TC Helper: Disabled";
            
            if (ParamTrailBrakingText != null)
                ParamTrailBrakingText.Text = p.EnableTrailBraking == true ? $"Trail Braking: Active ({((p.TbMinLimit ?? 0.2) * 100):0}%)" : "Trail Braking: Disabled";
            
            if (ParamAudioText != null)
            {
                ParamAudioText.Text = (p.EnableSlipAudio == true || p.EnableTcAudio == true) 
                    ? $"Audio: {(p.EnableSlipAudio == true ? "Slip ON" : "")} {(p.EnableTcAudio == true ? "TC ON" : "")}".Trim() 
                    : "Audio Cues: Disabled";
            }

            if (PresetDetailsPanel != null) PresetDetailsPanel.Visibility = Visibility.Visible;
            if (EmptyStateBorder != null) EmptyStateBorder.Visibility = Visibility.Collapsed;
            if (InputStatusText != null) InputStatusText.Visibility = Visibility.Collapsed;
        }

        private void ResetPreview()
        {
            if (!_isInitialized) return;

            Package = null;
            if (PresetDetailsPanel != null) PresetDetailsPanel.Visibility = Visibility.Collapsed;
            if (EmptyStateBorder != null) EmptyStateBorder.Visibility = Visibility.Visible;
            if (SaveToTargetSlotButton != null) SaveToTargetSlotButton.IsEnabled = false;
            if (ApplyToCurrentContextButton != null) ApplyToCurrentContextButton.IsEnabled = false;
            if (InputStatusText != null) InputStatusText.Visibility = Visibility.Collapsed;
        }

        private void ShareCodeInputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_isInitialized) return;

            string text = ShareCodeInputBox?.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                if (InputStatusText != null) InputStatusText.Visibility = Visibility.Collapsed;
                if (Package == null) ResetPreview();
                return;
            }

            var pkg = _sharingService.ImportFromShareCode(text);
            if (pkg != null)
            {
                LoadPackage(pkg);
            }
            else
            {
                if (InputStatusText != null)
                {
                    InputStatusText.Text = "Invalid Share Code format or corrupted data.";
                    InputStatusText.Visibility = Visibility.Visible;
                }
                ResetPreview();
            }
        }

        private void PasteClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                if (Clipboard.ContainsText())
                {
                    string clip = Clipboard.GetText()?.Trim();
                    if (!string.IsNullOrEmpty(clip))
                    {
                        if (ShareCodeInputBox != null) ShareCodeInputBox.Text = clip;
                        return;
                    }
                }

                if (InputStatusText != null)
                {
                    InputStatusText.Text = "Clipboard is empty or does not contain valid text.";
                    InputStatusText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                if (InputStatusText != null)
                {
                    InputStatusText.Text = $"Clipboard error: {ex.Message}";
                    InputStatusText.Visibility = Visibility.Visible;
                }
            }
        }

        private void BrowseFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Open vWheel Hub Preset File",
                    Filter = "vWheel Hub Preset (*.vwh;*.json)|*.vwh;*.json|All Files (*.*)|*.*"
                };

                if (ofd.ShowDialog(this) == true)
                {
                    var pkg = _sharingService.ImportFromFile(ofd.FileName);
                    if (pkg != null)
                    {
                        if (ShareCodeInputBox != null) ShareCodeInputBox.Text = string.Empty;
                        LoadPackage(pkg);
                    }
                    else
                    {
                        if (InputStatusText != null)
                        {
                            InputStatusText.Text = "Could not parse selected file as a valid preset.";
                            InputStatusText.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (InputStatusText != null)
                {
                    InputStatusText.Text = $"File error: {ex.Message}";
                    InputStatusText.Visibility = Visibility.Visible;
                }
            }
        }

        #region Drag and Drop Support
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

        private void Dialog_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (IsValidProfileDrag(e))
            {
                e.Effects = DragDropEffects.Copy;
                if (DialogDropOverlay != null) DialogDropOverlay.Visibility = Visibility.Visible;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Dialog_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (IsValidProfileDrag(e))
            {
                e.Effects = DragDropEffects.Copy;
                if (DialogDropOverlay != null && DialogDropOverlay.Visibility != Visibility.Visible)
                {
                    DialogDropOverlay.Visibility = Visibility.Visible;
                }
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                if (DialogDropOverlay != null && DialogDropOverlay.Visibility != Visibility.Collapsed)
                {
                    DialogDropOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void Dialog_PreviewDragLeave(object sender, DragEventArgs e)
        {
            if (DialogDropOverlay != null) DialogDropOverlay.Visibility = Visibility.Collapsed;
        }

        private void Dialog_PreviewDrop(object sender, DragEventArgs e)
        {
            if (DialogDropOverlay != null) DialogDropOverlay.Visibility = Visibility.Collapsed;

            if (IsValidProfileDrag(e))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    var pkg = _sharingService.ImportFromFile(filePath);
                    if (pkg != null)
                    {
                        if (ShareCodeInputBox != null) ShareCodeInputBox.Text = string.Empty;
                        LoadPackage(pkg);
                    }
                    else
                    {
                        if (InputStatusText != null)
                        {
                            InputStatusText.Text = $"Could not parse dropped file: {Path.GetFileName(filePath)}";
                            InputStatusText.Visibility = Visibility.Visible;
                        }
                    }
                }
                e.Handled = true;
            }
        }
        #endregion

        private void SaveToTargetSlotButton_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = ProfileImportAction.SaveToTargetSlot;
            DialogResult = true;
            Close();
        }

        private void ApplyToCurrentContextButton_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = ProfileImportAction.ApplyToCurrentContext;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ResultAction = ProfileImportAction.None;
            DialogResult = false;
            Close();
        }
    }
}
