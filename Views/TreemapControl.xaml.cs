using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BytePeeX.Models;
using BytePeeX.ViewModels;

namespace BytePeeX.Views
{
    public partial class TreemapControl : UserControl
    {
        private static readonly string[] HarmonicPalette = new[]
        {
            "#2563EB", // Rich Blue (Users)
            "#16A34A", // Forest Green (Windows)
            "#D97706", // Amber (Program Files)
            "#0891B2", // Cyan (ProgramData)
            "#DC2626", // Red (Temp)
            "#64748B", // Slate ($Recycle.Bin)
            "#9333EA", // Purple (Other)
            "#0D9488", // Teal
            "#E11D48", // Rose
            "#6366F1", // Indigo
            "#EA580C", // Orange
            "#0284C7"  // Sky
        };

        public TreemapControl()
        {
            InitializeComponent();
            DataContextChanged += TreemapControl_DataContextChanged;
        }

        private void TreemapControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is MainViewModel vm)
            {
                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(MainViewModel.CurrentDrillDownNode) ||
                        ev.PropertyName == nameof(MainViewModel.VisibleNodes) ||
                        ev.PropertyName == nameof(MainViewModel.HideSystemInTreemap) ||
                        ev.PropertyName == nameof(MainViewModel.UseBalancedTreemapScale) ||
                        ev.PropertyName == nameof(MainViewModel.TreeStructureVersion) ||
                        ev.PropertyName == nameof(MainViewModel.SelectedNode))
                    {
                        RenderTreemap();
                    }
                };
            }
            RenderTreemap();
        }

        private void TreemapContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderTreemap();
        }

        public void RenderTreemap()
        {
            if (TreemapCanvas == null || TreemapContainer.ActualWidth < 40 || TreemapContainer.ActualHeight < 40)
                return;

            TreemapCanvas.Children.Clear();

            if (DataContext is not MainViewModel vm || vm.CurrentDrillDownNode == null)
                return;

            var targetNode = vm.CurrentDrillDownNode;
            var query = targetNode.Children.Where(c => c.SizeBytes > 0);

            if (vm.HideSystemInTreemap)
            {
                query = query.Where(c => !c.IsSystemNode);
            }

            var children = query.OrderByDescending(c => c.SizeBytes).ToList();
            if (children.Count == 0) return;

            double width = TreemapContainer.ActualWidth;
            double height = TreemapContainer.ActualHeight;

            long totalSize = targetNode.SizeBytes > 0 ? targetNode.SizeBytes : children.Sum(c => c.SizeBytes);

            LayoutGroup(children, new Rect(0, 0, width, height), depth: 0, parentSize: totalSize, parentColor: null);
        }

        private double GetNodeVisualWeight(FileSystemNode node, bool balanced)
        {
            if (node.SizeBytes <= 0) return 0;
            if (!balanced) return (double)node.SizeBytes;

            // Power factor 0.40 compresses the extreme dynamic range of byte sizes:
            // 200 GB -> ~31,000, 40 GB -> ~17,000, 20 GB -> ~13,000, 1 GB -> ~4,000, 100 MB -> ~1,600
            // Giant folders still remain the largest, but are prevented from consuming 80-90% of screen.
            return Math.Pow((double)node.SizeBytes, 0.40);
        }

        private void LayoutGroup(List<FileSystemNode> nodes, Rect bounds, int depth, long parentSize, Color? parentColor)
        {
            if (nodes.Count == 0 || bounds.Width < 2 || bounds.Height < 2)
                return;

            List<FileSystemNode> layoutNodes;
            if (nodes.Count > 7 && depth > 0)
            {
                layoutNodes = nodes.Take(6).ToList();
                long otherBytes = nodes.Skip(6).Sum(n => n.SizeBytes);
                if (otherBytes > 0)
                {
                    var otherNode = new FileSystemNode
                    {
                        Name = "Other",
                        SizeBytes = otherBytes,
                        IsDirectory = false
                    };
                    layoutNodes.Add(otherNode);
                }
            }
            else
            {
                layoutNodes = nodes;
            }

            LayoutSliceAndDice(layoutNodes, bounds, depth, parentSize, parentColor);
        }

        private void LayoutSliceAndDice(List<FileSystemNode> nodes, Rect bounds, int depth, long parentSize, Color? parentColor)
        {
            if (nodes.Count == 0 || bounds.Width < 2 || bounds.Height < 2 || depth > 10)
                return;

            if (nodes.Count == 1)
            {
                RenderSingleNode(nodes[0], bounds, depth, parentSize, parentColor);
                return;
            }

            bool balanced = DataContext is MainViewModel vm && vm.UseBalancedTreemapScale;
            double total = nodes.Sum(n => GetNodeVisualWeight(n, balanced));
            if (total <= 0) return;

            double half = 0;
            int mid = 1;
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                half += GetNodeVisualWeight(nodes[i], balanced);
                if (half >= total / 2.0)
                {
                    mid = i + 1;
                    break;
                }
            }

            // Ensure mid is strictly between 1 and nodes.Count - 1 to guarantee termination
            mid = Math.Max(1, Math.Min(nodes.Count - 1, mid));

            var group1 = nodes.Take(mid).ToList();
            var group2 = nodes.Skip(mid).ToList();

            double ratio1 = group1.Sum(n => GetNodeVisualWeight(n, balanced)) / total;

            Rect r1, r2;
            if (bounds.Width > bounds.Height)
            {
                double w1 = Math.Round(bounds.Width * ratio1, 1);
                r1 = new Rect(bounds.X, bounds.Y, w1, bounds.Height);
                r2 = new Rect(bounds.X + w1, bounds.Y, bounds.Width - w1, bounds.Height);
            }
            else
            {
                double h1 = Math.Round(bounds.Height * ratio1, 1);
                r1 = new Rect(bounds.X, bounds.Y, bounds.Width, h1);
                r2 = new Rect(bounds.X, bounds.Y + h1, bounds.Width, bounds.Height - h1);
            }

            LayoutSliceAndDice(group1, r1, depth, parentSize, parentColor);
            LayoutSliceAndDice(group2, r2, depth, parentSize, parentColor);
        }

        private void RenderSingleNode(FileSystemNode node, Rect rect, int depth, long parentSize, Color? parentColor)
        {
            if (rect.Width < 4 || rect.Height < 4) return;
            if (DataContext is not MainViewModel vm) return;

            Color baseColor = ResolveNodeColor(node, depth, parentColor);

            double percent = parentSize > 0 ? (double)node.SizeBytes / parentSize * 100.0 : 0;
            string percentText = percent >= 0.1 ? $"{percent:F1}%" : "<0.1%";

            bool canNest = node.IsExpanded && 
                           node.Children.Count > 0 && 
                           rect.Width >= 75 && 
                           rect.Height >= 70 && 
                           depth < 4;

            if (canNest)
            {
                RenderContainerNode(node, rect, depth, parentSize, baseColor, percentText, vm);
            }
            else
            {
                RenderLeafNode(node, rect, depth, parentSize, baseColor, percentText, vm);
            }
        }

        private void RenderContainerNode(FileSystemNode node, Rect rect, int depth, long parentSize, Color baseColor, string percentText, MainViewModel vm)
        {
            bool isSelected = vm.SelectedNode == node;

            var container = new Border
            {
                Width = Math.Max(0, rect.Width - 2),
                Height = Math.Max(0, rect.Height - 2),
                Background = new SolidColorBrush(Color.FromArgb(140, baseColor.R, baseColor.G, baseColor.B)),
                BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                BorderThickness = new Thickness(isSelected ? 2 : 1.5),
                CornerRadius = new CornerRadius(5),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                ToolTip = $"{node.Name} (Açık Klasör)\nBoyut: {node.FormattedSize} ({percentText})\nKlasörler: {node.FolderCount}, Dosyalar: {node.FileCount}\nYol: {node.FullPath}"
            };

            Canvas.SetLeft(container, rect.X + 1);
            Canvas.SetTop(container, rect.Y + 1);

            var headerBorder = new Border
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)),
                Padding = new Thickness(6, 3, 6, 3)
            };

            var headerStack = new StackPanel { Orientation = Orientation.Vertical };
            var nameTxt = new TextBlock
            {
                Text = node.Name,
                FontWeight = FontWeights.Bold,
                FontSize = rect.Width > 120 ? 12 : 11,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var metaTxt = new TextBlock
            {
                Text = $"{node.FormattedSize}  •  {percentText}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240))
            };

            headerStack.Children.Add(nameTxt);
            headerStack.Children.Add(metaTxt);
            headerBorder.Child = headerStack;

            container.Child = headerBorder;

            container.MouseDown += (s, e) =>
            {
                e.Handled = true;
                if (e.ClickCount == 1)
                {
                    vm.SelectedNode = node;
                }
                else if (e.ClickCount >= 2)
                {
                    vm.ToggleNodeExpansion(node);
                }
            };

            TreemapCanvas.Children.Add(container);

            double headerHeight = 36;
            Rect innerBounds = new Rect(rect.X + 4, rect.Y + headerHeight + 2, rect.Width - 8, rect.Height - headerHeight - 6);

            if (innerBounds.Width > 20 && innerBounds.Height > 20)
            {
                var query = node.Children.Where(c => c.SizeBytes > 0);
                if (vm.HideSystemInTreemap)
                {
                    query = query.Where(c => !c.IsSystemNode);
                }
                var children = query.OrderByDescending(c => c.SizeBytes).ToList();
                if (children.Count > 0)
                {
                    LayoutGroup(children, innerBounds, depth + 1, node.SizeBytes, baseColor);
                }
            }
        }

        private void RenderLeafNode(FileSystemNode node, Rect rect, int depth, long parentSize, Color baseColor, string percentText, MainViewModel vm)
        {
            bool isSelected = vm.SelectedNode == node;

            var border = new Border
            {
                Width = Math.Max(0, rect.Width - 2),
                Height = Math.Max(0, rect.Height - 2),
                Background = new SolidColorBrush(Color.FromArgb(230, baseColor.R, baseColor.G, baseColor.B)),
                BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                ToolTip = $"{node.Name}\nBoyut: {node.FormattedSize} ({percentText})\nDosyalar: {node.FormattedFiles}\nYol: {node.FullPath}"
            };

            Canvas.SetLeft(border, rect.X + 1);
            Canvas.SetTop(border, rect.Y + 1);

            if (rect.Width > 35 && rect.Height > 24)
            {
                var panel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(rect.Width > 60 ? 6 : 3)
                };

                var txtName = new TextBlock
                {
                    Text = node.Name,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = rect.Width > 90 && rect.Height > 55 ? 12 : (rect.Width > 60 ? 11 : 10),
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                var txtSize = new TextBlock
                {
                    Text = node.FormattedSize,
                    FontSize = rect.Width > 70 ? 11 : 9,
                    FontWeight = FontWeights.Normal,
                    Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))
                };

                panel.Children.Add(txtName);
                panel.Children.Add(txtSize);

                if (rect.Height > 45 && rect.Width > 50)
                {
                    var txtPercent = new TextBlock
                    {
                        Text = percentText,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
                    };
                    panel.Children.Add(txtPercent);
                }

                border.Child = panel;
            }

            border.MouseEnter += (s, e) =>
            {
                byte r = (byte)Math.Min(255, baseColor.R + 25);
                byte g = (byte)Math.Min(255, baseColor.G + 25);
                byte b = (byte)Math.Min(255, baseColor.B + 25);
                border.Background = new SolidColorBrush(Color.FromArgb(255, r, g, b));
                border.BorderBrush = Brushes.White;
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(230, baseColor.R, baseColor.G, baseColor.B));
                border.BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            };

            border.MouseDown += (s, e) =>
            {
                e.Handled = true;
                if (e.ClickCount == 1)
                {
                    vm.SelectedNode = node;
                }
                else if (e.ClickCount >= 2 && node.IsDirectory)
                {
                    vm.ToggleNodeExpansion(node);
                }
            };

            TreemapCanvas.Children.Add(border);
        }

        private Color ResolveNodeColor(FileSystemNode node, int depth, Color? parentColor)
        {
            if (node.Name == "Other") return Color.FromRgb(100, 116, 139);

            string n = node.Name.Trim().ToLowerInvariant();
            if (n == "users" || n == "kullanıcılar") return (Color)ColorConverter.ConvertFromString("#2563EB");
            if (n == "windows") return (Color)ColorConverter.ConvertFromString("#16A34A");
            if (n == "program files" || n == "program files (x86)") return (Color)ColorConverter.ConvertFromString("#D97706");
            if (n == "programdata") return (Color)ColorConverter.ConvertFromString("#0891B2");
            if (n == "temp") return (Color)ColorConverter.ConvertFromString("#DC2626");
            if (n == "$recycle.bin") return (Color)ColorConverter.ConvertFromString("#64748B");

            if (depth > 0)
            {
                int h = Math.Abs(node.Name.GetHashCode());
                return (Color)ColorConverter.ConvertFromString(HarmonicPalette[h % HarmonicPalette.Length]);
            }

            int hash = Math.Abs(node.Name.GetHashCode());
            return (Color)ColorConverter.ConvertFromString(HarmonicPalette[hash % HarmonicPalette.Length]);
        }
    }
}
