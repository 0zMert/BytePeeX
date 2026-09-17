using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Folderize.Models;
using Folderize.ViewModels;

namespace Folderize
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        private const uint WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (hwnd != IntPtr.Zero && !string.IsNullOrEmpty(exePath))
                {
                    if (ExtractIconEx(exePath, 0, out IntPtr hLarge, out IntPtr hSmall, 1) > 0)
                    {
                        if (hSmall != IntPtr.Zero) SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, hSmall);
                        if (hLarge != IntPtr.Zero) SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, hLarge);
                    }
                }
            }
            catch
            {
            }
        }

        private async void PathTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await _viewModel.StartScanAsync();
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedNode != null)
            {
                var node = _viewModel.SelectedNode;
                if (node.HasChildren)
                {
                    _viewModel.ToggleNodeExpansion(node);
                }
                else if (!node.IsDirectory && !node.IsSummaryFilesNode && File.Exists(node.FullPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(node.FullPath) { UseShellExecute = true });
                    }
                    catch
                    {
                        // File type might not have an association
                    }
                }
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Tag is string tag)
            {
                _viewModel.SortBy(tag);
            }
        }
    }
}