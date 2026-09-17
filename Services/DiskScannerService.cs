using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Folderize.Models;

namespace Folderize.Services
{
    public class ScanProgress
    {
        public string CurrentPath { get; set; } = string.Empty;
        public long TotalFiles { get; set; }
        public long TotalFolders { get; set; }
        public long TotalBytes { get; set; }
        public int InaccessibleFoldersCount { get; set; }
        public FileSystemNode? RootNode { get; set; }
        public bool IsLiveUpdate { get; set; }
    }

    public class DiskScannerService
    {
        private const int ClusterSize = 4096; // Standard NTFS 4KB cluster

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "GetCompressedFileSizeW", ExactSpelling = true, SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern uint GetCompressedFileSizeW(string lpFileName, out uint lpFileSizeHigh);

        public static long GetAllocatedSize(string filePath, long fallbackLength)
        {
            try
            {
                uint high;
                uint low = GetCompressedFileSizeW(filePath, out high);
                if (low == 0xFFFFFFFF && System.Runtime.InteropServices.Marshal.GetLastWin32Error() != 0)
                {
                    return CalculateAllocatedBytes(fallbackLength);
                }
                return ((long)high << 32) + low;
            }
            catch
            {
                return CalculateAllocatedBytes(fallbackLength);
            }
        }

        public async Task<FileSystemNode?> ScanPathAsync(
            string rootPath,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return null;
            }

            return await Task.Run(() =>
            {
                var progressData = new ScanProgress();
                var stopwatch = Stopwatch.StartNew();
                long lastReportTime = 0;
                var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var rootDirInfo = new DirectoryInfo(rootPath);
                visitedPaths.Add(rootDirInfo.FullName.TrimEnd('\\'));

                var rootNode = new FileSystemNode
                {
                    Name = string.IsNullOrEmpty(rootDirInfo.Parent?.FullName) ? rootDirInfo.FullName : rootDirInfo.Name,
                    FullPath = rootDirInfo.FullName,
                    IsDirectory = true,
                    Level = 0,
                    Parent = null,
                    IsExpanded = true,
                    LastModified = TryGetLastWriteTime(rootDirInfo)
                };

                // Discover top-level directories first
                var topDirs = new List<DirectoryInfo>();
                try
                {
                    foreach (var sub in rootDirInfo.EnumerateDirectories())
                    {
                        var child = new FileSystemNode
                        {
                            Name = sub.Name,
                            FullPath = sub.FullName,
                            IsDirectory = true,
                            Level = 1,
                            Parent = rootNode,
                            LastModified = TryGetLastWriteTime(sub)
                        };
                        rootNode.Children.Add(child);
                        topDirs.Add(sub);
                    }
                }
                catch
                {
                }

                // Initial report so UI displays the drive/folders immediately
                progressData.RootNode = rootNode;
                progressData.CurrentPath = rootDirInfo.FullName;
                progressData.IsLiveUpdate = true;
                progress?.Report(progressData);

                void ReportProgress(string currentPath)
                {
                    if (progress == null) return;
                    long elapsed = stopwatch.ElapsedMilliseconds;
                    if (elapsed - lastReportTime > 90) // ~11 FPS smooth update rate
                    {
                        lastReportTime = elapsed;
                        progressData.CurrentPath = currentPath;
                        progressData.TotalBytes = rootNode.SizeBytes;
                        progressData.TotalFiles = rootNode.FileCount;
                        progressData.TotalFolders = rootNode.FolderCount;
                        progressData.IsLiveUpdate = true;
                        progress.Report(progressData);
                    }
                }

                // 1. Scan direct root files (e.g. pagefile.sys, hiberfil.sys, etc.)
                var directRootFiles = new List<FileSystemNode>();
                long directRootSize = 0;
                long directRootAllocated = 0;
                try
                {
                    foreach (var file in rootDirInfo.EnumerateFiles())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        long length = 0;
                        DateTime? lastWrite = null;
                        try { length = file.Length; lastWrite = file.LastWriteTime; } catch { }

                        long allocated = GetAllocatedSize(file.FullName, length);
                        directRootSize += length;
                        directRootAllocated += allocated;
                        rootNode.FileCount++;
                        progressData.TotalFiles++;
                        progressData.TotalBytes += length;

                        directRootFiles.Add(new FileSystemNode
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            IsDirectory = false,
                            SizeBytes = length,
                            AllocatedBytes = allocated,
                            FileCount = 1,
                            Level = 1,
                            Parent = rootNode,
                            LastModified = lastWrite
                        });
                    }
                }
                catch
                {
                }

