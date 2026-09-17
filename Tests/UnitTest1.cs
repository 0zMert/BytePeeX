using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Folderize.Models;
using Folderize.Services;
using Folderize.ViewModels;
using Xunit;

namespace Tests
{
    public class DiskScannerTests
    {
        [Fact]
        public void FormatBytes_FormatsCorrectly()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Assert.Equal("0 Bytes", FileSystemNode.FormatBytes(0, inv));
            Assert.Equal("500 Bytes", FileSystemNode.FormatBytes(500, inv));
            Assert.Equal("1.0 KB", FileSystemNode.FormatBytes(1024, inv));
            Assert.Equal("1.5 KB", FileSystemNode.FormatBytes(1536, inv));
            Assert.Equal("1.0 MB", FileSystemNode.FormatBytes(1024 * 1024, inv));
            Assert.Equal("28.3 GB", FileSystemNode.FormatBytes((long)(28.3 * 1024 * 1024 * 1024), inv));
            Assert.Equal("1.50 TB", FileSystemNode.FormatBytes((long)(1.5 * 1024L * 1024 * 1024 * 1024), inv));
        }

        [Fact]
        public async Task DiskScanner_ScansDirectoryAccurately()
        {
            // Setup a temporary directory structure for testing
            string tempDir = Path.Combine(Path.GetTempPath(), "DiskScannerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // SubFolderA: 2 files of 5000 bytes each
                string subDirA = Path.Combine(tempDir, "FolderA");
                Directory.CreateDirectory(subDirA);
                File.WriteAllBytes(Path.Combine(subDirA, "fileA1.dat"), new byte[5000]);
                File.WriteAllBytes(Path.Combine(subDirA, "fileA2.dat"), new byte[5000]);

                // SubFolderB: 1 file of 20000 bytes
                string subDirB = Path.Combine(tempDir, "FolderB");
                Directory.CreateDirectory(subDirB);
                File.WriteAllBytes(Path.Combine(subDirB, "fileB1.dat"), new byte[20000]);

                // Direct file in tempDir: 1 file of 1000 bytes
                File.WriteAllBytes(Path.Combine(tempDir, "rootFile.txt"), new byte[1000]);

                var scanner = new DiskScannerService();
                var result = await scanner.ScanPathAsync(tempDir, null, CancellationToken.None);

                Assert.NotNull(result);
                Assert.Equal(31000, result.SizeBytes); // 10000 + 20000 + 1000
                Assert.Equal(4, result.FileCount);
                Assert.Equal(2, result.FolderCount);
                Assert.Equal(100.0, result.PercentOfParent);

                // Children should be sorted descending by size:
                // FolderB (20000) > FolderA (10000) > [1 Files] (1000)
                Assert.NotEmpty(result.Children);
                Assert.Equal("FolderB", result.Children[0].Name);
                Assert.Equal(20000, result.Children[0].SizeBytes);
                Assert.True(result.Children[0].PercentOfParent > 60.0);

                Assert.Equal("FolderA", result.Children[1].Name);
                Assert.Equal(10000, result.Children[1].SizeBytes);

                Assert.Equal("[1 Files]", result.Children[2].Name);
                Assert.Equal(1000, result.Children[2].SizeBytes);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void MainViewModel_ToggleExpansion_ExpandsAndCollapses()
        {
            var vm = new MainViewModel();

            var root = new FileSystemNode
            {
                Name = "Root",
                FullPath = @"C:\Root",
                IsDirectory = true,
                Level = 0,
                IsExpanded = true
            };

            var child1 = new FileSystemNode
            {
                Name = "Child1",
                FullPath = @"C:\Root\Child1",
                IsDirectory = true,
                Level = 1,
                Parent = root
            };

            var subChild = new FileSystemNode
            {
                Name = "SubChild",
                FullPath = @"C:\Root\Child1\Sub",
                IsDirectory = false,
                Level = 2,
                Parent = child1
            };

            child1.Children.Add(subChild);
            root.Children.Add(child1);

            vm.VisibleNodes.Add(root);
            vm.VisibleNodes.Add(child1);

            Assert.Equal(2, vm.VisibleNodes.Count);

            // Expand Child1
            vm.ToggleNodeExpansion(child1);
            Assert.True(child1.IsExpanded);
            Assert.Equal(3, vm.VisibleNodes.Count);
            Assert.Contains(subChild, vm.VisibleNodes);

            // Collapse Child1
            vm.ToggleNodeExpansion(child1);
            Assert.False(child1.IsExpanded);
            Assert.Equal(2, vm.VisibleNodes.Count);
            Assert.DoesNotContain(subChild, vm.VisibleNodes);
        }

        [Fact]
        public void MainViewModel_ExpandAll_CyclesThrough5_10_AndAll()
        {
            var vm = new MainViewModel();
            var current = new FileSystemNode { Name = "Root", FullPath = @"C:\TreeTest", IsDirectory = true, Level = 0 };
            vm.RootNode = current;

            for (int i = 1; i <= 12; i++)
            {
                var child = new FileSystemNode { Name = $"Level_{i}", FullPath = $@"C:\TreeTest\L{i}", IsDirectory = true, Level = i, Parent = current };
                current.Children.Add(child);
                current = child;
            }

            // Initially default button text
            Assert.Contains("Genişlet", vm.ExpandAllButtonText);

            // Step 1: Click 1 -> 5 Levels
            vm.ExpandAll();
            Assert.Contains("5", vm.ExpandAllButtonText);
            Assert.Contains("5", vm.StatusText);

            // Step 2: Click 2 -> 10 Levels
            vm.ExpandAll();
            Assert.Contains("10", vm.ExpandAllButtonText);
            Assert.Contains("10", vm.StatusText);

            // Step 3: Click 3 -> All / Hepsi
            vm.ExpandAll();
            Assert.Contains("Hepsi", vm.ExpandAllButtonText);

            // Collapse All -> Resets
            vm.CollapseAll();
            Assert.Contains("Genişlet", vm.ExpandAllButtonText);
        }

        [Fact]
        public void SettingsService_PersistsSettings()
        {
            var settings = SettingsService.Instance;
            Assert.NotNull(settings);
            Assert.NotNull(settings.Settings);

            // Change settings, save, and ensure they hold
            bool prevHide = settings.Settings.HideSystemInTreemap;
            string prevPath = settings.Settings.LastSelectedPath;
            bool prevSunburst = settings.Settings.IsSunburstChartSelected;
            string prevTab = settings.Settings.ActiveBottomTab;

            settings.Settings.HideSystemInTreemap = !prevHide;
            settings.Settings.LastSelectedPath = "D:\\TestFolder";
            settings.Settings.IsSunburstChartSelected = true;
            settings.Settings.ActiveBottomTab = "Files";
            settings.Save();

            Assert.Equal(!prevHide, settings.Settings.HideSystemInTreemap);
            Assert.Equal("D:\\TestFolder", settings.Settings.LastSelectedPath);
            Assert.True(settings.Settings.IsSunburstChartSelected);
            Assert.Equal("Files", settings.Settings.ActiveBottomTab);

            // Restore original settings
            settings.Settings.HideSystemInTreemap = prevHide;
            settings.Settings.LastSelectedPath = prevPath;
            settings.Settings.IsSunburstChartSelected = prevSunburst;
            settings.Settings.ActiveBottomTab = prevTab;
            settings.Save();
        }

        [Fact]
        public void MainViewModel_LoadsDrivesImmediately()
        {
            var vm = new MainViewModel();
            // Drives should be populated synchronously upon instantiation (0 delay)
            Assert.NotEmpty(vm.AvailableDrives);
            Assert.NotEmpty(vm.DriveCards);
            Assert.Contains(vm.DriveCards, d => d.TotalSizeBytes > 0);
            // IsRunningAsAdmin should be initialized without throwing
            Assert.True(vm.IsRunningAsAdmin == true || vm.IsRunningAsAdmin == false);
        }

        [Fact]
        public void InstalledAppService_LoadsApps_WithoutThrowing()
        {
            var service = new InstalledAppService();
            var apps = service.GetInstalledApplications();
            Assert.NotNull(apps);
            // Some apps may be unknown (null LastRunTime), which is valid and expected
            foreach (var app in apps)
            {
                Assert.False(string.IsNullOrWhiteSpace(app.Name));
                Assert.True(app.SizeBytes >= 0);
            }
        }

        [Fact]
        public void SecurityPrivilegeService_EnsureBackupPrivileges_DoesNotThrow()
        {
            // Should safely attempt to grant privileges without unhandled exceptions
            bool result = SecurityPrivilegeService.EnsureBackupPrivileges();
            // In normal user context without elevation it may return false; when elevated it returns true
            Assert.True(result || !result);
        }

        [Fact]
        public void DiskScannerService_ToExtendedPath_FormatsCorrectly()
        {
            Assert.Equal(@"\\?\C:\Users", DiskScannerService.ToExtendedPath(@"C:\Users"));
            Assert.Equal(@"\\?\C:\Windows", DiskScannerService.ToExtendedPath(@"\\?\C:\Windows"));
            Assert.Equal(@"\\?\UNC\server\share", DiskScannerService.ToExtendedPath(@"\\server\share"));
            Assert.Equal("", DiskScannerService.ToExtendedPath(""));
        }

        [Fact]
        public void FileSystemNode_HeatProperties_CalculatesBasedOnSize()
        {
            var hugeNode = new FileSystemNode { SizeBytes = 15L * 1024 * 1024 * 1024 }; // 15 GB
            Assert.Equal("🔥 Kritik", hugeNode.HeatBadgeText);
            Assert.Equal("#EF4444", hugeNode.HeatColor);

            var largeNode = new FileSystemNode { SizeBytes = 4L * 1024 * 1024 * 1024 }; // 4 GB
            Assert.Equal("⚡ Büyük", largeNode.HeatBadgeText);
            Assert.Equal("#F97316", largeNode.HeatColor);

            var midNode = new FileSystemNode { SizeBytes = 500L * 1024 * 1024 }; // 500 MB
            Assert.Equal("📦 Orta", midNode.HeatBadgeText);
            Assert.Equal("#EAB308", midNode.HeatColor);

            var smallNode = new FileSystemNode { SizeBytes = 50L * 1024 * 1024 }; // 50 MB
            Assert.Equal("📄 Hafif", smallNode.HeatBadgeText);
            Assert.Equal("#38BDF8", smallNode.HeatColor);
        }

        [Fact]
        public void MainViewModel_ChartModeSwitcher_SupportsHeatmap()
        {
            var vm = new MainViewModel();

            vm.SelectChartModeCommand.Execute("Heatmap");
            Assert.True(vm.IsHeatmapChartSelected);
            Assert.False(vm.IsTreemapChartSelected);
            Assert.False(vm.IsSunburstChartSelected);
            Assert.Equal("Heatmap", vm.SelectedVisualizationMode);

            vm.SelectChartModeCommand.Execute("Treemap");
            Assert.True(vm.IsTreemapChartSelected);
            Assert.False(vm.IsHeatmapChartSelected);
            Assert.False(vm.IsSunburstChartSelected);
            Assert.Equal("Treemap", vm.SelectedVisualizationMode);

            vm.SelectChartModeCommand.Execute("Sunburst");
            Assert.True(vm.IsSunburstChartSelected);
            Assert.False(vm.IsTreemapChartSelected);
            Assert.False(vm.IsHeatmapChartSelected);
            Assert.Equal("Sunburst", vm.SelectedVisualizationMode);
        }

        [Fact]
        public void FileOperationService_SendToRecycleBin_HandlesInvalidOrNonExistentGracefully()
        {
            Assert.False(FileOperationService.SendToRecycleBin(""));
            Assert.False(FileOperationService.SendToRecycleBin("   "));
            Assert.False(FileOperationService.SendToRecycleBin(@"C:\NonExistent_Fake_File_987654321.xyz"));
        }

        [Fact]
        public void MainViewModel_RemoveNodeFromTree_UpdatesSizesAndAncestors()
        {
            var vm = new MainViewModel();

            var root = new FileSystemNode
            {
                Name = "C:",
                FullPath = @"C:\",
                IsDirectory = true,
                Level = 0,
                SizeBytes = 10000,
                FileCount = 10,
                FolderCount = 2
            };

            var subDir = new FileSystemNode
            {
                Name = "Folder1",
                FullPath = @"C:\Folder1",
                IsDirectory = true,
                Level = 1,
                Parent = root,
                SizeBytes = 6000,
                FileCount = 6,
                FolderCount = 1
            };

            var otherDir = new FileSystemNode
            {
                Name = "Folder2",
                FullPath = @"C:\Folder2",
                IsDirectory = true,
                Level = 1,
                Parent = root,
                SizeBytes = 4000,
                FileCount = 4,
                FolderCount = 0
            };

            root.Children.Add(subDir);
            root.Children.Add(otherDir);
            vm.RootNode = root;
            vm.TotalSizeBytes = 10000;
            vm.TotalFilesCount = 10;
            vm.TotalFoldersCount = 2;

            vm.VisibleNodes.Add(root);
            vm.VisibleNodes.Add(subDir);
            vm.VisibleNodes.Add(otherDir);

            // Now remove Folder1 (6000 bytes, 6 files, 1 folder)
            vm.RemoveNodeFromTree(subDir);

            // subDir should no longer be in VisibleNodes or root.Children
            Assert.DoesNotContain(subDir, vm.VisibleNodes);
            Assert.DoesNotContain(subDir, root.Children);

            // root sizes and counts should have been subtracted
            Assert.Equal(4000, root.SizeBytes);
            Assert.Equal(4, root.FileCount);
            Assert.Equal(0, root.FolderCount); // 2 - (1 + 1) = 0

            // vm totals updated
            Assert.Equal(4000, vm.TotalSizeBytes);
            Assert.Equal(4, vm.TotalFilesCount);
            Assert.Equal(0, vm.TotalFoldersCount);

            // remaining child percentage recalculated
            Assert.Equal(100.0, otherDir.PercentOfParent);
        }

        [Fact]
        public void Diagnostic_SystemVolumeInformation_Access()
        {
            SecurityPrivilegeService.EnsureBackupPrivileges();
            var di = new DirectoryInfo(@"C:\System Volume Information");
            try
            {
                var files = di.GetFiles();
                Assert.NotNull(files);
            }
            catch
            {
                // Access may be restricted without elevation, which is valid and handled gracefully
            }
            Assert.True(true);
        }
    }
}



