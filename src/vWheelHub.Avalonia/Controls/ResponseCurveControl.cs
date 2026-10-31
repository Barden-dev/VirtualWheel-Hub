using System;
using System.Collections;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SimRacingHub.Avalonia.Controls
{
    /// <summary>
    /// Supported marker types for response curve threshold adjustments.
    /// </summary>
    public enum ResponseCurveMarkerType
    {
        Min,
        Max,
        LockClamp
    }

    /// <summary>
    /// Event arguments for marker drag interactions.
    /// </summary>
    public sealed class MarkerDraggedEventArgs : RoutedEventArgs
    {
        public ResponseCurveMarkerType MarkerType { get; }
        public double OldValue { get; }
        public double NewValue { get; }

        public MarkerDraggedEventArgs(
            RoutedEvent? routedEvent,
            object? source,
            ResponseCurveMarkerType markerType,
            double oldValue,
            double newValue)
            : base(routedEvent, source)
        {
            MarkerType = markerType;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// High-performance Avalonia custom control for rendering non-linear input response curves,
    /// interactive Min/Max/Lock draggable markers, and real-time input/output indicator dots.
    /// </summary>
    public class ResponseCurveControl : Control
    {
        public static readonly StyledProperty<string> InputLabelProperty = AvaloniaProperty.Register<ResponseCurveControl, string>(nameof(InputLabel), "In");
        public static readonly StyledProperty<string> OutputLabelProperty = AvaloniaProperty.Register<ResponseCurveControl, string>(nameof(OutputLabel), "Out");
        public string InputLabel { get => GetValue(InputLabelProperty); set => SetValue(InputLabelProperty, value); }
        public string OutputLabel { get => GetValue(OutputLabelProperty); set => SetValue(OutputLabelProperty, value); }
        #region Styled Properties

        public static readonly StyledProperty<IEnumerable?> CurvePointsProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IEnumerable?>(nameof(CurvePoints));

        public static readonly StyledProperty<double> GammaProperty =
            AvaloniaProperty.Register<ResponseCurveControl, double>(nameof(Gamma), defaultValue: 1.0);

        public static readonly StyledProperty<double> InputValueProperty =
            AvaloniaProperty.Register<ResponseCurveControl, double>(nameof(InputValue), defaultValue: 0.0);

        public static readonly StyledProperty<bool> IsBipolarProperty =
            AvaloniaProperty.Register<ResponseCurveControl, bool>(nameof(IsBipolar), defaultValue: false);

        public static readonly StyledProperty<IBrush> CurveBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(CurveBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(96, 165, 250))); // #60A5FA

        public static readonly StyledProperty<IBrush> GridBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(GridBrush),
                defaultValue: new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));

        public static readonly StyledProperty<IBrush> ReferenceBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(ReferenceBrush),
                defaultValue: new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)));

        public static readonly StyledProperty<IBrush> LiveDotBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(LiveDotBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(245, 158, 11))); // #F59E0B

        public static readonly StyledProperty<IBrush> BackgroundBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(BackgroundBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(15, 23, 42))); // #0F172A

        public static readonly StyledProperty<IBrush> BorderBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(BorderBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(30, 41, 59))); // #1E293B

        public static readonly StyledProperty<double?> MinMarkerProperty =
            AvaloniaProperty.Register<ResponseCurveControl, double?>(nameof(MinMarker));

        public static readonly StyledProperty<double?> MaxMarkerProperty =
            AvaloniaProperty.Register<ResponseCurveControl, double?>(nameof(MaxMarker));

        public static readonly StyledProperty<double?> LockClampMarkerProperty =
            AvaloniaProperty.Register<ResponseCurveControl, double?>(nameof(LockClampMarker));

        public static readonly StyledProperty<bool> IsMinMarkerVisibleProperty =
            AvaloniaProperty.Register<ResponseCurveControl, bool>(nameof(IsMinMarkerVisible), defaultValue: false);

        public static readonly StyledProperty<bool> IsMaxMarkerVisibleProperty =
            AvaloniaProperty.Register<ResponseCurveControl, bool>(nameof(IsMaxMarkerVisible), defaultValue: false);

        public static readonly StyledProperty<bool> IsLockClampMarkerVisibleProperty =
            AvaloniaProperty.Register<ResponseCurveControl, bool>(nameof(IsLockClampMarkerVisible), defaultValue: false);

        public static readonly StyledProperty<IBrush> MinMarkerBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(MinMarkerBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(56, 189, 248))); // #38BDF8 Sky

        public static readonly StyledProperty<IBrush> MaxMarkerBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(MaxMarkerBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(244, 63, 94))); // #F43F5E Rose

        public static readonly StyledProperty<IBrush> LockClampMarkerBrushProperty =
            AvaloniaProperty.Register<ResponseCurveControl, IBrush>(nameof(LockClampMarkerBrush),
                defaultValue: new SolidColorBrush(Color.FromRgb(251, 191, 36))); // #FBBF24 Amber

        public static readonly StyledProperty<bool> IsMarkerDraggingEnabledProperty =
            AvaloniaProperty.Register<ResponseCurveControl, bool>(nameof(IsMarkerDraggingEnabled), defaultValue: true);

        public static readonly StyledProperty<ICommand?> MarkerDraggedCommandProperty =
            AvaloniaProperty.Register<ResponseCurveControl, ICommand?>(nameof(MarkerDraggedCommand));

        public static readonly StyledProperty<ICommand?> MinMarkerChangedCommandProperty =
            AvaloniaProperty.Register<ResponseCurveControl, ICommand?>(nameof(MinMarkerChangedCommand));

        public static readonly StyledProperty<ICommand?> MaxMarkerChangedCommandProperty =
            AvaloniaProperty.Register<ResponseCurveControl, ICommand?>(nameof(MaxMarkerChangedCommand));

        public static readonly StyledProperty<ICommand?> LockClampMarkerChangedCommandProperty =
            AvaloniaProperty.Register<ResponseCurveControl, ICommand?>(nameof(LockClampMarkerChangedCommand));

        #endregion

        #region Routed Events

        public static readonly RoutedEvent<MarkerDraggedEventArgs> MarkerDraggedEvent =
            RoutedEvent.Register<ResponseCurveControl, MarkerDraggedEventArgs>(
                nameof(MarkerDragged),
                RoutingStrategies.Bubble);

        public event EventHandler<MarkerDraggedEventArgs>? MarkerDragged
        {
            add => AddHandler(MarkerDraggedEvent, value);
            remove => RemoveHandler(MarkerDraggedEvent, value);
        }

        #endregion

        #region CLR Properties

        /// <summary>Supports production SteeringCurvePoint sequences and Avalonia Point adapters.</summary>
        public IEnumerable? CurvePoints
        {
            get => GetValue(CurvePointsProperty);
            set => SetValue(CurvePointsProperty, value);
        }

        public double Gamma
        {
            get => GetValue(GammaProperty);
            set => SetValue(GammaProperty, value);
        }

        public double InputValue
        {
            get => GetValue(InputValueProperty);
            set => SetValue(InputValueProperty, value);
        }

        // Display the response before calibration, inversion, offset or assists.
        // This also keeps the live point on the curve while the output monitor
        // independently displays the actual mapped device output.
        public double OutputValue => EvaluateOutput(InputValue);

        public bool IsBipolar
        {
            get => GetValue(IsBipolarProperty);
            set => SetValue(IsBipolarProperty, value);
        }

        public IBrush CurveBrush
        {
            get => GetValue(CurveBrushProperty);
            set => SetValue(CurveBrushProperty, value);
        }

        public IBrush GridBrush
        {
            get => GetValue(GridBrushProperty);
            set => SetValue(GridBrushProperty, value);
        }

        public IBrush ReferenceBrush
        {
            get => GetValue(ReferenceBrushProperty);
            set => SetValue(ReferenceBrushProperty, value);
        }

        public IBrush LiveDotBrush
        {
            get => GetValue(LiveDotBrushProperty);
            set => SetValue(LiveDotBrushProperty, value);
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

        public double? MinMarker
        {
            get => GetValue(MinMarkerProperty);
            set => SetValue(MinMarkerProperty, value);
        }

        public double? MaxMarker
        {
            get => GetValue(MaxMarkerProperty);
            set => SetValue(MaxMarkerProperty, value);
        }

        public double? LockClampMarker
        {
            get => GetValue(LockClampMarkerProperty);
            set => SetValue(LockClampMarkerProperty, value);
        }

        public bool IsMinMarkerVisible
        {
            get => GetValue(IsMinMarkerVisibleProperty);
            set => SetValue(IsMinMarkerVisibleProperty, value);
        }

        public bool IsMaxMarkerVisible
        {
            get => GetValue(IsMaxMarkerVisibleProperty);
            set => SetValue(IsMaxMarkerVisibleProperty, value);
        }

        public bool IsLockClampMarkerVisible
        {
            get => GetValue(IsLockClampMarkerVisibleProperty);
            set => SetValue(IsLockClampMarkerVisibleProperty, value);
        }

        public IBrush MinMarkerBrush
        {
            get => GetValue(MinMarkerBrushProperty);
            set => SetValue(MinMarkerBrushProperty, value);
        }

        public IBrush MaxMarkerBrush
        {
            get => GetValue(MaxMarkerBrushProperty);
            set => SetValue(MaxMarkerBrushProperty, value);
        }

        public IBrush LockClampMarkerBrush
        {
            get => GetValue(LockClampMarkerBrushProperty);
            set => SetValue(LockClampMarkerBrushProperty, value);
        }

        public bool IsMarkerDraggingEnabled
        {
            get => GetValue(IsMarkerDraggingEnabledProperty);
            set => SetValue(IsMarkerDraggingEnabledProperty, value);
        }

        public ICommand? MarkerDraggedCommand
        {
            get => GetValue(MarkerDraggedCommandProperty);
            set => SetValue(MarkerDraggedCommandProperty, value);
        }

        public ICommand? MinMarkerChangedCommand
        {
            get => GetValue(MinMarkerChangedCommandProperty);
            set => SetValue(MinMarkerChangedCommandProperty, value);
        }

        public ICommand? MaxMarkerChangedCommand
        {
            get => GetValue(MaxMarkerChangedCommandProperty);
            set => SetValue(MaxMarkerChangedCommandProperty, value);
        }

        public ICommand? LockClampMarkerChangedCommand
        {
            get => GetValue(LockClampMarkerChangedCommandProperty);
            set => SetValue(LockClampMarkerChangedCommandProperty, value);
        }

        #endregion

        #region Private Fields for Drag Tracking

        private readonly ResponseCurveDragState _dragState = new();
        private const double HandleRadius = 6.0;
        private const double HitTolerance = 10.0;
        private const double PaddingPx = 14.0;
        private const double BottomReadoutHeight = 24.0;

        #endregion

        static ResponseCurveControl()
        {
            AffectsRender<ResponseCurveControl>(InputLabelProperty, OutputLabelProperty);
            AffectsRender<ResponseCurveControl>(
                CurvePointsProperty,
                GammaProperty,
                InputValueProperty,
                IsBipolarProperty,
                CurveBrushProperty,
                GridBrushProperty,
                ReferenceBrushProperty,
                LiveDotBrushProperty,
                BackgroundBrushProperty,
                BorderBrushProperty,
                MinMarkerProperty,
                MaxMarkerProperty,
                LockClampMarkerProperty,
                IsMinMarkerVisibleProperty,
                IsMaxMarkerVisibleProperty,
                IsLockClampMarkerVisibleProperty,
                MinMarkerBrushProperty,
                MaxMarkerBrushProperty,
                LockClampMarkerBrushProperty);
        }

        public ResponseCurveControl()
        {
            ClipToBounds = true;
            Focusable = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) ? 300 : Math.Max(120, availableSize.Width);
            double h = double.IsInfinity(availableSize.Height) ? 180 : Math.Max(100, availableSize.Height);
            return new Size(w, h);
        }

        #region Rendering

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double width = Bounds.Width;
            double height = Bounds.Height;
            if (width <= 0 || height <= 0) return;

            // 1. Draw Background and Border
            var bgRect = new Rect(0, 0, width, height);
            var borderPen = new Pen(BorderBrush, 1.0);
            context.DrawRectangle(BackgroundBrush, borderPen, bgRect, radiusX: 6, radiusY: 6);

            // 2. Compute Plot Inner Rectangle
            double plotWidth = Math.Max(10, width - (PaddingPx * 2));
            double plotHeight = Math.Max(10, height - (PaddingPx * 2) - BottomReadoutHeight);
            var plotRect = new Rect(PaddingPx, PaddingPx, plotWidth, plotHeight);

            // 3. Grid Lines
            var gridPen = new Pen(GridBrush, 1.0, dashStyle: DashStyle.Dash);
            for (int i = 1; i <= 3; i++)
            {
                double frac = i / 4.0;
                double gx = plotRect.Left + (plotWidth * frac);
                context.DrawLine(gridPen, new Point(gx, plotRect.Top), new Point(gx, plotRect.Bottom));

                double gy = plotRect.Top + (plotHeight * frac);
                context.DrawLine(gridPen, new Point(plotRect.Left, gy), new Point(plotRect.Right, gy));
            }

            // 4. Reference Diagonal Line (1:1)
            var refPen = new Pen(ReferenceBrush, 1.0, dashStyle: DashStyle.Dash);
            context.DrawLine(refPen, new Point(plotRect.Left, plotRect.Bottom), new Point(plotRect.Right, plotRect.Top));

            // 5. Draw Non-Linear Curve
            DrawCurve(context, plotRect);

            // 6. Draw Draggable Markers
            DrawMarkers(context, plotRect);

            // 7. Live Dot Indicator
            DrawLiveDot(context, plotRect);

            // 8. Bottom Data Readout
            DrawBottomReadout(context, width, height, plotRect);
        }

        private void DrawCurve(DrawingContext context, Rect plotRect)
        {
            var pointsList = ResponseCurvePointAdapter.Normalize(CurvePoints);
            var geometryTransform = new ResponseCurveGeometry(plotRect, IsBipolar);
            var curvePen = new Pen(CurveBrush, 2.5);

            if (pointsList.Count >= 2)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    bool first = true;
                    foreach (ResponseCurvePoint point in pointsList)
                    {
                        var pt = geometryTransform.ToCanvas(point);
                        if (first)
                        {
                            ctx.BeginFigure(pt, isFilled: false);
                            first = false;
                        }
                        else
                        {
                            ctx.LineTo(pt);
                        }
                    }
                }
                context.DrawGeometry(null, curvePen, geometry);
            }
            else
            {
                // Fallback: Analytical curve based on Gamma
                double g = Gamma <= 0 ? 1.0 : Gamma;
                const int steps = 80;
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    bool first = true;
                    for (int i = 0; i <= steps; i++)
                    {
                        double t = i / (double)steps;
                        double normIn, normOut;

                        if (IsBipolar)
                        {
                            normIn = -1.0 + (t * 2.0);
                            normOut = Math.Sign(normIn) * Math.Pow(Math.Abs(normIn), g);
                        }
                        else
                        {
                            normIn = t;
                            normOut = Math.Pow(normIn, g);
                        }

                        var pt = geometryTransform.ToCanvas(normIn, normOut);
                        if (first)
                        {
                            ctx.BeginFigure(pt, isFilled: false);
                            first = false;
                        }
                        else
                        {
                            ctx.LineTo(pt);
                        }
                    }
                }
                context.DrawGeometry(null, curvePen, geometry);
            }
        }

        private void DrawMarkers(DrawingContext context, Rect plotRect)
        {
            if (IsMinMarkerVisible && MinMarker.HasValue)
            {
                DrawMarkerLine(context, plotRect, MinMarker.Value, MinMarkerBrush, ResponseCurveMarkerType.Min);
            }

            if (IsMaxMarkerVisible && MaxMarker.HasValue)
            {
                DrawMarkerLine(context, plotRect, MaxMarker.Value, MaxMarkerBrush, ResponseCurveMarkerType.Max);
            }

            if (IsLockClampMarkerVisible && LockClampMarker.HasValue)
            {
                DrawMarkerLine(context, plotRect, LockClampMarker.Value, LockClampMarkerBrush, ResponseCurveMarkerType.LockClamp);
            }
        }

        private void DrawMarkerLine(
            DrawingContext context,
            Rect plotRect,
            double markerValue,
            IBrush brush,
            ResponseCurveMarkerType type)
        {
            double screenX = new ResponseCurveGeometry(plotRect, IsBipolar).InputToCanvasX(markerValue);
            var pen = new Pen(brush, 1.5, dashStyle: DashStyle.Dash);
            context.DrawLine(pen, new Point(screenX, plotRect.Top), new Point(screenX, plotRect.Bottom));

            // Draw top handle thumb
            var handleCenter = new Point(screenX, plotRect.Top);
            var handlePen = new Pen(Brushes.White, 1.5);
            context.DrawEllipse(brush, handlePen, handleCenter, HandleRadius, HandleRadius);

            // Draw bottom handle thumb
            var bottomCenter = new Point(screenX, plotRect.Bottom);
            context.DrawEllipse(brush, handlePen, bottomCenter, HandleRadius, HandleRadius);
        }

        private void DrawLiveDot(DrawingContext context, Rect plotRect)
        {
            double curIn = InputValue;
            double curOut = OutputValue;

            var dotPt = new ResponseCurveGeometry(plotRect, IsBipolar).ToCanvas(curIn, curOut);

            // Outer ring
            var outerPen = new Pen(LiveDotBrush, 1.5);
            context.DrawEllipse(null, outerPen, dotPt, 8.0, 8.0);

            // Inner filled dot
            var innerPen = new Pen(Brushes.White, 1.5);
            context.DrawEllipse(LiveDotBrush, innerPen, dotPt, 5.0, 5.0);
        }

        private void DrawBottomReadout(DrawingContext context, double width, double height, Rect plotRect)
        {
            double curIn = InputValue;
            double curOut = OutputValue;
            double g = Gamma <= 0 ? 1.0 : Gamma;

            string inText = IsBipolar
                ? $"{InputLabel}: {curIn * 100.0:F0}%"
                : $"{InputLabel}: {Math.Clamp(curIn, 0.0, 1.0) * 100.0:F0}%";

            string gammaText = $"γ = {g:F2}";

            string outText = IsBipolar
                ? $"{OutputLabel}: {curOut * 100.0:F0}%"
                : $"{OutputLabel}: {Math.Clamp(curOut, 0.0, 1.0) * 100.0:F0}%";

            var typeface = new Typeface(FontFamily.Default);
            var textBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94A3B8

            double y = plotRect.Bottom + 6.0;

            var inFormatted = new FormattedText(inText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11, textBrush);
            context.DrawText(inFormatted, new Point(plotRect.Left, y));

            var gammaFormatted = new FormattedText(gammaText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11, textBrush);
            double gammaX = (width - gammaFormatted.Width) / 2.0;
            context.DrawText(gammaFormatted, new Point(gammaX, y));

            var outFormatted = new FormattedText(outText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11, textBrush);
            double outX = Math.Max(gammaX + gammaFormatted.Width + 4, plotRect.Right - outFormatted.Width);
            context.DrawText(outFormatted, new Point(outX, y));
        }

        #endregion

        public double EvaluateOutput(double input) => ResponseCurveEvaluator.Evaluate(
            input,
            ResponseCurvePointAdapter.Normalize(CurvePoints),
            Gamma,
            IsBipolar);

        #region Pointer and Drag Interaction

        private Rect GetPlotRect()
        {
            double w = Math.Max(10, Bounds.Width - (PaddingPx * 2));
            double h = Math.Max(10, Bounds.Height - (PaddingPx * 2) - BottomReadoutHeight);
            return new Rect(PaddingPx, PaddingPx, w, h);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (!IsMarkerDraggingEnabled) return;

            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed) return;

            var plotRect = GetPlotRect();
            var pos = point.Position;

            // Hit test markers (priority: LockClamp -> Max -> Min)
            if (IsLockClampMarkerVisible && LockClampMarker.HasValue &&
                HitTestMarker(pos.X, LockClampMarker.Value, plotRect))
            {
                StartDrag(ResponseCurveMarkerType.LockClamp, LockClampMarker.Value, e);
                return;
            }

            if (IsMaxMarkerVisible && MaxMarker.HasValue &&
                HitTestMarker(pos.X, MaxMarker.Value, plotRect))
            {
                StartDrag(ResponseCurveMarkerType.Max, MaxMarker.Value, e);
                return;
            }

            if (IsMinMarkerVisible && MinMarker.HasValue &&
                HitTestMarker(pos.X, MinMarker.Value, plotRect))
            {
                StartDrag(ResponseCurveMarkerType.Min, MinMarker.Value, e);
                return;
            }
        }

        private bool HitTestMarker(double mouseX, double markerValue, Rect plotRect)
        {
            double markerScreenX = new ResponseCurveGeometry(plotRect, IsBipolar).InputToCanvasX(markerValue);
            return Math.Abs(mouseX - markerScreenX) <= HitTolerance;
        }

        private void StartDrag(ResponseCurveMarkerType type, double initialValue, PointerPressedEventArgs e)
        {
            _dragState.Begin(type, initialValue);
            e.Pointer.Capture(this);
            e.Handled = true;
            Focus();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_dragState.ActiveMarker == null) return;

            var plotRect = GetPlotRect();
            var pos = e.GetPosition(this);
            double newNorm = new ResponseCurveGeometry(plotRect, IsBipolar).CanvasXToInput(pos.X);

            double oldVal = _dragState.ActiveMarker switch
            {
                ResponseCurveMarkerType.Min => MinMarker ?? 0.0,
                ResponseCurveMarkerType.Max => MaxMarker ?? 1.0,
                ResponseCurveMarkerType.LockClamp => LockClampMarker ?? 1.0,
                _ => 0.0
            };

            switch (_dragState.ActiveMarker.Value)
            {
                case ResponseCurveMarkerType.Min:
                    if (MaxMarker.HasValue) newNorm = Math.Min(newNorm, MaxMarker.Value);
                    MinMarker = newNorm;
                    MinMarkerChangedCommand?.Execute(newNorm);
                    break;

                case ResponseCurveMarkerType.Max:
                    if (MinMarker.HasValue) newNorm = Math.Max(newNorm, MinMarker.Value);
                    MaxMarker = newNorm;
                    MaxMarkerChangedCommand?.Execute(newNorm);
                    break;

                case ResponseCurveMarkerType.LockClamp:
                    LockClampMarker = newNorm;
                    LockClampMarkerChangedCommand?.Execute(newNorm);
                    break;
            }

            var args = new MarkerDraggedEventArgs(
                MarkerDraggedEvent,
                this,
                _dragState.ActiveMarker.Value,
                oldVal,
                newNorm);

            RaiseEvent(args);
            MarkerDraggedCommand?.Execute(args);

            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_dragState.ActiveMarker != null)
            {
                e.Pointer.Capture(null);
                _dragState.End();
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            CancelDrag();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape && _dragState.ActiveMarker != null)
            {
                CancelDrag();
                e.Handled = true;
            }
        }

        public void CancelDrag()
        {
            if (_dragState.ActiveMarker == null) return;

            switch (_dragState.ActiveMarker.Value)
            {
                case ResponseCurveMarkerType.Min:
                    MinMarker = _dragState.OriginalValue;
                    break;
                case ResponseCurveMarkerType.Max:
                    MaxMarker = _dragState.OriginalValue;
                    break;
                case ResponseCurveMarkerType.LockClamp:
                    LockClampMarker = _dragState.OriginalValue;
                    break;
            }

            _dragState.End();
            InvalidateVisual();
        }

        #endregion
    }
}
