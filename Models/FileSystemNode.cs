using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Folderize.Models
{
    public class FileSystemNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private long _sizeBytes;
        private long _allocatedBytes;
        private int _fileCount;
        private int _folderCount;
        private double _percentOfParent;

        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public bool IsSummaryFilesNode { get; set; }
        public DateTime? LastModified { get; set; }
        public int Level { get; set; }
        public FileSystemNode? Parent { get; set; }
        public List<FileSystemNode> Children { get; set; } = new();

        private static readonly string[] Palette = new[]
        {
            "#00C0EF", "#00A65A", "#F39C12", "#DD4B39", "#8E44AD",
            "#3C8DBC", "#0073B7", "#001F3F", "#39CCCC", "#01FF70",
            "#FF851B", "#605CA8", "#D81B60", "#111111", "#4A90E2"
        };

        public string DisplayColor
        {
            get
            {
                if (IsSummaryFilesNode) return "#4FC3F7";
                int hash = Math.Abs(Name.GetHashCode());
                return Palette[hash % Palette.Length];
            }
        }

        public bool IsSystemNode
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name)) return false;
                string n = Name.Trim().ToLowerInvariant();
                return n == "windows" || n == "system volume information" || n == "$recycle.bin" ||
                       n == "recovery" || n == "boot" || n == "perflogs" || n == "config.msi" ||
                       n == "pagefile.sys" || n == "hiberfil.sys" || n == "swapfile.sys";
            }
        }

        public long SizeBytes
        {
            get => _sizeBytes;
            set
            {
                if (_sizeBytes != value)
                {
                    _sizeBytes = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedSize));
                }
            }
        }

        public long AllocatedBytes
        {
            get => _allocatedBytes;
            set
            {
                if (_allocatedBytes != value)
                {
                    _allocatedBytes = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedAllocated));
                }
            }
        }

        public int FileCount
        {
            get => _fileCount;
            set
            {
                if (_fileCount != value)
                {
                    _fileCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedFiles));
                }
            }
        }

        public int FolderCount
        {
            get => _folderCount;
            set
            {
                if (_folderCount != value)
                {
                    _folderCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedFolders));
                }
            }
        }

        public double PercentOfParent
        {
            get => _percentOfParent;
            set
            {
                if (Math.Abs(_percentOfParent - value) > 0.001)
                {
                    _percentOfParent = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedPercent));
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasChildren => Children.Count > 0;

        public Thickness IndentMargin => new(Level * 18 + 4, 0, 4, 0);

        public string FormattedSize => FormatBytes(SizeBytes);
        public string FormattedAllocated => FormatBytes(AllocatedBytes);
        public string FormattedFiles => IsDirectory || IsSummaryFilesNode ? FileCount.ToString("N0") : "";
        public string FormattedFolders => IsDirectory && !IsSummaryFilesNode ? FolderCount.ToString("N0") : "";
        public string FormattedPercent => $"{PercentOfParent:F1}%";
        public string FormattedLastModified => LastModified?.ToString("dd.MM.yyyy HH:mm") ?? "-";

        public static string FormatBytes(long bytes, IFormatProvider? provider = null)
        {
            if (bytes <= 0) return "0 Bytes";

            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;
            const double TB = GB * 1024.0;

            if (bytes >= TB)
                return (bytes / TB).ToString("F2", provider) + " TB";
            if (bytes >= GB)
                return (bytes / GB).ToString("F1", provider) + " GB";
            if (bytes >= MB)
                return (bytes / MB).ToString("F1", provider) + " MB";
            if (bytes >= KB)
                return (bytes / KB).ToString("F1", provider) + " KB";

            return $"{bytes} Bytes";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
