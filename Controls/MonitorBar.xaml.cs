using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SimRacingHub.Controls
{
    public partial class MonitorBar : UserControl
    {
        private bool _isDragging = false;
        private Line? _draggedLine = null;
        private DependencyProperty? _draggedProperty = null;
        private MarkerType _draggedMarkerType;

        public MonitorBar()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(double), typeof(MonitorBar), new PropertyMetadata(-32768.0, OnLimitChanged));
        public double Minimum { get { return (double)GetValue(MinimumProperty); } set { SetValue(MinimumProperty, value); } }

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(double), typeof(MonitorBar), new PropertyMetadata(32768.0, OnLimitChanged));
        public double Maximum { get { return (double)GetValue(MaximumProperty); } set { SetValue(MaximumProperty, value); } }

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(MonitorBar), new PropertyMetadata(0.0));
        public double Value { get { return (double)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }

        public static readonly DependencyProperty AbsLimit1Property = DependencyProperty.Register("AbsLimit1", typeof(double), typeof(MonitorBar), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLimitChanged));
        public double AbsLimit1 { get { return (double)GetValue(AbsLimit1Property); } set { SetValue(AbsLimit1Property, value); } }

        public static readonly DependencyProperty AbsLimit2Property = DependencyProperty.Register("AbsLimit2", typeof(double), typeof(MonitorBar), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLimitChanged));
        public double AbsLimit2 { get { return (double)GetValue(AbsLimit2Property); } set { SetValue(AbsLimit2Property, value); } }

        public static readonly DependencyProperty PctLimit1Property = DependencyProperty.Register("PctLimit1", typeof(double), typeof(MonitorBar), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLimitChanged));
        public double PctLimit1 { get { return (double)GetValue(PctLimit1Property); } set { SetValue(PctLimit1Property, value); } }

        public static readonly DependencyProperty PctLimit2Property = DependencyProperty.Register("PctLimit2", typeof(double), typeof(MonitorBar), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLimitChanged));
        public double PctLimit2 { get { return (double)GetValue(PctLimit2Property); } set { SetValue(PctLimit2Property, value); } }

        public static readonly DependencyProperty PctLimit3Property = DependencyProperty.Register("PctLimit3", typeof(double), typeof(MonitorBar), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLimitChanged));
        public double PctLimit3 { get { return (double)GetValue(PctLimit3Property); } set { SetValue(PctLimit3Property, value); } }

        public static readonly DependencyProperty PctLimit4Property = DependencyProperty.Register("PctLimit4", typeof(double), typeof(MonitorBar), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLimitChanged));
        public double PctLimit4 { get { return (double)GetValue(PctLimit4Property); } set { SetValue(PctLimit4Property, value); } }

        public static readonly DependencyProperty InvertPctProperty = DependencyProperty.Register("InvertPct", typeof(bool), typeof(MonitorBar), new PropertyMetadata(false, OnLimitChanged));
        public bool InvertPct { get { return (bool)GetValue(InvertPctProperty); } set { SetValue(InvertPctProperty, value); } }

        public static readonly DependencyProperty AbsLimit1NameProperty = DependencyProperty.Register("AbsLimit1Name", typeof(string), typeof(MonitorBar), new PropertyMetadata(null));
        public string AbsLimit1Name { get { return (string)GetValue(AbsLimit1NameProperty); } set { SetValue(AbsLimit1NameProperty, value); } }

        public static readonly DependencyProperty AbsLimit2NameProperty = DependencyProperty.Register("AbsLimit2Name", typeof(string), typeof(MonitorBar), new PropertyMetadata(null));
        public string AbsLimit2Name { get { return (string)GetValue(AbsLimit2NameProperty); } set { SetValue(AbsLimit2NameProperty, value); } }

        public static readonly DependencyProperty PctLimit1NameProperty = DependencyProperty.Register("PctLimit1Name", typeof(string), typeof(MonitorBar), new PropertyMetadata(null));
        public string PctLimit1Name { get { return (string)GetValue(PctLimit1NameProperty); } set { SetValue(PctLimit1NameProperty, value); } }

        public static readonly DependencyProperty PctLimit2NameProperty = DependencyProperty.Register("PctLimit2Name", typeof(string), typeof(MonitorBar), new PropertyMetadata(null));
        public string PctLimit2Name { get { return (string)GetValue(PctLimit2NameProperty); } set { SetValue(PctLimit2NameProperty, value); } }

        public static readonly DependencyProperty PctLimit3NameProperty = DependencyProperty.Register("PctLimit3Name", typeof(string), typeof(MonitorBar), new PropertyMetadata(null));
        public string PctLimit3Name { get { return (string)GetValue(PctLimit3NameProperty); } set { SetValue(PctLimit3NameProperty, value); } }

        public static readonly DependencyProperty PctLimit4NameProperty = DependencyProperty.Register("PctLimit4Name", typeof(string), typeof(MonitorBar), new PropertyMetadata(null));
        public string PctLimit4Name { get { return (string)GetValue(PctLimit4NameProperty); } set { SetValue(PctLimit4NameProperty, value); } }

        public static readonly DependencyProperty SymLockProperty = DependencyProperty.Register("SymLock", typeof(double), typeof(MonitorBar), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLimitChanged));
        public double SymLock { get { return (double)GetValue(SymLockProperty); } set { SetValue(SymLockProperty, value); } }

        public static readonly DependencyProperty SymRangeProperty = DependencyProperty.Register("SymRange", typeof(double), typeof(MonitorBar), new PropertyMetadata(900.0, OnLimitChanged));
        public double SymRange { get { return (double)GetValue(SymRangeProperty); } set { SetValue(SymRangeProperty, value); } }

        public static readonly DependencyProperty SymLockNameProperty = DependencyProperty.Register("SymLockName", typeof(string), typeof(MonitorBar), new PropertyMetadata(null));
        public string SymLockName { get { return (string)GetValue(SymLockNameProperty); } set { SetValue(SymLockNameProperty, value); } }

        public static readonly DependencyProperty IsSymLockEnabledProperty = DependencyProperty.Register("IsSymLockEnabled", typeof(bool), typeof(MonitorBar), new PropertyMetadata(true, OnLimitChanged));
        public bool IsSymLockEnabled { get { return (bool)GetValue(IsSymLockEnabledProperty); } set { SetValue(IsSymLockEnabledProperty, value); } }

        private static void OnLimitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MonitorBar bar && !bar._isDragging)
            {
                bar.RedrawMarkers();
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isDragging)
            {
                RedrawMarkers();
            }
        }

        private void RedrawMarkers()
        {
            MarkersCanvas.Children.Clear();
            double width = MarkersCanvas.ActualWidth;
            if (width <= 0) return;

            DrawMarkerAbs(AbsLimit1Property, AbsLimit1, width, Brushes.White, AbsLimit1Name);
            DrawMarkerAbs(AbsLimit2Property, AbsLimit2, width, Brushes.White, AbsLimit2Name);
            
            DrawMarkerPct(PctLimit1Property, PctLimit1, width, Brushes.Yellow, PctLimit1Name);
            DrawMarkerPct(PctLimit2Property, PctLimit2, width, Brushes.Yellow, PctLimit2Name);
            DrawMarkerPct(PctLimit3Property, PctLimit3, width, Brushes.Yellow, PctLimit3Name);
            DrawMarkerPct(PctLimit4Property, PctLimit4, width, Brushes.Yellow, PctLimit4Name);
            
            if (IsSymLockEnabled)
            {
                DrawMarkerSym(SymLockProperty, SymLock, SymRange, width, Brushes.Cyan, SymLockName);
            }
        }

        public enum MarkerType { Abs, Pct, SymLeft, SymRight }

        private class MarkerData
        {
            public DependencyProperty? Prop { get; set; }
            public MarkerType Type { get; set; }
        }

        private void DrawMarkerAbs(DependencyProperty prop, double limit, double canvasWidth, Brush color, string name)
        {
            if (double.IsNaN(limit)) return;
            
            double range = Maximum - Minimum;
            if (range <= 0) return;

            double pct = (limit - Minimum) / range;
            Line? line = DrawMarkerLine(pct, canvasWidth, color, name);
            if (line != null)
            {
                line.Tag = new MarkerData { Prop = prop, Type = MarkerType.Abs };
            }
        }

        private void DrawMarkerPct(DependencyProperty prop, double pct, double canvasWidth, Brush color, string name)
        {
            if (double.IsNaN(pct)) return;
            
            double outMin = double.IsNaN(AbsLimit1) ? Minimum : AbsLimit1;
            double outMax = double.IsNaN(AbsLimit2) ? Maximum : AbsLimit2;
            
            double outVal = outMin + pct * (outMax - outMin);
            
            if (InvertPct)
            {
                outVal = outMax - (outVal - outMin);
            }
            
            double range = Maximum - Minimum;
            if (range <= 0) return;
            
            double visualPct = (outVal - Minimum) / range;
            
            Line? line = DrawMarkerLine(visualPct, canvasWidth, color, name);
            if (line != null)
            {
                line.Tag = new MarkerData { Prop = prop, Type = MarkerType.Pct };
            }
        }

        private void DrawMarkerSym(DependencyProperty prop, double lockVal, double rangeVal, double canvasWidth, Brush color, string name)
        {
            if (double.IsNaN(lockVal) || rangeVal <= 0) return;

            double ratio = lockVal / rangeVal;
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            
            double outMin = double.IsNaN(AbsLimit1) ? Minimum : AbsLimit1;
            double outMax = double.IsNaN(AbsLimit2) ? Maximum : AbsLimit2;

            double leftOutVal = outMin + (0.5 - ratio / 2.0) * (outMax - outMin);
            double rightOutVal = outMin + (0.5 + ratio / 2.0) * (outMax - outMin);

            double fullRange = Maximum - Minimum;
            if (fullRange <= 0) return;

            double leftVisualPct = (leftOutVal - Minimum) / fullRange;
            double rightVisualPct = (rightOutVal - Minimum) / fullRange;
            
            Line? leftLine = DrawMarkerLine(leftVisualPct, canvasWidth, color, name + " (Left)");
            if (leftLine != null)
            {
                leftLine.Tag = new MarkerData { Prop = prop, Type = MarkerType.SymLeft };
            }
            
            Line? rightLine = DrawMarkerLine(rightVisualPct, canvasWidth, color, name + " (Right)");
            if (rightLine != null)
            {
                rightLine.Tag = new MarkerData { Prop = prop, Type = MarkerType.SymRight };
            }
        }

        private Line? DrawMarkerLine(double pct, double canvasWidth, Brush color, string name)
        {
            if (pct < 0 || pct > 1) return null;

            double x = pct * canvasWidth;
            
            Line line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = MarkersCanvas.ActualHeight,
                Stroke = color,
                StrokeThickness = 6, // Slightly thicker for dragging
                Opacity = 0.9,
                Cursor = System.Windows.Input.Cursors.SizeWE
            };
            
            if (!string.IsNullOrEmpty(name))
            {
                line.ToolTip = name;
            }
            
            line.SetBinding(Line.Y2Property, new System.Windows.Data.Binding("ActualHeight") { Source = MarkersCanvas });
            
            line.MouseLeftButtonDown += Line_MouseLeftButtonDown;
            line.MouseMove += Line_MouseMove;
            line.MouseLeftButtonUp += Line_MouseLeftButtonUp;

            MarkersCanvas.Children.Add(line);
            return line;
        }

        private void Line_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Line line)
            {
                _draggedLine = line;
                var data = line.Tag as MarkerData;
                if (data != null && data.Prop != null)
                {
                    _draggedProperty = data.Prop;
                    
                    // We can store MarkerType in a private field, replacing _isDraggingAbs
                    _draggedMarkerType = data.Type;
                    
                    line.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        private void Line_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_draggedLine != null && _draggedProperty != null)
            {
                double x = e.GetPosition(MarkersCanvas).X;
                double width = MarkersCanvas.ActualWidth;
                if (width <= 0) return;
                
                if (x < 0) x = 0;
                if (x > width) x = width;

                _draggedLine.X1 = x;
                _draggedLine.X2 = x;

                double visualPct = x / width;

                _isDragging = true;

                if (_draggedMarkerType == MarkerType.Abs)
                {
                    double range = Maximum - Minimum;
                    double val = Minimum + visualPct * range;
                    SetValue(_draggedProperty, (double)(int)val);
                }
                else if (_draggedMarkerType == MarkerType.SymLeft || _draggedMarkerType == MarkerType.SymRight)
                {
                    double range = Maximum - Minimum;
                    double outVal = Minimum + visualPct * range;
                    
                    double outMin = double.IsNaN(AbsLimit1) ? Minimum : AbsLimit1;
                    double outMax = double.IsNaN(AbsLimit2) ? Maximum : AbsLimit2;
                    
                    if (outMax != outMin)
                    {
                        double inputPct = (outVal - outMin) / (outMax - outMin);
                        double centerPct = 0.5;
                        double diff = Math.Abs(inputPct - centerPct);
                        double ratio = diff * 2.0;
                        double newLock = ratio * SymRange;
                        if (newLock < 0) newLock = 0;
                        if (newLock > SymRange) newLock = SymRange;
                        SetValue(_draggedProperty, (double)(int)newLock);
                    }
                }
                else if (_draggedMarkerType == MarkerType.Pct)
                {
                    double range = Maximum - Minimum;
                    double outVal = Minimum + visualPct * range;
                    
                    double outMin = double.IsNaN(AbsLimit1) ? Minimum : AbsLimit1;
                    double outMax = double.IsNaN(AbsLimit2) ? Maximum : AbsLimit2;
                    
                    if (InvertPct)
                    {
                        outVal = outMax - (outVal - outMin);
                    }
                    
                    if (outMax != outMin)
                    {
                        double inputPct = (outVal - outMin) / (outMax - outMin);
                        if (inputPct < 0.0) inputPct = 0.0;
                        if (inputPct > 1.0) inputPct = 1.0;
                        
                        SetValue(_draggedProperty, Math.Round(inputPct, 2));
                    }
                }

                _isDragging = false;
            }
        }

        private void Line_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_draggedLine != null)
            {
                _draggedLine.ReleaseMouseCapture();
                _draggedLine = null;
                _draggedProperty = null;
                RedrawMarkers(); // Redraw everything clean
            }
        }
    }
}
