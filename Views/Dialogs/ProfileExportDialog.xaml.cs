using System;
using System.Windows;
using Microsoft.Win32;
using SimRacingHub.Models;
using SimRacingHub.Services;
using Wpf.Ui.Controls;

namespace SimRacingHub.Views.Dialogs
{
    public partial class ProfileExportDialog : FluentWindow
    {
        private readonly Profile _profile;
        private readonly ProfileContext _context;
        private readonly ProfileSharingService _sharingService;

        public ProfileExportDialog(Profile profile, ProfileContext context, ProfileSharingService sharingService)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _context = context ?? new ProfileContext("Universal");
            _sharingService = sharingService ?? new ProfileSharingService();

            InitializeComponent();

            ContextText.Text = !string.IsNullOrEmpty(_context.DisplayPath) ? _context.DisplayPath : "Universal";

            string defaultName = !string.IsNullOrEmpty(_context.Car) 
                ? $"{_context.Car} Setup" 
                : (!string.IsNullOrEmpty(_context.Game) && _context.Game != "Universal" ? $"{_context.Game} Setup" : "Universal Setup");

            PresetNameBox.Text = defaultName;
        }

        private ProfileSharePackage BuildPackage()
        {
            return new ProfileSharePackage
            {
                SchemaVersion = ProfileSharePackage.CurrentSchemaVersion,
                AppVersion = UpdateService.CurrentVersion,
                Name = !string.IsNullOrWhiteSpace(PresetNameBox.Text) ? PresetNameBox.Text.Trim() : "Preset",
                Author = !string.IsNullOrWhiteSpace(AuthorBox.Text) ? AuthorBox.Text.Trim() : string.Empty,
                Description = !string.IsNullOrWhiteSpace(DescriptionBox.Text) ? DescriptionBox.Text.Trim() : string.Empty,
                CreatedAtUtc = DateTime.UtcNow,
                TargetContext = new ProfileContext
                {
                    Game = _context.Game,
                    CarClass = _context.CarClass,
                    Car = _context.Car
                },
                Settings = _profile
            };
        }

        private void CopyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var package = BuildPackage();
                string shareCode = _sharingService.ExportToShareCode(package);
                Clipboard.SetText(shareCode);

                StatusNoticeText.Text = "✔ Share Code copied to clipboard! Paste it anywhere (e.g. Discord).";
            }
            catch (Exception ex)
            {
                StatusNoticeText.Foreground = System.Windows.Media.Brushes.Red;
                StatusNoticeText.Text = $"Failed to generate code: {ex.Message}";
            }
        }

        private void SaveFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var package = BuildPackage();
                string defaultFileName = _sharingService.GenerateDefaultFileName(_context, package.Name);

                var sfd = new SaveFileDialog
                {
                    Title = "Export Profile Preset",
                    Filter = "vWheel Hub Profile (*.vwh)|*.vwh|JSON File (*.json)|*.json",
                    FileName = defaultFileName
                };

                if (sfd.ShowDialog(this) == true)
                {
                    _sharingService.ExportToFile(sfd.FileName, package);
                    StatusNoticeText.Text = $"✔ Saved file: {System.IO.Path.GetFileName(sfd.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusNoticeText.Foreground = System.Windows.Media.Brushes.Red;
                StatusNoticeText.Text = $"Failed to export file: {ex.Message}";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
