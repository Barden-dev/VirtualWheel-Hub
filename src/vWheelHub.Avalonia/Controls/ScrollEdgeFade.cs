using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SimRacingHub.Avalonia.Controls;

/// <summary>Non-interactive edge fades over the native scroll presenter.</summary>
public sealed class ScrollEdgeFade : Control
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeFade, ScrollViewer, bool>("Enabled");
    public static readonly AttachedProperty<bool> SuppressedProperty =
        AvaloniaProperty.RegisterAttached<ScrollEdgeFade, Control, bool>("Suppressed", inherits: true);
    private static readonly ConditionalWeakTable<ScrollViewer, Attachment> Attachments = new();

    static ScrollEdgeFade()
    {
        EnabledProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) =>
        {
            if (GetEnabled(viewer)) Attachments.GetValue(viewer, v => new Attachment(v));
            else if (Attachments.TryGetValue(viewer, out var attachment))
            {
                attachment.Dispose();
                Attachments.Remove(viewer);
            }
        });
        SuppressedProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) =>
        {
            if (Attachments.TryGetValue(viewer, out var attachment)) attachment.Refresh();
        });
    }

    public static bool GetEnabled(ScrollViewer viewer) => viewer.GetValue(EnabledProperty);
    public static void SetEnabled(ScrollViewer viewer, bool value) => viewer.SetValue(EnabledProperty, value);

    public bool ShowsTopEdge { get; private set; }
    public bool ShowsBottomEdge { get; private set; }
    private readonly IBrush _top = FadeBrush(true);
    private readonly IBrush _bottom = FadeBrush(false);

    private static IBrush FadeBrush(bool top) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new(Color.FromArgb(top ? (byte)245 : (byte)0, 39, 39, 39), 0),
            new(Color.FromArgb(top ? (byte)0 : (byte)245, 39, 39, 39), 1)
        }
    };

    private void Update(ScrollViewer viewer)
    {
        var top = viewer.Offset.Y > 1;
        var bottom = viewer.Extent.Height - viewer.Viewport.Height - viewer.Offset.Y > 1;
        var visible = !viewer.GetValue(SuppressedProperty) && viewer.IsEffectivelyVisible && viewer.Viewport.Height > 0 && (top || bottom);
        if (ShowsTopEdge == top && ShowsBottomEdge == bottom && IsVisible == visible) return;
        ShowsTopEdge = top;
        ShowsBottomEdge = bottom;
        IsVisible = visible;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var height = Math.Min(26, Bounds.Height / 3);
        var width = Math.Max(0, Bounds.Width - 12); // Leave the native scrollbar unobscured.
        if (ShowsTopEdge) context.DrawRectangle(_top, null, new Rect(0, 0, width, height));
        if (ShowsBottomEdge) context.DrawRectangle(_bottom, null, new Rect(0, Bounds.Height - height, width, height));
    }

    private sealed class Attachment : IDisposable
    {
        private readonly ScrollViewer _viewer;
        private AdornerLayer? _layer;
        private ScrollEdgeFade? _fade;

        public Attachment(ScrollViewer viewer)
        {
            _viewer = viewer;
            viewer.AttachedToVisualTree += Attached;
            viewer.DetachedFromVisualTree += Detached;
            viewer.ScrollChanged += Scrolled;
            viewer.LayoutUpdated += LayoutUpdated;
            QueueAttach();
        }

        private void Attached(object? sender, VisualTreeAttachmentEventArgs e) => QueueAttach();
        private void QueueAttach() => Dispatcher.UIThread.Post(() =>
        {
            if (_fade != null || !GetEnabled(_viewer) || TopLevel.GetTopLevel(_viewer) == null) return;
            _layer = AdornerLayer.GetAdornerLayer(_viewer);
            if (_layer == null) return;
            _fade = new ScrollEdgeFade { Name = "ScrollEdgeFadeOverlay", IsHitTestVisible = false, IsVisible = false };
            AdornerLayer.SetAdornedElement(_fade, _viewer);
            AdornerLayer.SetIsClipEnabled(_fade, true);
            _layer.Children.Add(_fade);
            _fade.Update(_viewer);
        });

        private void Scrolled(object? sender, ScrollChangedEventArgs e) => _fade?.Update(_viewer);
        public void Refresh() => _fade?.Update(_viewer);
        private void LayoutUpdated(object? sender, EventArgs e) => _fade?.Update(_viewer);
        private void Detached(object? sender, VisualTreeAttachmentEventArgs e) => RemoveOverlay();
        private void RemoveOverlay()
        {
            if (_fade != null) _layer?.Children.Remove(_fade);
            _fade = null;
            _layer = null;
        }

        public void Dispose()
        {
            _viewer.AttachedToVisualTree -= Attached;
            _viewer.DetachedFromVisualTree -= Detached;
            _viewer.ScrollChanged -= Scrolled;
            _viewer.LayoutUpdated -= LayoutUpdated;
            RemoveOverlay();
        }
    }
}
