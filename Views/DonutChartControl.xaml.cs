using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BytePeeX.ViewModels;

namespace BytePeeX.Views
{
    public partial class DonutChartControl : UserControl
    {
        public DonutChartControl()
        {
            InitializeComponent();
            DataContextChanged += DonutChartControl_DataContextChanged;
        }

        private void DonutChartControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is MainViewModel vm)
            {
                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MainViewModel.TopSegments) ||
                        ev.PropertyName == nameof(MainViewModel.TotalSizeBytes) ||
                        ev.PropertyName == nameof(MainViewModel.RootNode) ||
                        ev.PropertyName == nameof(MainViewModel.SelectedNode))
                    {
                        RenderDonut();
                    }
                };
            }
            RenderDonut();
        }

        private void DonutContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderDonut();
        }

        public void RenderDonut()
        {
            if (DonutCanvas == null) return;

            DonutCanvas.Children.Clear();

            if (DataContext is not MainViewModel vm || vm.TopSegments == null || vm.TopSegments.Count == 0)
                return;

            double width = DonutContainer.ActualWidth;
            double height = DonutContainer.ActualHeight;
            if (width < 30 || height < 30) return;

            Point center = new(width / 2.0, height / 2.0);

            // Dynamically fit radius into available canvas space
            double outerRadius = Math.Max(38, Math.Min(width, height) / 2.0 - 4);
            double innerRadius = Math.Max(28, outerRadius - 16);

            if (outerRadius <= innerRadius) return;

            // Check if there is only 1 dominant segment (>= 99.5%)
            var dominantSeg = vm.TopSegments.FirstOrDefault(s => s.Percentage >= 99.5);
            if (dominantSeg != null)
            {
                var fullRing = CreateFullDonutRing(center, innerRadius, outerRadius, dominantSeg.ColorHex, dominantSeg.Name, dominantSeg.FormattedSize, dominantSeg.FormattedPercentage);
                DonutCanvas.Children.Add(fullRing);
                return;
            }

            double currentAngle = 0;

            foreach (var seg in vm.TopSegments)
            {
                if (seg.Percentage <= 0.1) continue;
                double sweepAngle = (seg.Percentage / 100.0) * 360.0;
                if (sweepAngle < 0.4) continue;

                if (sweepAngle > 359.8) sweepAngle = 359.8;

                var arc = CreateDonutSlice(center, innerRadius, outerRadius, currentAngle, sweepAngle, seg.ColorHex, seg.Name, seg.FormattedSize, seg.FormattedPercentage);
                DonutCanvas.Children.Add(arc);

                currentAngle += sweepAngle;
            }
        }

        private Path CreateFullDonutRing(Point center, double rInner, double rOuter, string colorHex, string name, string sizeStr, string percentStr)
        {
            var outerGeo = new EllipseGeometry(center, rOuter, rOuter);
            var innerGeo = new EllipseGeometry(center, rInner, rInner);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, outerGeo, innerGeo);

            Color brushColor;
            try
            {
                brushColor = (Color)ColorConverter.ConvertFromString(colorHex);
            }
            catch
            {
                brushColor = Color.FromRgb(56, 189, 248);
            }

            return new Path
            {
                Data = combined,
                Fill = new SolidColorBrush(brushColor),
                Stroke = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                StrokeThickness = 1.5,
                ToolTip = $"{name}: {sizeStr} ({percentStr})"
            };
        }

        private Path CreateDonutSlice(Point center, double rInner, double rOuter, double startAngleDeg, double sweepAngleDeg, string colorHex, string name, string sizeStr, string percentStr)
        {
            if (sweepAngleDeg >= 360.0) sweepAngleDeg = 359.8;

            double endAngleDeg = startAngleDeg + sweepAngleDeg;
            double a1 = (startAngleDeg - 90) * Math.PI / 180.0;
            double a2 = (endAngleDeg - 90) * Math.PI / 180.0;

            Point pInnerStart = new(center.X + rInner * Math.Cos(a1), center.Y + rInner * Math.Sin(a1));
            Point pOuterStart = new(center.X + rOuter * Math.Cos(a1), center.Y + rOuter * Math.Sin(a1));
            Point pOuterEnd = new(center.X + rOuter * Math.Cos(a2), center.Y + rOuter * Math.Sin(a2));
            Point pInnerEnd = new(center.X + rInner * Math.Cos(a2), center.Y + rInner * Math.Sin(a2));

            bool isLargeArc = sweepAngleDeg > 180.0;

            var figure = new PathFigure
            {
                StartPoint = pInnerStart,
                IsClosed = true,
                Segments = new PathSegmentCollection
                {
                    new LineSegment(pOuterStart, true),
                    new ArcSegment(pOuterEnd, new Size(rOuter, rOuter), 0, isLargeArc, SweepDirection.Clockwise, true),
                    new LineSegment(pInnerEnd, true),
                    new ArcSegment(pInnerStart, new Size(rInner, rInner), 0, isLargeArc, SweepDirection.Counterclockwise, true)
                }
            };

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            Color brushColor;
            try
            {
                brushColor = (Color)ColorConverter.ConvertFromString(colorHex);
            }
            catch
            {
                brushColor = Color.FromRgb(59, 130, 246);
            }

            var path = new Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(brushColor),
                Stroke = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                StrokeThickness = 1.5,
                ToolTip = $"{name}: {sizeStr} ({percentStr})"
            };

            return path;
        }
    }
}
