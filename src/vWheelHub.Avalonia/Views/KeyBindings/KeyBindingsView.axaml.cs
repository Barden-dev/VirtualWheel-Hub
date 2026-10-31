using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SimRacingHub.Application.Features.KeyBindings;

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

        private void OnClearConflictClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is KeyBindingsViewModel vm)
            {
                vm.ClearConflict();
            }
        }
    }
}
