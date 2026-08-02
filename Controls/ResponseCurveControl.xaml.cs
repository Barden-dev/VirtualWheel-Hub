using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SimRacingHub.Controls
{
    public partial class ResponseCurveControl : UserControl
    {
        public static readonly DependencyProperty GammaProperty =
            DependencyProperty.Register("Gamma", typeof(double), typeof(ResponseCurveControl),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

        public static readonly DependencyProperty InputValueProperty =
            DependencyProperty.Register("InputValue", typeof(double), typeof(ResponseCurveControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

        public static readonly DependencyProperty IsBipolarProperty =
            DependencyProperty.Register("IsBipolar", typeof(bool), typeof(ResponseCurveControl),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

        public static readonly DependencyProperty CurveColorProperty =
            DependencyProperty.Register("CurveColor", typeof(Brush), typeof(ResponseCurveControl),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(96, 165, 250)), FrameworkPropertyMetadataOptions.AffectsRender));

        public double Gamma
        {
            get => (double)GetValue(GammaProperty);
            set => SetValue(GammaProperty, value);
        }

        public double InputValue
        {
            get => (double)GetValue(InputValueProperty);
            set => SetValue(InputValueProperty, value);
        }

        public bool IsBipolar
        {
            get => (bool)GetValue(IsBipolarProperty);
            set => SetValue(IsBipolarProperty, value);
        }

        public Brush CurveColor
        {
            get => (Brush)GetValue(CurveColorProperty);
            set => SetValue(CurveColorProperty, value);
        }

        public ResponseCurveControl()
        {
            InitializeComponent();
            Loaded += (s, e) => Redraw();
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ResponseCurveControl control)
            {
                control.Redraw();
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        public void Redraw()
        {
            if (PlotCanvas == null || PlotCanvas.ActualWidth <= 0 || PlotCanvas.ActualHeight <= 0) return;

            double width = PlotCanvas.ActualWidth;
            double height = PlotCanvas.ActualHeight;

            double g = Gamma <= 0 ? 1.0 : Gamma;
            bool bipolar = IsBipolar;

            TxtGamma.Text = $"γ = {g:F2}";

            // 1. Draw Grid Lines
            var gridGeometry = new StreamGeometry();
            using (var ctx = gridGeometry.Open())
            {
                // Vertical lines at 25%, 50%, 75%
                for (int i = 1; i < 4; i++)
                {
                    double x = width * (i / 4.0);
                    ctx.BeginFigure(new Point(x, 0), false, false);
                    ctx.LineTo(new Point(x, height), true, false);
                }
                // Horizontal lines at 25%, 50%, 75%
                for (int i = 1; i < 4; i++)
                {
                    double y = height * (i / 4.0);
                    ctx.BeginFigure(new Point(0, y), false, false);
                    ctx.LineTo(new Point(width, y), true, false);
                }
            }
            gridGeometry.Freeze();
            GridPath.Data = gridGeometry;

            // 2. Draw Diagonal Linear Reference Line (1:1)
            var linearGeometry = new StreamGeometry();
            using (var ctx = linearGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, height), false, false);
                ctx.LineTo(new Point(width, 0), true, false);
            }
            linearGeometry.Freeze();
            LinearPath.Data = linearGeometry;

            // 3. Draw Non-Linear Curve
            var curveGeometry = new StreamGeometry();
            int steps = 100;
            using (var ctx = curveGeometry.Open())
            {
                bool first = true;
                for (int i = 0; i <= steps; i++)
                {
                    double t = i / (double)steps; // 0..1 representing Canvas X (left to right)
                    double normIn, normOut;

                    if (bipolar)
                    {
                        // Map t 0..1 to input -1..+1
                        normIn = -1.0 + (t * 2.0);
                        normOut = Math.Sign(normIn) * Math.Pow(Math.Abs(normIn), g);
                        // Map normOut -1..+1 back to canvas Y (height..0)
                        double canvasY = height - ((normOut + 1.0) / 2.0 * height);
                        double canvasX = t * width;

                        if (first) { ctx.BeginFigure(new Point(canvasX, canvasY), false, false); first = false; }
                        else { ctx.LineTo(new Point(canvasX, canvasY), true, false); }
                    }
                    else
                    {
                        // Map t 0..1 to input 0..1
                        normIn = t;
                        normOut = Math.Pow(normIn, g);
                        double canvasY = height - (normOut * height);
                        double canvasX = t * width;

                        if (first) { ctx.BeginFigure(new Point(canvasX, canvasY), false, false); first = false; }
                        else { ctx.LineTo(new Point(canvasX, canvasY), true, false); }
                    }
                }
            }
            curveGeometry.Freeze();
            CurvePath.Data = curveGeometry;

            // 4. Update Live Indicator Dot Position
            double curIn = InputValue;
            double curOut;

            if (bipolar)
            {
                if (curIn > 1.0) curIn = 1.0;
                else if (curIn < -1.0) curIn = -1.0;

                curOut = Math.Sign(curIn) * Math.Pow(Math.Abs(curIn), g);

                double dotX = ((curIn + 1.0) / 2.0) * width;
                double dotY = height - (((curOut + 1.0) / 2.0) * height);

                Canvas.SetLeft(LiveDot, dotX - 5);
                Canvas.SetTop(LiveDot, dotY - 5);

                TxtInput.Text = $"In: {curIn * 100:F0}%";
                TxtOutput.Text = $"Out: {curOut * 100:F0}%";
            }
            else
            {
                if (curIn > 1.0) curIn = 1.0;
                else if (curIn < 0.0) curIn = 0.0;

                curOut = Math.Pow(curIn, g);

                double dotX = curIn * width;
                double dotY = height - (curOut * height);

                Canvas.SetLeft(LiveDot, dotX - 5);
                Canvas.SetTop(LiveDot, dotY - 5);

                TxtInput.Text = $"In: {curIn * 100:F0}%";
                TxtOutput.Text = $"Out: {curOut * 100:F0}%";
            }
        }
    }
}
