using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Folderize.Models;
using Microsoft.Win32;

namespace Folderize.Views
{
    /// <summary>
    /// Interaction logic for FolderPickerWindow.xaml
    /// </summary>
    public partial class FolderPickerWindow : Window
    {
        public string? SelectedPath { get; private set; }

        public FolderPickerWindow(IEnumerable<DriveCardModel> drives, string? currentPath = null)
        {
            InitializeComponent();
            DrivesItemsControl.ItemsSource = drives;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void DriveCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string driveName && !string.IsNullOrWhiteSpace(driveName))
            {
                string path = driveName.TrimEnd('\\') + "\\";
                SelectedPath = path;
                DialogResult = true;
                Close();
            }
        }

        private void QuickFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is string tag)
            {
                string targetPath = tag switch
                {
                    "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "Downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                    "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "UserProfile" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    _ => ""
                };

                if (!string.IsNullOrEmpty(targetPath) && Directory.Exists(targetPath))
                {
                    SelectedPath = targetPath;
                    DialogResult = true;
                    Close();
                }
            }
        }

        private void BrowseSystemFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Taranacak Klasör veya Sürücüyü Seçin",
                InitialDirectory = "shell:MyComputerFolder"
            };

            if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                SelectedPath = dialog.FolderName;
                DialogResult = true;
                Close();
            }
        }
    }
}
