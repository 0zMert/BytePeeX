using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Folderize.Models;
using Folderize.ViewModels;

namespace Folderize.Views
{
    public partial class StorageHeatmapControl : UserControl
    {
        public StorageHeatmapControl()
        {
            InitializeComponent();
            DataContextChanged += StorageHeatmapControl_DataContextChanged;
        }

        private void StorageHeatmapControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }

            if (e.NewValue is MainViewModel newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
                RefreshCards();
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentDrillDownNode) ||
                e.PropertyName == nameof(MainViewModel.TreeStructureVersion) ||
                e.PropertyName == nameof(MainViewModel.SelectedVisualizationMode) ||
                e.PropertyName == nameof(MainViewModel.VisibleNodes))
            {
                RefreshCards();
            }
        }

        private void RefreshCards()
        {
            if (DataContext is not MainViewModel vm || vm.CurrentDrillDownNode == null)
            {
                ItemsList.ItemsSource = null;
                return;
            }

            var target = vm.CurrentDrillDownNode;
            if (target.Children == null || target.Children.Count == 0)
            {
                ItemsList.ItemsSource = null;
                return;
            }

            // Exclude empty nodes and order by size descending (Heat priority)
            var sorted = target.Children
                .Where(c => c.SizeBytes > 0)
                .OrderByDescending(c => c.SizeBytes)
                .ToList();

            ItemsList.ItemsSource = sorted;
        }

        private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FileSystemNode node)
            {
                if (node.IsDirectory && DataContext is MainViewModel vm)
                {
                    vm.SetDrillDownNode(node);
                }
            }
        }
    }
}