                if (directRootFiles.Count > 0)
                {
                    var rootFilesNode = new FileSystemNode
                    {
                        Name = $"[{directRootFiles.Count} Files]",
                        FullPath = rootDirInfo.FullName,
                        IsDirectory = false,
                        IsSummaryFilesNode = true,
                        SizeBytes = directRootSize,
                        AllocatedBytes = directRootAllocated,
                        FileCount = directRootFiles.Count,
                        Level = 1,
                        Parent = rootNode
                    };
                    directRootFiles.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                    rootFilesNode.Children.AddRange(directRootFiles);
                    rootNode.Children.Insert(0, rootFilesNode);

                    rootNode.SizeBytes += directRootSize;
                    rootNode.AllocatedBytes += directRootAllocated;
                }

                // 2. Scan each top-level directory sequentially and accumulate live sizes!
                for (int i = 0; i < topDirs.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var subDir = topDirs[i];
                    string pathNorm = subDir.FullName.TrimEnd('\\');
                    if (visitedPaths.Contains(pathNorm)) continue;
                    visitedPaths.Add(pathNorm);

                    // Find matching child node
                    var childNode = rootNode.Children.FirstOrDefault(c => c.FullPath.Equals(subDir.FullName, StringComparison.OrdinalIgnoreCase));
                    if (childNode == null) continue;

                    var scannedChild = ScanDirectoryRecursive(subDir, 1, rootNode, progressData, visitedPaths, ReportProgress, cancellationToken);
                    if (scannedChild != null)
                    {
                        childNode.SizeBytes = scannedChild.SizeBytes;
                        childNode.AllocatedBytes = scannedChild.AllocatedBytes;
                        childNode.FileCount = scannedChild.FileCount;
                        childNode.FolderCount = scannedChild.FolderCount;
                        childNode.Children = scannedChild.Children;

                        rootNode.SizeBytes += scannedChild.SizeBytes;
                        rootNode.AllocatedBytes += scannedChild.AllocatedBytes;
                        rootNode.FileCount += scannedChild.FileCount;
                        rootNode.FolderCount += 1 + scannedChild.FolderCount;

                        // Live calculate % for scanned children
                        if (rootNode.SizeBytes > 0)
                        {
                            foreach (var c in rootNode.Children)
                            {
                                c.PercentOfParent = Math.Min(100.0, (double)c.SizeBytes / rootNode.SizeBytes * 100.0);
                            }
                        }
                    }

                    ReportProgress(subDir.FullName);
                }

                FinalizeNodeHierarchy(rootNode, rootNode.SizeBytes);

                if (progress != null)
                {
                    progressData.CurrentPath = "Tamamlandı";
                    progressData.RootNode = rootNode;
                    progressData.TotalBytes = rootNode.SizeBytes;
                    progressData.TotalFiles = rootNode.FileCount;
                    progressData.TotalFolders = rootNode.FolderCount;
                    progressData.IsLiveUpdate = false;
                    progress.Report(progressData);
                }

