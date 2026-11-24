using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SimRacingHub.Avalonia.Controls
{
    /// <summary>
    /// Real-time axis visualizer for steering, throttle, brake, and clutch axes,
    /// displaying smooth proportional progress bars alongside percentage and raw integer readouts.
    /// </summary>
    public class AxisMonitorControl : Control
    {
        public static readonly StyledProperty<string> SteeringLabelProperty = AvaloniaProperty.Register<AxisMonitorControl, string>(nameof(SteeringLabel), "STEER");
        public static readonly StyledProperty<string> ThrottleLabelProperty = AvaloniaProperty.Register<AxisMonitorControl, string>(nameof(ThrottleLabel), "THROTTLE");
        public static readonly StyledProperty<string> BrakeLabelProperty = AvaloniaProperty.Register<AxisMonitorControl, string>(nameof(BrakeLabel), "BRAKE");
        public static readonly StyledProperty<string> ClutchLabelProperty = AvaloniaProperty.Register<AxisMonitorControl, string>(nameof(ClutchLabel), "CLUTCH");
        public string SteeringLabel { get => GetValue(SteeringLabelProperty); set => SetValue(SteeringLabelProperty, value); }
        public string ThrottleLabel { get => GetValue(ThrottleLabelProperty); set => SetValue(ThrottleLabelProperty, value); }
        public string BrakeLabel { get => GetValue(BrakeLabelProperty); set => SetValue(BrakeLabelProperty, value); }
        public string ClutchLabel { get => GetValue(ClutchLabelProperty); set => SetValue(ClutchLabelProperty, value); }
        #region Styled Properties

        public static readonly StyledProperty<double> SteeringValueProperty =
            AvaloniaProperty.Register<AxisMonitorControl, double>(nameof(SteeringValue), defaultValue: 0.0);

        public static readonly StyledProperty<int> SteeringRawProperty =
            AvaloniaProperty.Register<AxisMonitorControl, int>(nameof(SteeringRaw), defaultValue: 16384);

        public static readonly StyledProperty<double> ThrottleValueProperty =
            AvaloniaProperty.Register<AxisMonitorControl, double>(nameof(ThrottleValue), defaultValue: 0.0);

        public static readonly StyledProperty<int> ThrottleRawProperty =
            AvaloniaProperty.Register<AxisMonitorControl, int>(nameof(ThrottleRaw), defaultValue: 0);

        public static readonly StyledProperty<double> BrakeValueProperty =
            AvaloniaProperty.Register<AxisMonitorControl, double>(nameof(BrakeValue), defaultValue: 0.0);

        public static readonly StyledProperty<int> BrakeRawProperty =
            AvaloniaProperty.Register<AxisMonitorControl, int>(nameof(BrakeRaw), defaultValue: 0);

        public static readonly StyledProperty<double> ClutchValueProperty =
            AvaloniaProperty.Register<AxisMonitorControl, double>(nameof(ClutchValue), defaultValue: 0.0);

        public static readonly StyledProperty<int> ClutchRawProperty =
            AvaloniaProperty.Register<AxisMonitorControl, int>(nameof(ClutchRaw), defaultValue: 0);

        public static readonly StyledProperty<bool> IsSteeringVisibleProperty =
            AvaloniaProperty.Register<AxisMonitorControl, bool>(nameof(IsSteeringVisible), defaultValue: true);

        public static readonly StyledProperty<bool> IsThrottleVisibleProperty =
            AvaloniaProperty.Register<AxisMonitorControl, bool>(nameof(IsThrottleVisible), defaultValue: true);

        public static readonly StyledProperty<bool> IsBrakeVisibleProperty =
            AvaloniaProperty.Register<AxisMonitorControl, bool>(nameof(IsBrakeVisible), defaultValue: true);

        public static readonly StyledProperty<bool> IsClutchVisibleProperty =
            AvaloniaProperty.Register<AxisMonitorControl, bool>(nameof(IsClutchVisible), defaultValue: false);

        public static readonly StyledProperty<bool> ShowRawValuesProperty =
            AvaloniaProperty.Register<AxisMonitorControl, bool>(nameof(ShowRawValues), defaultValue: true);

        public static readonly StyledProperty<bool> ShowPercentageProperty =
            AvaloniaProperty.Register<AxisMonitorControl, bool>(nameof(ShowPercentage), defaultValue: true);

        public static readonly StyledProperty<IBrush> SteeringBrushProperty =
            AvaloniaProperty.Register<AxisMonitorControl, IBrush>(nameof(SteeringBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(96, 165, 250))); // #60A5FA Blue

        public static readonly StyledProperty<IBrush> ThrottleBrushProperty =
            AvaloniaProperty.Register<AxisMonitorControl, IBrush>(nameof(ThrottleBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(74, 222, 128))); // #4ADE80 Emerald

        public static readonly StyledProperty<IBrush> BrakeBrushProperty =
            AvaloniaProperty.Register<AxisMonitorControl, IBrush>(nameof(BrakeBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(248, 113, 113))); // #F87171 Red

        public static readonly StyledProperty<IBrush> ClutchBrushProperty =
            AvaloniaProperty.Register<AxisMonitorControl, IBrush>(nameof(ClutchBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(251, 191, 36))); // #FBBF24 Amber

        public static readonly StyledProperty<IBrush> TrackBrushProperty =
            AvaloniaProperty.Register<AxisMonitorControl, IBrush>(nameof(TrackBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(30, 41, 59))); // #1E293B

        public static readonly StyledProperty<IBrush> BackgroundBrushProperty =
            AvaloniaProperty.Register<AxisMonitorControl, IBrush>(nameof(BackgroundBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(15, 23, 42))); // #0F172A

        public static readonly StyledProperty<IBrush> BorderBrushProperty =
            AvaloniaProperty.Register<AxisMonitorControl, IBrush>(nameof(BorderBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(30, 41, 59))); // #1E293B

        public static readonly StyledProperty<IBrush> TextBrushProperty =
            AvaloniaProperty.Register<AxisMonitorControl, IBrush>(nameof(TextBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(226, 232, 240))); // #E2E8F0

        #endregion

        #region CLR Properties

        public double SteeringValue
        {
            get => GetValue(SteeringValueProperty);
            set => SetValue(SteeringValueProperty, value);
        }

        public int SteeringRaw
        {
            get => GetValue(SteeringRawProperty);
            set => SetValue(SteeringRawProperty, value);
        }

        public double ThrottleValue
        {
            get => GetValue(ThrottleValueProperty);
            set => SetValue(ThrottleValueProperty, value);
        }

        public int ThrottleRaw
        {
            get => GetValue(ThrottleRawProperty);
            set => SetValue(ThrottleRawProperty, value);
        }

        public double BrakeValue
        {
            get => GetValue(BrakeValueProperty);
            set => SetValue(BrakeValueProperty, value);
        }

        public int BrakeRaw
        {
            get => GetValue(BrakeRawProperty);
            set => SetValue(BrakeRawProperty, value);
        }

        public double ClutchValue
        {
            get => GetValue(ClutchValueProperty);
            set => SetValue(ClutchValueProperty, value);
        }

        public int ClutchRaw
        {
            get => GetValue(ClutchRawProperty);
            set => SetValue(ClutchRawProperty, value);
        }

        public bool IsSteeringVisible
        {
            get => GetValue(IsSteeringVisibleProperty);
            set => SetValue(IsSteeringVisibleProperty, value);
        }

        public bool IsThrottleVisible
        {
            get => GetValue(IsThrottleVisibleProperty);
            set => SetValue(IsThrottleVisibleProperty, value);
        }

        public bool IsBrakeVisible
        {
            get => GetValue(IsBrakeVisibleProperty);
            set => SetValue(IsBrakeVisibleProperty, value);
        }

        public bool IsClutchVisible
        {
            get => GetValue(IsClutchVisibleProperty);
            set => SetValue(IsClutchVisibleProperty, value);
        }

        public bool ShowRawValues
        {
            get => GetValue(ShowRawValuesProperty);
            set => SetValue(ShowRawValuesProperty, value);
        }

        public bool ShowPercentage
        {
            get => GetValue(ShowPercentageProperty);
            set => SetValue(ShowPercentageProperty, value);
        }

        public IBrush SteeringBrush
        {
            get => GetValue(SteeringBrushProperty);
            set => SetValue(SteeringBrushProperty, value);
        }

        public IBrush ThrottleBrush
        {
            get => GetValue(ThrottleBrushProperty);
            set => SetValue(ThrottleBrushProperty, value);
        }

        public IBrush BrakeBrush
        {
            get => GetValue(BrakeBrushProperty);
            set => SetValue(BrakeBrushProperty, value);
        }

        public IBrush ClutchBrush
        {
            get => GetValue(ClutchBrushProperty);
            set => SetValue(ClutchBrushProperty, value);
        }

        public IBrush TrackBrush
        {
            get => GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public IBrush BackgroundBrush
        {
            get => GetValue(BackgroundBrushProperty);
            set => SetValue(BackgroundBrushProperty, value);
        }

        public IBrush BorderBrush
        {
            get => GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public IBrush TextBrush
        {
            get => GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        #endregion

        private const double LabelColumnWidth = 75.0;
        private const double ReadoutColumnWidth = 120.0;
        private const double BarHeight = 16.0;
        private const double BarPadding = 12.0;

        static AxisMonitorControl()
        {
            AffectsRender<AxisMonitorControl>(SteeringLabelProperty, ThrottleLabelProperty, BrakeLabelProperty, ClutchLabelProperty);
            AffectsMeasure<AxisMonitorControl>(
                IsSteeringVisibleProperty,
                IsThrottleVisibleProperty,
                IsBrakeVisibleProperty,
                IsClutchVisibleProperty);

            AffectsRender<AxisMonitorControl>(
                SteeringValueProperty,
                SteeringRawProperty,
                ThrottleValueProperty,
                ThrottleRawProperty,
                BrakeValueProperty,
                BrakeRawProperty,
                ClutchValueProperty,
                ClutchRawProperty,
                IsSteeringVisibleProperty,
                IsThrottleVisibleProperty,
                IsBrakeVisibleProperty,
                IsClutchVisibleProperty,
                ShowRawValuesProperty,
                ShowPercentageProperty,
                SteeringBrushProperty,
                ThrottleBrushProperty,
                BrakeBrushProperty,
                ClutchBrushProperty,
                TrackBrushProperty,
                BackgroundBrushProperty,
                BorderBrushProperty,
                TextBrushProperty);
        }

        public AxisMonitorControl()
        {
            ClipToBounds = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int visibleRows = 0;
            if (IsSteeringVisible) visibleRows++;
            if (IsThrottleVisible) visibleRows++;
            if (IsBrakeVisible) visibleRows++;
            if (IsClutchVisible) visibleRows++;

            double h = Math.Max(50, (visibleRows * (BarHeight + BarPadding)) + BarPadding + 8);
            double w = double.IsInfinity(availableSize.Width) ? 360 : Math.Max(200, availableSize.Width);
            return new Size(w, h);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double width = Bounds.Width;
            double height = Bounds.Height;
            if (width <= 0 || height <= 0) return;

            // 1. Outer Container Rect
            var bgRect = new Rect(0, 0, width, height);
            var borderPen = new Pen(BorderBrush, 1.0);
            context.DrawRectangle(BackgroundBrush, borderPen, bgRect, radiusX: 6, radiusY: 6);

            // 2. Compute Layout Rows
            double yOffset = 10.0;
            double contentLeft = 14.0;
            double contentWidth = width - 28.0;

            double barTrackLeft = contentLeft + LabelColumnWidth;
            double barTrackWidth = Math.Max(40, contentWidth - LabelColumnWidth - ReadoutColumnWidth);
            double readoutLeft = barTrackLeft + barTrackWidth + 10.0;

            var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
            var valueTypeface = new Typeface(FontFamily.Default);

            // Steering Row
            if (IsSteeringVisible)
            {
                RenderSteeringRow(context, contentLeft, barTrackLeft, barTrackWidth, readoutLeft, yOffset, typeface, valueTypeface);
                yOffset += BarHeight + BarPadding;
            }

            // Throttle Row
            if (IsThrottleVisible)
            {
                RenderUnipolarRow(context, ThrottleLabel, ThrottleValue, ThrottleRaw, ThrottleBrush,
                    contentLeft, barTrackLeft, barTrackWidth, readoutLeft, yOffset, typeface, valueTypeface);
                yOffset += BarHeight + BarPadding;
            }

            // Brake Row
            if (IsBrakeVisible)
            {
                RenderUnipolarRow(context, BrakeLabel, BrakeValue, BrakeRaw, BrakeBrush,
                    contentLeft, barTrackLeft, barTrackWidth, readoutLeft, yOffset, typeface, valueTypeface);
                yOffset += BarHeight + BarPadding;
            }

            // Clutch Row
            if (IsClutchVisible)
            {
                RenderUnipolarRow(context, ClutchLabel, ClutchValue, ClutchRaw, ClutchBrush,
                    contentLeft, barTrackLeft, barTrackWidth, readoutLeft, yOffset, typeface, valueTypeface);
            }
        }

        private void RenderSteeringRow(
            DrawingContext context,
            double labelX,
            double trackX,
            double trackWidth,
            double readoutX,
            double y,
            Typeface labelTypeface,
            Typeface valueTypeface)
        {
            // Label
            var labelText = new FormattedText(SteeringLabel, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, labelTypeface, 11, TextBrush);
            context.DrawText(labelText, new Point(labelX, y + 1.0));

            // Track Background
            var trackRect = new Rect(trackX, y, trackWidth, BarHeight);
            context.DrawRectangle(TrackBrush, null, trackRect, radiusX: 4, radiusY: 4);

            // Center Reference Notch
            double centerX = trackX + (trackWidth / 2.0);
            var notchPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1.5);
            context.DrawLine(notchPen, new Point(centerX, y), new Point(centerX, y + BarHeight));

            // Proportional Steering Bar (Centered Bipolar)
            double steer = Math.Clamp(SteeringValue, -1.0, 1.0);
            double halfTrack = trackWidth / 2.0;

            if (Math.Abs(steer) > 0.001)
            {
                Rect fillRect;
                if (steer > 0)
                {
                    double fillWidth = steer * halfTrack;
                    fillRect = new Rect(centerX, y + 1, fillWidth, BarHeight - 2);
                }
                else
                {
                    double fillWidth = Math.Abs(steer) * halfTrack;
                    fillRect = new Rect(centerX - fillWidth, y + 1, fillWidth, BarHeight - 2);
                }

                context.DrawRectangle(SteeringBrush, null, fillRect, radiusX: 2, radiusY: 2);
            }

            // Readout
            string readoutStr = FormatReadout(steer * 100.0, SteeringRaw, isBipolar: true);
            var readoutText = new FormattedText(readoutStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, valueTypeface, 11, TextBrush);
            context.DrawText(readoutText, new Point(readoutX, y + 1.0));
        }

        private void RenderUnipolarRow(
            DrawingContext context,
            string label,
            double normalizedValue,
            int rawValue,
            IBrush fillBrush,
            double labelX,
            double trackX,
            double trackWidth,
            double readoutX,
            double y,
            Typeface labelTypeface,
            Typeface valueTypeface)
        {
            // Label
            var labelText = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, labelTypeface, 11, TextBrush);
            context.DrawText(labelText, new Point(labelX, y + 1.0));

            // Track Background
            var trackRect = new Rect(trackX, y, trackWidth, BarHeight);
            context.DrawRectangle(TrackBrush, null, trackRect, radiusX: 4, radiusY: 4);

            // Proportional Progress Fill (Unipolar 0..100%)
            double clamped = Math.Clamp(normalizedValue, 0.0, 1.0);
            if (clamped > 0.001)
            {
                double fillWidth = Math.Max(3.0, clamped * trackWidth);
                var fillRect = new Rect(trackX + 1, y + 1, fillWidth - 2, BarHeight - 2);
                context.DrawRectangle(fillBrush, null, fillRect, radiusX: 3, radiusY: 3);
            }

            // Readout
            string readoutStr = FormatReadout(clamped * 100.0, rawValue, isBipolar: false);
            var readoutText = new FormattedText(readoutStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, valueTypeface, 11, TextBrush);
            context.DrawText(readoutText, new Point(readoutX, y + 1.0));
        }

        private string FormatReadout(double percent, int raw, bool isBipolar)
        {
            string sign = (isBipolar && percent > 0.01) ? "+" : "";
            string pctStr = ShowPercentage ? $"{sign}{percent:F1}%" : "";
            string rawStr = ShowRawValues ? $"({raw})" : "";

            if (ShowPercentage && ShowRawValues)
            {
                return $"{pctStr} {rawStr}";
            }

            return pctStr + rawStr;
        }
    }
}
