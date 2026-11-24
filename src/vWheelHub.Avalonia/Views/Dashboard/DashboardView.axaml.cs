using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using SimRacingHub.Application.Features.Dashboard;
using SimRacingHub.Avalonia.Controls;
using SimRacingHub.Models;

namespace SimRacingHub.Avalonia.Views.Dashboard
{
    /// <summary>
    /// UserControl view for the vWheelHub Dashboard, displaying real-time execution status,
    /// mouse lock controls, live proportional axis monitor, and diagnostics.
    /// Bound to DashboardViewModel.
    /// </summary>
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            EditionSlotNames.Attach(this, MenuSectionSlot);
            PopulateExtraSlots();
            if (OperatingSystem.IsLinux() && this.FindControl<TextBlock>("DeviceModeText") is { } deviceModeText)
                deviceModeText.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.SelectedOutputMode))
                {
                    Converter = new FuncValueConverter<OutputDeviceMode, string>(mode => mode == OutputDeviceMode.Xbox360 ? "uinput (Xbox 360)" : "uinput")
                });
        }

        public DashboardView(DashboardViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        partial void PopulateExtraSlots();

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.Dashboard != null)
            {
                DataContext = App.Services.Dashboard;
            }
        }
    }
}
