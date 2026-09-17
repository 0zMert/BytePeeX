using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Folderize.Models;
using Folderize.ViewModels;

namespace Folderize.Views
{
    public partial class FluentExplorerControl : UserControl
    {
        public FluentExplorerControl()
        {
            InitializeComponent();
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.SelectedNode != null)
            {
                var node = vm.SelectedNode;
                if (node.IsDirectory)
                {
                    vm.DrillDownCommand.Execute(node);
                }
                else if (File.Exists(node.FullPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(node.FullPath) { UseShellExecute = true });
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && e.OriginalSource is GridViewColumnHeader header && header.Tag is string tag)
            {
                vm.SortBy(tag);
            }
        }
    }
}
