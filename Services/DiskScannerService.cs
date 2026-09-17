using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using BytePeeX.Models;

namespace BytePeeX.Services
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        private const int FIND_FIRST_EX_LARGE_FETCH = 2;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindFirstFileExW(
            string lpFileName,
            int fInfoLevelId,
            out WIN32_FIND_DATA lpFindFileData,
            int fSearchOp,
            IntPtr lpSearchFilter,
            int dwAdditionalFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindNextFileW(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", EntryPoint = "GetCompressedFileSizeW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetCompressedFileSizeW(string lpFileName, out uint lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            IntPtr hFile,
            int FileInformationClass,
            IntPtr lpFileInformation,
            uint dwBufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint FILE_LIST_DIRECTORY = 0x0001;
        private const uint FILE_SHARE_READ = 1;
        private const uint FILE_SHARE_WRITE = 2;
        private const uint FILE_SHARE_DELETE = 4;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const int FileFullDirectoryInfo = 14;

        public static string ToExtendedPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.StartsWith(@"\\?\")) return path;
            if (path.StartsWith(@"\\")) return @"\\?\UNC\" + path.Substring(2);
            return @"\\?\" + path;
        }

        public static long GetAllocatedSize(string filePath, long fallbackLength, uint fileAttributes = 0)
        {
            // If attributes are known and file is neither compressed (0x800) nor sparse (0x200),
            // NTFS allocated size is standard 4KB cluster rounding. Zero syscalls!
            if (fileAttributes != 0 && (fileAttributes & 0xA00) == 0)
            {
                return CalculateAllocatedBytes(fallbackLength);
            }

            try
            {
                string p = ToExtendedPath(filePath);
                uint high;
                uint low = GetCompressedFileSizeW(p, out high);
                if (low == 0xFFFFFFFF && Marshal.GetLastWin32Error() != 0)
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

        private static DateTime? FileTimeToDateTime(FILETIME ft)
        {
            long high = (long)ft.dwHighDateTime << 32;
            long low = (uint)ft.dwLowDateTime;
            long fileTime = high | low;
            if (fileTime == 0) return null;
            try
            {
                return DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
            }
            catch
            {
                return null;
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

            // Ensure Windows SeBackupPrivilege / SeRestorePrivilege are active to bypass DACL restrictions
            SecurityPrivilegeService.EnsureBackupPrivileges();

            return await Task.Run(() =>
            {
                var progressData = new ScanProgress();
                var stopwatch = Stopwatch.StartNew();
                long lastReportTime = 0;

                var rootDirInfo = new DirectoryInfo(rootPath);

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

                // Discover top-level directories and direct files using fast single-pass Win32 enumeration
                var topDirs = new List<(string Name, string FullPath, DateTime? LastModified)>();
                var directRootFiles = new List<FileSystemNode>();
                long directRootSize = 0;
                long directRootAllocated = 0;

                EnumerateDirectoryFast(
                    rootDirInfo.FullName,
                    out var initialFiles,
                    out var initialDirs);

                foreach (var d in initialDirs)
                {
                    var child = new FileSystemNode
                    {
                        Name = d.Name,
                        FullPath = d.FullPath,
                        IsDirectory = true,
                        Level = 1,
                        Parent = rootNode,
                        LastModified = d.LastModified
                    };
                    rootNode.Children.Add(child);
                    topDirs.Add((d.Name, d.FullPath, d.LastModified));
                }

                foreach (var f in initialFiles)
                {
                    directRootSize += f.Size;
                    directRootAllocated += f.Allocated;
                    progressData.TotalFiles++;
                    progressData.TotalBytes += f.Size;

                    directRootFiles.Add(new FileSystemNode
                    {
                        Name = f.Name,
                        FullPath = f.FullPath,
                        IsDirectory = false,
                        SizeBytes = f.Size,
                        AllocatedBytes = f.Allocated,
                        FileCount = 1,
                        Level = 1,
                        Parent = rootNode,
                        LastModified = f.LastModified
                    });
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
                    rootNode.FileCount += directRootFiles.Count;
                }

                // Initial report so UI displays the drive and top folders right away
                progressData.RootNode = rootNode;
                progressData.CurrentPath = rootDirInfo.FullName;
                progressData.IsLiveUpdate = true;
                progressData.TotalBytes = rootNode.SizeBytes;
                progressData.TotalFiles = rootNode.FileCount;
                progressData.TotalFolders = rootNode.Children.Count(c => c.IsDirectory);
                progress?.Report(progressData);

                void ReportProgress(string currentPath)
                {
                    if (progress == null) return;
                    long elapsed = stopwatch.ElapsedMilliseconds;
                    if (elapsed - lastReportTime > 80) // ~12 FPS smooth live update
                    {
                        lastReportTime = elapsed;
                        progressData.CurrentPath = currentPath;
                        progressData.IsLiveUpdate = true;
                        progress.Report(progressData);
                    }
                }

                // Scan each top-level directory and stream live sizes continuously
                for (int i = 0; i < topDirs.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var topDir = topDirs[i];
                    var childNode = rootNode.Children.FirstOrDefault(c => c.FullPath.Equals(topDir.FullPath, StringComparison.OrdinalIgnoreCase));
                    if (childNode == null) continue;

                    ScanDirectoryFast(
                        topDir.FullPath,
                        topDir.Name,
                        1,
                        rootNode,
                        childNode,
                        rootNode,
                        progressData,
                        ReportProgress,
                        cancellationToken);

                    childNode.SizeBytes = childNode.RawSizeBytes;
                    rootNode.FileCount += childNode.FileCount;
                    rootNode.FolderCount += 1 + childNode.FolderCount;

                    ReportProgress(topDir.FullPath);
                }

                rootNode.SizeBytes = rootNode.RawSizeBytes;
                FinalizeNodeHierarchy(rootNode, rootNode.SizeBytes);

                if (progress != null)
                {
                    progressData.CurrentPath = cancellationToken.IsCancellationRequested ? "Durduruldu" : "Tamamlandı";
                    progressData.RootNode = rootNode;
                    progressData.IsLiveUpdate = false;
                    progress.Report(progressData);
                }

                return rootNode;
            }, cancellationToken);
        }

        private static void EnumerateDirectoryFast(
            string dirPath,
            out List<(string Name, string FullPath, long Size, long Allocated, DateTime? LastModified)> files,
            out List<(string Name, string FullPath, DateTime? LastModified)> dirs)
        {
            files = new List<(string, string, long, long, DateTime?)>();
            dirs = new List<(string, string, DateTime?)>();

            string searchPattern = ToExtendedPath(dirPath).TrimEnd('\\') + @"\*";
            IntPtr hFind = FindFirstFileExW(
                searchPattern,
                1, // FindExInfoBasic (avoids querying 8.3 short name, faster)
                out WIN32_FIND_DATA findData,
                0, // FindExSearchNameMatch
                IntPtr.Zero,
                FIND_FIRST_EX_LARGE_FETCH);

            if (hFind != INVALID_HANDLE_VALUE)
            {
                try
                {
                    do
                    {
                        string name = findData.cFileName;
                        if (name == "." || name == "..") continue;

                        uint attrs = findData.dwFileAttributes;
                        string childFullPath = Path.Combine(dirPath, name);
                        DateTime? lastWrite = FileTimeToDateTime(findData.ftLastWriteTime);

                        if ((attrs & FILE_ATTRIBUTE_DIRECTORY) != 0)
                        {
                            // Skip reparse points (symlinks / junctions) to prevent duplicate counting and circular loops
                            if ((attrs & FILE_ATTRIBUTE_REPARSE_POINT) == 0)
                            {
                                dirs.Add((name, childFullPath, lastWrite));
                            }
                        }
                        else
                        {
                            long size = ((long)findData.nFileSizeHigh << 32) | (long)findData.nFileSizeLow;
                            long allocated = ((attrs & 0xA00) != 0)
                                ? GetAllocatedSize(childFullPath, size, attrs)
                                : CalculateAllocatedBytes(size);

                            files.Add((name, childFullPath, size, allocated, lastWrite));
                        }
                    }
                    while (FindNextFileW(hFind, out findData));
                }
                finally
                {
                    FindClose(hFind);
                }
            }
            else
            {
                // Fallback 1: Use Win32 CreateFileW with FILE_FLAG_BACKUP_SEMANTICS (bypasses DACLs for System Volume Information, other user profiles, etc.)
                bool backupReadOk = false;
                try
                {
                    backupReadOk = EnumerateDirectoryBackupSemantics(dirPath, files, dirs);
                }
                catch
                {
                }

                if (!backupReadOk)
                {
                    // Fallback 2: Standard .NET EnumerateFileSystemInfos
                    try
                    {
                        var di = new DirectoryInfo(dirPath);
                        foreach (var fsi in di.EnumerateFileSystemInfos())
                        {
                            if (fsi is DirectoryInfo sub)
                            {
                                if ((sub.Attributes & FileAttributes.ReparsePoint) == 0)
                                {
                                    dirs.Add((sub.Name, sub.FullName, TryGetLastWriteTime(sub)));
                                }
                            }
                            else if (fsi is FileInfo fi)
                            {
                                long length = 0;
                                try { length = fi.Length; } catch { }
                                long allocated = CalculateAllocatedBytes(length);
                                files.Add((fi.Name, fi.FullName, length, allocated, TryGetLastWriteTime(fi)));
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static bool EnumerateDirectoryBackupSemantics(
            string dirPath,
            List<(string Name, string FullPath, long Size, long Allocated, DateTime? LastModified)> files,
            List<(string Name, string FullPath, DateTime? LastModified)> dirs)
        {
            string extended = ToExtendedPath(dirPath).TrimEnd('\\');
            IntPtr hDir = CreateFileW(
                extended,
                FILE_LIST_DIRECTORY,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (hDir == INVALID_HANDLE_VALUE)
            {
                // Try with GENERIC_READ fallback
                hDir = CreateFileW(
                    extended,
                    0x80000000 /* GENERIC_READ */,
                    FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_FLAG_BACKUP_SEMANTICS,
                    IntPtr.Zero);
            }

            if (hDir == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            const int bufferSize = 65536; // 64 KB buffer
            IntPtr pBuffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                bool foundAny = false;
                while (GetFileInformationByHandleEx(hDir, FileFullDirectoryInfo, pBuffer, (uint)bufferSize))
                {
                    foundAny = true;
                    IntPtr current = pBuffer;
                    while (true)
                    {
                        uint nextOffset = (uint)Marshal.ReadInt32(current, 0);
                        long lastWriteTime = Marshal.ReadInt64(current, 24);
                        long endOfFile = Marshal.ReadInt64(current, 40);
                        long allocationSize = Marshal.ReadInt64(current, 48);
                        uint attributes = (uint)Marshal.ReadInt32(current, 56);
                        uint fileNameLength = (uint)Marshal.ReadInt32(current, 60);

                        string? name = Marshal.PtrToStringUni(IntPtr.Add(current, 68), (int)(fileNameLength / 2));

                        if (!string.IsNullOrEmpty(name) && name != "." && name != "..")
                        {
                            string childFullPath = Path.Combine(dirPath, name);
                            DateTime? lastWrite = lastWriteTime > 0 ? DateTime.FromFileTimeUtc(lastWriteTime).ToLocalTime() : null;

                            if ((attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                            {
                                if ((attributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0)
                                {
                                    dirs.Add((name, childFullPath, lastWrite));
                                }
                            }
                            else
                            {
                                long size = endOfFile;
                                long allocated = allocationSize > 0 ? allocationSize : CalculateAllocatedBytes(size);
                                files.Add((name, childFullPath, size, allocated, lastWrite));
                            }
                        }

                        if (nextOffset == 0) break;
                        current = IntPtr.Add(current, (int)nextOffset);
                    }
                }
                return foundAny;
            }
            finally
            {
                Marshal.FreeHGlobal(pBuffer);
                CloseHandle(hDir);
            }
        }

        private FileSystemNode? ScanDirectoryFast(
            string dirPath,
            string dirName,
            int level,
            FileSystemNode? parent,
            FileSystemNode currentTopNode,
            FileSystemNode rootNode,
            ScanProgress progressData,
            Action<string> reportProgress,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var node = (level == 1 && parent == rootNode)
                ? currentTopNode
                : new FileSystemNode
                {
                    Name = dirName,
                    FullPath = dirPath,
                    IsDirectory = true,
                    Level = level,
                    Parent = parent
                };

            reportProgress(dirPath);

            long directFilesSize = 0;
            long directFilesAllocated = 0;
            var directFileNodes = new List<FileSystemNode>();
            DateTime? maxFileModified = null;

            EnumerateDirectoryFast(dirPath, out var files, out var subDirs);

            if (files.Count == 0 && subDirs.Count == 0)
            {
                // Check if folder was inaccessible
                // Still keep node with 0 bytes
            }

            // 1. Process files
            for (int i = 0; i < files.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var f = files[i];
                directFilesSize += f.Size;
                directFilesAllocated += f.Allocated;

                progressData.TotalFiles++;
                progressData.TotalBytes += f.Size;

                // Live dynamic update: immediately feed into the top-level folder and root node!
                currentTopNode.RawSizeBytes += f.Size;
                currentTopNode.AllocatedBytes += f.Allocated;
                rootNode.RawSizeBytes += f.Size;
                rootNode.AllocatedBytes += f.Allocated;

                if (f.LastModified.HasValue && (maxFileModified == null || f.LastModified > maxFileModified))
                {
                    maxFileModified = f.LastModified;
                }

                directFileNodes.Add(new FileSystemNode
                {
                    Name = f.Name,
                    FullPath = f.FullPath,
                    IsDirectory = false,
                    SizeBytes = f.Size,
                    AllocatedBytes = f.Allocated,
                    FileCount = 1,
                    FolderCount = 0,
                    Level = level + 2,
                    LastModified = f.LastModified
                });
            }

            // 2. Process subdirectories recursively
            long subFoldersSize = 0;
            long subFoldersAllocated = 0;
            int totalSubFiles = directFileNodes.Count;
            int totalSubFolders = 0;
            var subFolderNodes = new List<FileSystemNode>();

            for (int i = 0; i < subDirs.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var d = subDirs[i];
                progressData.TotalFolders++;

                var childSubNode = ScanDirectoryFast(
                    d.FullPath,
                    d.Name,
                    level + 1,
                    node,
                    currentTopNode,
                    rootNode,
                    progressData,
                    reportProgress,
                    cancellationToken);

                if (childSubNode != null)
                {
                    subFolderNodes.Add(childSubNode);
                    subFoldersSize += childSubNode.SizeBytes;
                    subFoldersAllocated += childSubNode.AllocatedBytes;
                    totalSubFiles += childSubNode.FileCount;
                    totalSubFolders += 1 + childSubNode.FolderCount;
                }
            }

            node.SizeBytes = directFilesSize + subFoldersSize;
            node.RawSizeBytes = node.SizeBytes;
            node.AllocatedBytes = directFilesAllocated + subFoldersAllocated;
            node.FileCount = totalSubFiles;
            node.FolderCount = totalSubFolders;
            node.LastModified = maxFileModified;

            var allChildren = new List<FileSystemNode>();

            if (directFileNodes.Count > 0)
            {
                var summaryFilesNode = new FileSystemNode
                {
                    Name = $"[{directFileNodes.Count} Files]",
                    FullPath = dirPath,
                    IsDirectory = false,
                    IsSummaryFilesNode = true,
                    SizeBytes = directFilesSize,
                    AllocatedBytes = directFilesAllocated,
                    FileCount = directFileNodes.Count,
                    FolderCount = 0,
                    Level = level + 1,
                    Parent = node,
                    LastModified = maxFileModified
                };

                foreach (var fn in directFileNodes)
                {
                    fn.Parent = summaryFilesNode;
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