                return rootNode;
            }, cancellationToken);
        }

        private FileSystemNode? ScanDirectoryRecursive(
            DirectoryInfo dirInfo,
            int level,
            FileSystemNode? parent,
            ScanProgress progressData,
            HashSet<string> visitedPaths,
            Action<string> reportProgress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var node = new FileSystemNode
            {
                Name = string.IsNullOrEmpty(dirInfo.Parent?.FullName) ? dirInfo.FullName : dirInfo.Name,
                FullPath = dirInfo.FullName,
                IsDirectory = true,
                Level = level,
                Parent = parent,
                LastModified = TryGetLastWriteTime(dirInfo)
            };

            reportProgress(dirInfo.FullName);

            long directFilesSize = 0;
            long directFilesAllocated = 0;
            var directFileNodes = new List<FileSystemNode>();

            // 1. Enumerate Files
            try
            {
                var files = dirInfo.EnumerateFiles();
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    long length = 0;
                    DateTime? lastWrite = null;
                    try
                    {
                        length = file.Length;
                        lastWrite = file.LastWriteTime;
                    }
                    catch
                    {
                    }

                    long allocated = GetAllocatedSize(file.FullName, length);
                    directFilesSize += length;
                    directFilesAllocated += allocated;

                    progressData.TotalFiles++;
                    progressData.TotalBytes += length;

                    directFileNodes.Add(new FileSystemNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        SizeBytes = length,
                        AllocatedBytes = allocated,
                        FileCount = 1,
                        FolderCount = 0,
                        Level = level + 1,
                        Parent = node,
                        LastModified = lastWrite
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                progressData.InaccessibleFoldersCount++;
            }
            catch (Exception)
            {
            }

            long totalChildrenSize = 0;
            long totalChildrenAllocated = 0;
            int totalSubFiles = directFileNodes.Count;
            int totalSubFolders = 0;

            var subFolderNodes = new List<FileSystemNode>();

            // 2. Enumerate Subdirectories
            try
            {
                var subDirs = dirInfo.EnumerateDirectories();
                foreach (var subDir in subDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Check for ReparsePoints (symlinks / junctions)
                    try
                    {
                        if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            var target = subDir.ResolveLinkTarget(false);
                            if (target != null)
                            {
                                string targetNorm = target.FullName.TrimEnd('\\');
                                if (visitedPaths.Contains(targetNorm))
                                {
                                    continue;
                                }
                                visitedPaths.Add(targetNorm);
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    string pathNorm = subDir.FullName.TrimEnd('\\');
                    if (visitedPaths.Contains(pathNorm))
                    {
                        continue;
                    }
                    visitedPaths.Add(pathNorm);

                    progressData.TotalFolders++;
                    var childNode = ScanDirectoryRecursive(subDir, level + 1, node, progressData, visitedPaths, reportProgress, cancellationToken);
                    if (childNode != null)
                    {
                        subFolderNodes.Add(childNode);
                        totalChildrenSize += childNode.SizeBytes;
                        totalChildrenAllocated += childNode.AllocatedBytes;
                        totalSubFiles += childNode.FileCount;
                        totalSubFolders += 1 + childNode.FolderCount;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                progressData.InaccessibleFoldersCount++;
            }
            catch (Exception)
            {
            }

            node.SizeBytes = directFilesSize + totalChildrenSize;
            node.AllocatedBytes = directFilesAllocated + totalChildrenAllocated;
            node.FileCount = totalSubFiles;
            node.FolderCount = totalSubFolders;

            var allChildren = new List<FileSystemNode>();

            if (directFileNodes.Count > 0)
            {
                var summaryFilesNode = new FileSystemNode
                {
                    Name = $"[{directFileNodes.Count} Files]",
                    FullPath = dirInfo.FullName,
                    IsDirectory = false,
                    IsSummaryFilesNode = true,
                    SizeBytes = directFilesSize,
                    AllocatedBytes = directFilesAllocated,
                    FileCount = directFileNodes.Count,
                    FolderCount = 0,
                    Level = level + 1,
                    Parent = node,
                    LastModified = directFileNodes.Max(f => f.LastModified)
                };

                foreach (var f in directFileNodes)
                {
                    f.Level = level + 2;
                    f.Parent = summaryFilesNode;
                }
                directFileNodes.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                summaryFilesNode.Children.AddRange(directFileNodes);

                allChildren.Add(summaryFilesNode);
            }

            allChildren.AddRange(subFolderNodes);

            node.Children = allChildren;
            return node;
        }

        private static void FinalizeNodeHierarchy(FileSystemNode node, long parentSizeBytes)
        {
            if (parentSizeBytes > 0)
            {
                node.PercentOfParent = Math.Min(100.0, (double)node.SizeBytes / parentSizeBytes * 100.0);
            }
            else
            {
                node.PercentOfParent = 0.0;
            }

            node.Children.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));

            foreach (var child in node.Children)
            {
                FinalizeNodeHierarchy(child, node.SizeBytes);
            }
        }

        private static long CalculateAllocatedBytes(long length)
        {
            if (length <= 0) return 0;
            long rem = length % ClusterSize;
            return rem == 0 ? length : length + (ClusterSize - rem);
        }

        private static DateTime? TryGetLastWriteTime(FileSystemInfo info)
        {
            try
            {
                return info.LastWriteTime;
            }
            catch
            {
                return null;
            }
        }
    }
}
