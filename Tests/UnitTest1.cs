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
        public void SettingsService_PersistsSettings()
        {
            var settings = SettingsService.Instance;
            Assert.NotNull(settings);
            Assert.NotNull(settings.Settings);

            // Change a setting, save, and ensure it holds
            bool prevHide = settings.Settings.HideSystemInTreemap;
            settings.Settings.HideSystemInTreemap = !prevHide;
            settings.Save();
            Assert.Equal(!prevHide, settings.Settings.HideSystemInTreemap);

            // Restore
            settings.Settings.HideSystemInTreemap = prevHide;
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
    }
}
