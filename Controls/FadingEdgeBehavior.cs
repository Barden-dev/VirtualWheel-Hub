using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SimRacingHub.Controls
{
    public static class FadingEdgeBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(FadingEdgeBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value)
        {
            element.SetValue(IsEnabledProperty, value);
        }

        public static bool GetIsEnabled(DependencyObject element)
        {
            return (bool)element.GetValue(IsEnabledProperty);
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.Loaded += Element_Loaded;
                }
                else
                {
                    element.Loaded -= Element_Loaded;
                    element.Unloaded -= Element_Unloaded;
                }
            }
        }

        private static void Element_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Unloaded += Element_Unloaded;
                foreach (var scrollViewer in FindVisualChildren<ScrollViewer>(element))
                {
                    scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
                    scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                    
                    // Handle resizing to update the absolute 40px offset mapping
                    scrollViewer.SizeChanged -= ScrollViewer_SizeChanged;
                    scrollViewer.SizeChanged += ScrollViewer_SizeChanged;
                    
                    UpdateOpacityMask(scrollViewer);
                }
            }
        }
        
        private static void Element_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Unloaded -= Element_Unloaded;
                foreach (var scrollViewer in FindVisualChildren<ScrollViewer>(element))
                {
                    scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
                    scrollViewer.SizeChanged -= ScrollViewer_SizeChanged;
                }
            }
        }

        private static void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                UpdateOpacityMask(scrollViewer);
            }
        }

        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                UpdateOpacityMask(scrollViewer);
            }
        }

        private static void UpdateOpacityMask(ScrollViewer scrollViewer)
        {
            // If we are at the bottom or there's no scrollbar, remove the mask.
            if (scrollViewer.ScrollableHeight == 0 || scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 1)
            {
                scrollViewer.OpacityMask = null;
            }
            else
            {
                // We want the bottom 40 pixels to fade out.
                double maskHeight = 40.0;
                double actualHeight = scrollViewer.ActualHeight;
                
                if (actualHeight <= 0) return;

                double offset = 1.0 - (maskHeight / actualHeight);
                if (offset < 0) offset = 0;

                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                brush.GradientStops.Add(new GradientStop(Colors.Black, 0));
                brush.GradientStops.Add(new GradientStop(Colors.Black, offset));
                brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

                scrollViewer.OpacityMask = brush;
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            
            // If the element itself is the target type, we can yield it, 
            // but usually we search children. If we want to include the parent, 
            // we should do it before calling this. This function just finds children.
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                {
                    yield return t;
                }
                
                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }
}
