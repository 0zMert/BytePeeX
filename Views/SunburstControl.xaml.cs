using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Folderize.Models;
using Folderize.ViewModels;

namespace Folderize.Views
{
    public partial class SunburstControl : UserControl
    {
        public SunburstControl()
        {
            InitializeComponent();
            DataContextChanged += SunburstControl_DataContextChanged;
        }

        private void SunburstControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is MainViewModel vm)
            {
                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MainViewModel.CurrentDrillDownNode) ||
                        ev.PropertyName == nameof(MainViewModel.VisibleNodes))
                    {
                        RenderSunburst();
                    }
                };
            }
            RenderSunburst();
        }

        private void ChartContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderSunburst();
        }

        private void CenterCircle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.NavigateUpCommand.Execute(null);
            }
        }

        public void RenderSunburst()
        {
            if (SunburstCanvas == null || ChartContainer.ActualWidth < 50 || ChartContainer.ActualHeight < 50)
                return;

            SunburstCanvas.Children.Clear();

            if (DataContext is not MainViewModel vm || vm.CurrentDrillDownNode == null)
                return;

            var root = vm.CurrentDrillDownNode;
            if (root.Children.Count == 0 || root.SizeBytes <= 0)
                return;

            double width = ChartContainer.ActualWidth;
            double height = ChartContainer.ActualHeight;
            Point center = new(width / 2.0, height / 2.0);
            double maxRadius = Math.Min(width, height) / 2.0 - 20;
            if (maxRadius < 90) return;

            double innerRadiusL1 = 80;
            double outerRadiusL1 = Math.Min(innerRadiusL1 + 60, innerRadiusL1 + (maxRadius - innerRadiusL1) * 0.48);
            double innerRadiusL2 = outerRadiusL1 + 4;
            double outerRadiusL2 = maxRadius;

            double currentAngle = 0;
            long totalBytes = root.SizeBytes;

            foreach (var child in root.Children)
            {
                if (child.SizeBytes <= 0) continue;

                double sweepAngle = (double)child.SizeBytes / totalBytes * 360.0;
                if (sweepAngle < 0.8) continue; // Don't draw hairline slices

                // Level 1 Arc
                var arc1 = CreateArc(center, innerRadiusL1, outerRadiusL1, currentAngle, sweepAngle, child.DisplayColor, child);
                SunburstCanvas.Children.Add(arc1);

                // Level 2 Sub-Arcs (Grandchildren)
                if (child.Children.Count > 0 && child.SizeBytes > 0 && outerRadiusL2 > innerRadiusL2 + 20)
                {
                    double subAngle = currentAngle;
                    foreach (var subChild in child.Children)
                    {
                        if (subChild.SizeBytes <= 0) continue;
                        double subSweep = (double)subChild.SizeBytes / totalBytes * 360.0;
                        if (subSweep < 0.6) continue;

                        var arc2 = CreateArc(center, innerRadiusL2, outerRadiusL2, subAngle, subSweep, subChild.DisplayColor, subChild);
                        SunburstCanvas.Children.Add(arc2);
                        subAngle += subSweep;
                    }
                }

                currentAngle += sweepAngle;
            }
        }

        private FrameworkElement CreateArc(Point center, double rInner, double rOuter, double startAngleDeg, double sweepAngleDeg, string colorHex, FileSystemNode node)
        {
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
                brushColor = Color.FromRgb(0, 192, 239);
            }

            var path = new Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(Color.FromArgb(220, brushColor.R, brushColor.G, brushColor.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                StrokeThickness = 1,
                Cursor = Cursors.Hand,
                ToolTip = $"{node.Name}\nBoyut: {node.FormattedSize}\nDosyalar: {node.FormattedFiles}\nOran: {node.FormattedPercent}"
            };

            path.MouseEnter += (s, e) =>
            {
                path.Fill = new SolidColorBrush(Color.FromArgb(255, brushColor.R, brushColor.G, brushColor.B));
                path.StrokeThickness = 2;
                TxtCenterName.Text = node.Name;
                TxtCenterSize.Text = node.FormattedSize;
            };

            path.MouseLeave += (s, e) =>
            {
                path.Fill = new SolidColorBrush(Color.FromArgb(220, brushColor.R, brushColor.G, brushColor.B));
                path.StrokeThickness = 1;
                if (DataContext is MainViewModel m && m.CurrentDrillDownNode != null)
                {
                    TxtCenterName.Text = m.CurrentDrillDownNode.Name;
                    TxtCenterSize.Text = m.CurrentDrillDownNode.FormattedSize;
                }
            };

            path.MouseDown += (s, e) =>
            {
                if (e.ClickCount >= 1 && node.IsDirectory && DataContext is MainViewModel m)
                {
                    m.DrillDownCommand.Execute(node);
                }
            };

            return path;
        }
    }
}
