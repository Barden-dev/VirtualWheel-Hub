using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SimRacingHub.Application.Features.KeyBindings;
using SimRacingHub.Avalonia.Controls;

namespace SimRacingHub.Avalonia.Views.KeyBindings
{
    /// <summary>
    /// UserControl view for game control key and button bindings, primary/secondary slots,
    /// interactive key/mouse capture, and real-time conflict detection and resolution.
    /// Bound to KeyBindingsViewModel.
    /// </summary>
    public partial class KeyBindingsView : UserControl
    {
        public KeyBindingsView()
        {
            InitializeComponent();
            AddHandler(PointerPressedEvent, OnCapturePointerPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerWheelChangedEvent, OnCapturePointerWheelChanged, RoutingStrategies.Tunnel);
        }

        public KeyBindingsView(KeyBindingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (DataContext == null && App.Services?.KeyBindings != null)
            {
                DataContext = App.Services.KeyBindings;
            }
        }

        private KeyBindControl? FindListeningControl()
        {
            if (DataContext is not KeyBindingsViewModel { IsCapturing: true, CapturingSlot: { } slot }) return null;
            return this.GetVisualDescendants().OfType<KeyBindControl>().FirstOrDefault(c => ReferenceEquals(c.SlotViewModel, slot));
        }

        private void OnCapturePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var listening = FindListeningControl();
            if (listening == null) return;
            if (e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<Button>()
                .Any(button => button.Name is "CancelCaptureButton" or "ReplaceConflictButton")) return;
            listening.CaptureMouseButton(e);
        }

        private void OnCapturePointerWheelChanged(object? sender, PointerWheelEventArgs e) =>
            FindListeningControl()?.CaptureMouseWheel(e);
    }
}
