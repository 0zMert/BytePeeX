using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Folderize.Common;
using Folderize.Models;
using Folderize.Services;
using Microsoft.Win32;

namespace Folderize.ViewModels
{
    public enum AppViewMode
    {
        TreeGrid,
        FluentExplorer,
        Sunburst,
        Treemap
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DiskScannerService _scannerService = new();
        private CancellationTokenSource? _cancellationTokenSource;

        private string _selectedPath = string.Empty;
        private bool _isScanning;
        private string _statusText = "Hazır. Lütfen taranacak bir dizin veya sürücü seçin.";
        private string _currentScanningPath = string.Empty;
        private FileSystemNode? _rootNode;
        private FileSystemNode? _selectedNode;
        private long _totalFilesCount;
        private long _totalFoldersCount;
        private long _totalSizeBytes;
        private string _currentSortColumn = "Size";
        private bool _sortAscending = false;

        private AppViewMode _selectedViewMode = AppViewMode.TreeGrid;
        private FileSystemNode? _currentDrillDownNode;
        private readonly Stack<FileSystemNode> _navigationHistory = new();
        private readonly InstalledAppService _appService = new();

        private bool _hideSystemInTreemap;
        private bool _useBalancedTreemapScale = true;
        private bool _isSettingsOpen;
        private bool _isAppsViewActive;
        private bool _isTopFilesViewActive;
        private bool _isLargeFilesViewActive;
        private string _activeBottomTab = "Summary";

        public MainViewModel()
        {
            VisibleNodes = new ObservableCollection<FileSystemNode>();
            AvailableDrives = new List<string>();
            BreadcrumbNodes = new ObservableCollection<FileSystemNode>();
            CurrentFolderItems = new ObservableCollection<FileSystemNode>();
            TopSegments = new ObservableCollection<StorageSegment>();
            InstalledApps = new ObservableCollection<InstalledAppModel>();
            TopExtensions = new ObservableCollection<FileExtensionSummary>();
            SelectedFolderFiles = new ObservableCollection<FileSystemNode>();
            TopFilesList = new ObservableCollection<TopFileItem>();
            LargeFilesList = new ObservableCollection<TopFileItem>();

            SelectedPath = "C:\\";
            AvailableDrives = new List<string> { "C:\\" };

            // Load Windows drives directly so they appear on screen immediately without delay
            LoadDrivesDirect();

            SelectFolderCommand = new RelayCommand(SelectFolder);
            StartScanCommand = new RelayCommand(async () => await StartScanAsync(), () => !IsScanning && !string.IsNullOrWhiteSpace(SelectedPath));
            CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
            RefreshCommand = new RelayCommand(async () => await StartScanAsync(), () => !IsScanning && !string.IsNullOrWhiteSpace(SelectedPath));
            SelectDriveCommand = new RelayCommand<string>(drive =>
            {
                if (!string.IsNullOrEmpty(drive))
                {
                    string path = drive.EndsWith("\\") ? drive : drive + "\\";
                    SelectedPath = path;
                    foreach (var card in DriveCards)
                    {
                        card.IsSelected = card.DriveName.TrimEnd('\\').Equals(drive.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                    }
                    OnPropertyChanged(nameof(DriveCards));
                }
            });

            ToggleExpandCommand = new RelayCommand<FileSystemNode>(ToggleNodeExpansion);
            ExpandNextLevelCommand = new RelayCommand(ExpandNextLevel);
            CollapseLevelCommand = new RelayCommand(CollapseLevel);
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            OpenInExplorerCommand = new RelayCommand<object>(OpenInExplorer);
            CopyPathCommand = new RelayCommand<object>(CopyPathToClipboard);
            SelectChartModeCommand = new RelayCommand<string>(mode =>
            {
                IsSunburstChartSelected = (mode == "Sunburst");
            });

            ToggleSettingsCommand = new RelayCommand(() =>
            {
                IsSettingsOpen = !IsSettingsOpen;
            });

            ShowDashboardCommand = new RelayCommand(() =>
            {
                IsAppsViewActive = false;
                IsTopFilesViewActive = false;
                IsLargeFilesViewActive = false;
                IsSettingsOpen = false;
                OnPropertyChanged(nameof(IsDashboardViewActive));
            });

            ShowInstalledAppsCommand = new RelayCommand(async () =>
            {
                IsTopFilesViewActive = false;
                IsLargeFilesViewActive = false;
                IsAppsViewActive = true;
                IsSettingsOpen = false;
                OnPropertyChanged(nameof(IsDashboardViewActive));
                if (InstalledApps.Count == 0)
                {
                    StatusText = Strings.Scanning;
                    var apps = await Task.Run(() => _appService.GetInstalledApplications());
                    InstalledApps.Clear();
                    foreach (var app in apps)
                    {
                        InstalledApps.Add(app);
                    }
                    SortInstalledApps("SizeBytes");
                    StatusText = string.Format(Strings.AppsFoundFormat, InstalledApps.Count);
                }
            });

            ShowTopFilesCommand = new RelayCommand(() =>
            {
                IsAppsViewActive = false;
                IsLargeFilesViewActive = false;
                IsTopFilesViewActive = true;
                IsSettingsOpen = false;
                OnPropertyChanged(nameof(IsDashboardViewActive));
                PopulateTopFiles();
            });

            ShowLargeFilesCommand = new RelayCommand(() =>
            {
                IsAppsViewActive = false;
                IsTopFilesViewActive = false;
                IsLargeFilesViewActive = true;
                IsSettingsOpen = false;
                OnPropertyChanged(nameof(IsDashboardViewActive));
                PopulateLargeFiles(50 * 1024 * 1024);
            });

            FilterLargeFilesCommand = new RelayCommand<string>(thresholdStr =>
            {
                if (long.TryParse(thresholdStr, out long threshold))
                {
                    PopulateLargeFiles(threshold);
                }
            });

            SelectBottomTabCommand = new RelayCommand<string>(tab =>
            {
                if (!string.IsNullOrEmpty(tab))
                {
                    ActiveBottomTab = tab;
                    if (tab == "Files") UpdateSelectedFolderFiles();
                    if (tab == "Summary") UpdateExtensionDistribution();
                }
            });

            ToggleTreemapScaleCommand = new RelayCommand(() =>
            {
                UseBalancedTreemapScale = !UseBalancedTreemapScale;
            });

            SetViewModeCommand = new RelayCommand<string>(modeStr =>
            {
                if (Enum.TryParse<AppViewMode>(modeStr, out var mode))
                {
                    SelectedViewMode = mode;
                }
            });

            DrillDownCommand = new RelayCommand<FileSystemNode>(node =>
            {
                if (node != null && node.IsDirectory)
                {
                    if (CurrentDrillDownNode != null)
                    {
                        _navigationHistory.Push(CurrentDrillDownNode);
                    }
                    SetDrillDownNode(node);
                }
            });

            DrillToNodeCommand = new RelayCommand<FileSystemNode>(node =>
            {
                if (node != null)
                {
                    if (CurrentDrillDownNode != null && CurrentDrillDownNode != node)
                    {
                        _navigationHistory.Push(CurrentDrillDownNode);
                    }
                    SetDrillDownNode(node);
                }
            });

            NavigateUpCommand = new RelayCommand(() =>
            {
                if (CurrentDrillDownNode?.Parent != null)
                {
                    _navigationHistory.Push(CurrentDrillDownNode);
                    SetDrillDownNode(CurrentDrillDownNode.Parent);
                }
            });

            NavigateBackCommand = new RelayCommand(() =>
            {
                if (_navigationHistory.Count > 0)
                {
                    var prev = _navigationHistory.Pop();
                    SetDrillDownNode(prev, addToHistory: false);
                }
            });

            RestartAsAdminCommand = new RelayCommand(() =>
            {
                try
                {
                    var proc = new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(proc);
                    Application.Current.Shutdown();
                }
                catch
                {
                }
            });

            SetLanguageCommand = new RelayCommand<string>(lang =>
            {
                if (!string.IsNullOrEmpty(lang))
                {
                    Strings.CurrentLanguage = lang;
                    NotifyLanguageChanged();
                }
            });
        }

        public LocalizationService Strings => LocalizationService.Instance;
        public bool IsTurkish => Strings.IsTurkish;
        public bool IsEnglish => Strings.IsEnglish;

        public ICommand SetLanguageCommand { get; }

        public string InstalledAppsSortColumn { get; private set; } = "SizeBytes";
        public bool InstalledAppsSortAscending { get; private set; } = false;

        public string InstalledAppsColNameHeader => GetInstalledAppsHeader(Strings.ColAppName, "Name");
        public string InstalledAppsColPublisherHeader => GetInstalledAppsHeader(Strings.ColPublisher, "Publisher");
        public string InstalledAppsColVersionHeader => GetInstalledAppsHeader(Strings.ColVersion, "Version");
        public string InstalledAppsColLastRunHeader => GetInstalledAppsHeader(Strings.ColLastRun, "LastRunTime");
        public string InstalledAppsColSizeHeader => GetInstalledAppsHeader(Strings.ColAppSize, "SizeBytes");
        public string InstalledAppsColPercentHeader => GetInstalledAppsHeader(Strings.ColAppPercent, "PercentOfTotal");

        private string GetInstalledAppsHeader(string title, string column)
        {
            if (InstalledAppsSortColumn.Equals(column, StringComparison.OrdinalIgnoreCase))
            {
                return $"{title} {(InstalledAppsSortAscending ? "▲" : "▼")}";
            }
            return title;
        }

        public void SortInstalledApps(string columnName)
        {
            if (InstalledApps.Count == 0) return;

            if (InstalledAppsSortColumn.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                InstalledAppsSortAscending = !InstalledAppsSortAscending;
            }
            else
            {
                InstalledAppsSortColumn = columnName;
                InstalledAppsSortAscending = (columnName.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                                              columnName.Equals("Publisher", StringComparison.OrdinalIgnoreCase) ||
                                              columnName.Equals("Version", StringComparison.OrdinalIgnoreCase));
            }

            var list = InstalledApps.ToList();
            IEnumerable<InstalledAppModel> sorted = columnName.ToLowerInvariant() switch
            {
                "name" => InstalledAppsSortAscending
                    ? list.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                    : list.OrderByDescending(a => a.Name, StringComparer.CurrentCultureIgnoreCase),

                "publisher" => InstalledAppsSortAscending
                    ? list.OrderBy(a => a.Publisher, StringComparer.CurrentCultureIgnoreCase)
                    : list.OrderByDescending(a => a.Publisher, StringComparer.CurrentCultureIgnoreCase),

                "version" => InstalledAppsSortAscending
                    ? list.OrderBy(a => a.Version, StringComparer.CurrentCultureIgnoreCase)
                    : list.OrderByDescending(a => a.Version, StringComparer.CurrentCultureIgnoreCase),

                "lastruntime" or "lastrun" => InstalledAppsSortAscending
                    ? list.OrderBy(a => a.LastRunTime ?? DateTime.MinValue)
                    : list.OrderByDescending(a => a.LastRunTime ?? DateTime.MinValue),

                "sizebytes" or "size" => InstalledAppsSortAscending
                    ? list.OrderBy(a => a.SizeBytes)
                    : list.OrderByDescending(a => a.SizeBytes),

                "percentoftotal" or "percent" => InstalledAppsSortAscending
                    ? list.OrderBy(a => a.PercentOfTotal)
                    : list.OrderByDescending(a => a.PercentOfTotal),

                _ => list
            };

            InstalledApps.Clear();
            foreach (var app in sorted)
            {
                InstalledApps.Add(app);
            }

            OnPropertyChanged(nameof(InstalledAppsColNameHeader));
            OnPropertyChanged(nameof(InstalledAppsColPublisherHeader));
            OnPropertyChanged(nameof(InstalledAppsColVersionHeader));
            OnPropertyChanged(nameof(InstalledAppsColLastRunHeader));
            OnPropertyChanged(nameof(InstalledAppsColSizeHeader));
            OnPropertyChanged(nameof(InstalledAppsColPercentHeader));
        }

        public void NotifyLanguageChanged()
        {
            OnPropertyChanged(nameof(IsTurkish));
            OnPropertyChanged(nameof(IsEnglish));
            OnPropertyChanged(nameof(Strings));
            OnPropertyChanged(nameof(InstalledAppsColNameHeader));
            OnPropertyChanged(nameof(InstalledAppsColPublisherHeader));
            OnPropertyChanged(nameof(InstalledAppsColVersionHeader));
            OnPropertyChanged(nameof(InstalledAppsColLastRunHeader));
            OnPropertyChanged(nameof(InstalledAppsColSizeHeader));
            OnPropertyChanged(nameof(InstalledAppsColPercentHeader));

            LoadDrivesDirect();

            if (InstalledApps.Count > 0)
            {
                var current = InstalledApps.ToList();
                InstalledApps.Clear();
                foreach (var app in current) InstalledApps.Add(app);
                StatusText = string.Format(Strings.AppsFoundFormat, InstalledApps.Count);
            }
        }

        public ObservableCollection<FileSystemNode> VisibleNodes { get; }
        public List<string> AvailableDrives { get; }
        public ObservableCollection<FileSystemNode> BreadcrumbNodes { get; }
        public ObservableCollection<FileSystemNode> CurrentFolderItems { get; }
        public ObservableCollection<StorageSegment> TopSegments { get; }
        public ObservableCollection<InstalledAppModel> InstalledApps { get; }
        public ObservableCollection<FileExtensionSummary> TopExtensions { get; }
        public ObservableCollection<FileSystemNode> SelectedFolderFiles { get; }
        public ObservableCollection<TopFileItem> TopFilesList { get; }
        public ObservableCollection<TopFileItem> LargeFilesList { get; }

        public bool HideSystemInTreemap
        {
            get => _hideSystemInTreemap;
            set
            {
                if (_hideSystemInTreemap != value)
                {
                    _hideSystemInTreemap = value;
                    OnPropertyChanged();
                    NotifyTreeStructureChanged();
                }
            }
        }

        public bool UseBalancedTreemapScale
        {
            get => _useBalancedTreemapScale;
            set
            {
                if (_useBalancedTreemapScale != value)
                {
                    _useBalancedTreemapScale = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TreemapScaleModeText));
                    NotifyTreeStructureChanged();
                }
            }
        }

        public string TreemapScaleModeText => UseBalancedTreemapScale ? "⚖ Dengeli Görünüm" : "📏 Gerçek Oran (1:1)";

        private bool _isSunburstChartSelected = false;
        public bool IsSunburstChartSelected
        {
            get => _isSunburstChartSelected;
            set
            {
                if (_isSunburstChartSelected != value)
                {
                    _isSunburstChartSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTreemapChartSelected));
                }
            }
        }

        public bool IsTreemapChartSelected => !IsSunburstChartSelected;

        public ICommand SelectChartModeCommand { get; }

        private int _treeStructureVersion;
        public int TreeStructureVersion
        {
            get => _treeStructureVersion;
            set
            {
                if (_treeStructureVersion != value)
                {
                    _treeStructureVersion = value;
                    OnPropertyChanged();
                }
            }
        }

        public void NotifyTreeStructureChanged()
        {
            TreeStructureVersion++;
        }

        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set
            {
                if (_isSettingsOpen != value)
                {
                    _isSettingsOpen = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsAppsViewActive
        {
            get => _isAppsViewActive;
            set
            {
                if (_isAppsViewActive != value)
                {
                    _isAppsViewActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDashboardViewActive));
                }
            }
        }

        public bool IsTopFilesViewActive
        {
            get => _isTopFilesViewActive;
            set
            {
                if (_isTopFilesViewActive != value)
                {
                    _isTopFilesViewActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDashboardViewActive));
                }
            }
        }

        public bool IsLargeFilesViewActive
        {
            get => _isLargeFilesViewActive;
            set
            {
                if (_isLargeFilesViewActive != value)
                {
                    _isLargeFilesViewActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDashboardViewActive));
                }
            }
        }

        public bool IsDashboardViewActive => !IsAppsViewActive && !IsTopFilesViewActive && !IsLargeFilesViewActive;

        public string ActiveBottomTab
        {
            get => _activeBottomTab;
            set
            {
                if (_activeBottomTab != value)
                {
                    _activeBottomTab = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSummaryTabSelected));
                    OnPropertyChanged(nameof(IsDetailsTabSelected));
                    OnPropertyChanged(nameof(IsFilesTabSelected));
                }
            }
        }

        public bool IsSummaryTabSelected => ActiveBottomTab == "Summary";
        public bool IsDetailsTabSelected => ActiveBottomTab == "Details";
        public bool IsFilesTabSelected => ActiveBottomTab == "Files";

        public AppViewMode SelectedViewMode
        {
            get => _selectedViewMode;
            set
            {
                if (_selectedViewMode != value)
                {
                    _selectedViewMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTreeGridVisible));
                    OnPropertyChanged(nameof(IsFluentExplorerVisible));
                    OnPropertyChanged(nameof(IsSunburstVisible));
                    OnPropertyChanged(nameof(IsTreemapVisible));
                }
            }
        }

        public bool IsTreeGridVisible => SelectedViewMode == AppViewMode.TreeGrid;
        public bool IsFluentExplorerVisible => SelectedViewMode == AppViewMode.FluentExplorer;
        public bool IsSunburstVisible => SelectedViewMode == AppViewMode.Sunburst;
        public bool IsTreemapVisible => SelectedViewMode == AppViewMode.Treemap;

        public FileSystemNode? CurrentDrillDownNode
        {
            get => _currentDrillDownNode;
            set
            {
                if (_currentDrillDownNode != value)
                {
                    _currentDrillDownNode = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedPath
        {
            get => _selectedPath;
            set
            {
                if (_selectedPath != value)
                {
                    _selectedPath = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (_isScanning != value)
                {
                    _isScanning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanStop));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanStop => IsScanning;

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentScanningPath
        {
            get => _currentScanningPath;
            set
            {
                if (_currentScanningPath != value)
                {
                    _currentScanningPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public FileSystemNode? RootNode
        {
            get => _rootNode;
            set
            {
                if (_rootNode != value)
                {
                    _rootNode = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<DriveCardModel> DriveCards { get; } = new();
        public ObservableCollection<FileSystemNode> Top5SubFolders { get; } = new();

        public FileSystemNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    OnPropertyChanged();
                    UpdateTopFolders();
                    UpdateExtensionDistribution();
                    UpdateSelectedFolderFiles();
                }
            }
        }

        public long TotalSizeBytes
        {
            get => _totalSizeBytes;
            set
            {
                if (_totalSizeBytes != value)
                {
                    _totalSizeBytes = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedTotalSize));
                }
            }
        }

        public long TotalFilesCount
        {
            get => _totalFilesCount;
            set
            {
                if (_totalFilesCount != value)
                {
                    _totalFilesCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedTotalFiles));
                }
            }
        }

        public long TotalFoldersCount
        {
            get => _totalFoldersCount;
            set
            {
                if (_totalFoldersCount != value)
                {
                    _totalFoldersCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedTotalFolders));
                }
            }
        }

        public string FormattedTotalSize => FileSystemNode.FormatBytes(TotalSizeBytes);
        public string FormattedTotalFiles => TotalFilesCount.ToString("N0");
        public string FormattedTotalFolders => TotalFoldersCount.ToString("N0");

        public ICommand SelectFolderCommand { get; }
        public ICommand StartScanCommand { get; }
        public ICommand CancelScanCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SelectDriveCommand { get; }
        public ICommand ToggleExpandCommand { get; }
        public ICommand ExpandNextLevelCommand { get; }
        public ICommand CollapseLevelCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand OpenInExplorerCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand SetViewModeCommand { get; }
        public ICommand DrillDownCommand { get; }
        public ICommand DrillToNodeCommand { get; }
        public ICommand NavigateUpCommand { get; }
        public ICommand NavigateBackCommand { get; }
        public ICommand RestartAsAdminCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowInstalledAppsCommand { get; }
        public ICommand ShowTopFilesCommand { get; }
        public ICommand ShowLargeFilesCommand { get; }
        public ICommand FilterLargeFilesCommand { get; }
        public ICommand SelectBottomTabCommand { get; }
        public ICommand ToggleTreemapScaleCommand { get; }

        public void LoadDrivesDirect()
        {
            var drivesList = new List<string>();
            var cardsList = new List<DriveCardModel>();
            try
            {
                var readyDrives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                    .ToList();

                foreach (var d in readyDrives)
                {
                    drivesList.Add(d.Name);
                    long total = d.TotalSize;
                    long free = d.TotalFreeSpace;
                    long used = total - free;
                    cardsList.Add(new DriveCardModel
                    {
                        DriveName = d.Name.TrimEnd('\\'),
                        TotalSizeBytes = total,
                        UsedSizeBytes = used,
                        FreeSizeBytes = free,
                        UsedPercent = total > 0 ? (double)used / total * 100.0 : 0,
                        IsRemovable = d.DriveType == DriveType.Removable,
                        IsSelected = d.Name.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
            catch
            {
                drivesList.Add("C:\\");
            }

            AvailableDrives.Clear();
            AvailableDrives.AddRange(drivesList);

            DriveCards.Clear();
            foreach (var card in cardsList)
            {
                DriveCards.Add(card);
            }

            if (!string.IsNullOrEmpty(SelectedPath))
            {
                foreach (var card in DriveCards)
                {
                    card.IsSelected = card.DriveName.TrimEnd('\\').Equals(SelectedPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public void SetDrillDownNode(FileSystemNode? node, bool addToHistory = true)
        {
            if (node == null) return;

            CurrentDrillDownNode = node;

            // 1. Rebuild Breadcrumbs
            BreadcrumbNodes.Clear();
            var curr = node;
            var list = new List<FileSystemNode>();
            while (curr != null)
            {
                list.Insert(0, curr);
                curr = curr.Parent;
            }
            foreach (var b in list)
            {
                BreadcrumbNodes.Add(b);
            }

            // 2. Rebuild CurrentFolderItems
            CurrentFolderItems.Clear();
            var children = new List<FileSystemNode>(node.Children);
            SortNodeChildrenRecursive(node, _currentSortColumn, _sortAscending);

            long parentSize = node.SizeBytes;
            foreach (var child in children)
            {
                if (parentSize > 0)
                {
                    child.PercentOfParent = Math.Min(100.0, (double)child.SizeBytes / parentSize * 100.0);
                }
                CurrentFolderItems.Add(child);
            }

            // 3. Rebuild TopSegments for Donut Chart & Capacity Bar
            TopSegments.Clear();
            var topChildren = children.Where(c => c.SizeBytes > 0).OrderByDescending(c => c.SizeBytes).Take(6).ToList();
            long topSize = topChildren.Sum(c => c.SizeBytes);
            long otherSize = parentSize > topSize ? parentSize - topSize : 0;

            string[] segmentPalette = new[]
            {
                "#38BDF8", // Sky Blue
                "#818CF8", // Indigo
                "#34D399", // Emerald Green
                "#FBBF24", // Amber Yellow
                "#F43F5E", // Rose Red
                "#A78BFA", // Violet
                "#FB923C", // Orange
                "#2DD4BF"  // Cyan / Teal
            };

            for (int i = 0; i < topChildren.Count; i++)
            {
                var c = topChildren[i];
                TopSegments.Add(new StorageSegment
                {
                    Name = c.Name,
                    SizeBytes = c.SizeBytes,
                    Percentage = parentSize > 0 ? (double)c.SizeBytes / parentSize * 100.0 : 0,
                    ColorHex = segmentPalette[i % segmentPalette.Length]
                });
            }

            if (otherSize > 0)
            {
                TopSegments.Add(new StorageSegment
                {
                    Name = "Diğer",
                    SizeBytes = otherSize,
                    Percentage = parentSize > 0 ? (double)otherSize / parentSize * 100.0 : 0,
                    ColorHex = "#64748B"
                });
            }
        }

        public void UpdateTopFolders()
        {
            Top5SubFolders.Clear();
            var target = SelectedNode ?? RootNode;
            if (target != null && target.Children.Count > 0)
            {
                var top = target.Children.Where(c => c.SizeBytes > 0).OrderByDescending(c => c.SizeBytes).Take(5).ToList();
                foreach (var c in top)
                {
                    Top5SubFolders.Add(c);
                }
            }
        }

        public void UpdateExtensionDistribution()
        {
            TopExtensions.Clear();
            var target = SelectedNode ?? RootNode;
            if (target == null) return;

            var extMap = new Dictionary<string, (long bytes, int count)>(StringComparer.OrdinalIgnoreCase);
            CollectExtensionsRecursive(target, extMap, 0, 50000);

            if (extMap.Count == 0) return;

            long totalExtSize = extMap.Values.Sum(v => v.bytes);
            var sorted = extMap.OrderByDescending(kv => kv.Value.bytes).ToList();

            var colors = new[] { "#38BDF8", "#818CF8", "#34D399", "#FBBF24", "#F472B6", "#A78BFA", "#94A3B8" };
            int colorIdx = 0;

            var top5 = sorted.Take(5).ToList();
            long top5Size = 0;

            foreach (var item in top5)
            {
                top5Size += item.Value.bytes;
                double pct = totalExtSize > 0 ? (double)item.Value.bytes / totalExtSize * 100.0 : 0;
                TopExtensions.Add(new FileExtensionSummary
                {
                    Extension = item.Key,
                    FileCount = item.Value.count,
                    TotalSizeBytes = item.Value.bytes,
                    Percentage = pct,
                    ColorHex = colors[colorIdx % colors.Length]
                });
                colorIdx++;
            }

            long otherBytes = totalExtSize - top5Size;
            int otherCount = sorted.Skip(5).Sum(kv => kv.Value.count);
            if (otherBytes > 0)
            {
                double pct = totalExtSize > 0 ? (double)otherBytes / totalExtSize * 100.0 : 0;
                TopExtensions.Add(new FileExtensionSummary
                {
                    Extension = "Diğer",
                    FileCount = otherCount,
                    TotalSizeBytes = otherBytes,
                    Percentage = pct,
                    ColorHex = "#64748B"
                });
            }
        }

        private void CollectExtensionsRecursive(
            FileSystemNode node,
            Dictionary<string, (long bytes, int count)> extMap,
            int currentDepth,
            int maxFiles)
        {
            if (extMap.Count >= maxFiles) return;

            foreach (var child in node.Children)
            {
                if (!child.IsDirectory && !child.IsSummaryFilesNode)
                {
                    string ext = Path.GetExtension(child.Name);
                    if (string.IsNullOrEmpty(ext)) ext = "[Uzantısız]";
                    else ext = ext.ToLowerInvariant();

                    if (extMap.TryGetValue(ext, out var val))
                    {
                        extMap[ext] = (val.bytes + child.SizeBytes, val.count + 1);
                    }
                    else
                    {
                        extMap[ext] = (child.SizeBytes, 1);
                    }
                }
                else if (child.IsSummaryFilesNode)
                {
                    foreach (var f in child.Children)
                    {
                        string ext = Path.GetExtension(f.Name);
                        if (string.IsNullOrEmpty(ext)) ext = "[Uzantısız]";
                        else ext = ext.ToLowerInvariant();

                        if (extMap.TryGetValue(ext, out var val))
                        {
                            extMap[ext] = (val.bytes + f.SizeBytes, val.count + 1);
                        }
                        else
                        {
                            extMap[ext] = (f.SizeBytes, 1);
                        }
                    }
                }
                else if (child.IsDirectory && currentDepth < 8)
                {
                    CollectExtensionsRecursive(child, extMap, currentDepth + 1, maxFiles);
                }
            }
        }

        public void UpdateSelectedFolderFiles()
        {
            SelectedFolderFiles.Clear();
            var target = SelectedNode ?? RootNode;
            if (target == null) return;

            var list = new List<FileSystemNode>();
            foreach (var child in target.Children)
            {
                if (!child.IsDirectory && !child.IsSummaryFilesNode)
                {
                    list.Add(child);
                }
                else if (child.IsSummaryFilesNode)
                {
                    list.AddRange(child.Children);
                }
            }

            foreach (var f in list.OrderByDescending(f => f.SizeBytes).Take(500))
            {
                SelectedFolderFiles.Add(f);
            }
        }

        public void PopulateTopFiles()
        {
            TopFilesList.Clear();
            if (RootNode == null) return;

            var allFiles = new List<FileSystemNode>();
            CollectAllFilesRecursive(RootNode, allFiles, 5000);

            long totalRoot = RootNode.SizeBytes;
            int rank = 1;
            foreach (var f in allFiles.OrderByDescending(f => f.SizeBytes).Take(100))
            {
                TopFilesList.Add(new TopFileItem
                {
                    Rank = rank++,
                    Name = f.Name,
                    FullPath = f.FullPath,
                    SizeBytes = f.SizeBytes,
                    PercentOfTotal = totalRoot > 0 ? (double)f.SizeBytes / totalRoot * 100.0 : 0,
                    LastModified = f.LastModified
                });
            }
        }

        public void PopulateLargeFiles(long minBytes = 52428800)
        {
            LargeFilesList.Clear();
            if (RootNode == null) return;

            var allFiles = new List<FileSystemNode>();
            CollectAllFilesRecursive(RootNode, allFiles, 5000);

            long totalRoot = RootNode.SizeBytes;
            int rank = 1;
            foreach (var f in allFiles.Where(f => f.SizeBytes >= minBytes).OrderByDescending(f => f.SizeBytes).Take(200))
            {
                LargeFilesList.Add(new TopFileItem
                {
                    Rank = rank++,
                    Name = f.Name,
                    FullPath = f.FullPath,
                    SizeBytes = f.SizeBytes,
                    PercentOfTotal = totalRoot > 0 ? (double)f.SizeBytes / totalRoot * 100.0 : 0,
                    LastModified = f.LastModified
                });
            }
        }

        private void CollectAllFilesRecursive(FileSystemNode node, List<FileSystemNode> list, int maxCount)
        {
            if (list.Count >= maxCount) return;

            foreach (var c in node.Children)
            {
                if (!c.IsDirectory && !c.IsSummaryFilesNode)
                {
                    list.Add(c);
                }
                else if (c.IsSummaryFilesNode)
                {
                    list.AddRange(c.Children);
                }
                else if (c.IsDirectory)
                {
                    CollectAllFilesRecursive(c, list, maxCount);
                }
            }
        }

        private void SelectFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Taranacak Klasörü Seçin",
                InitialDirectory = Directory.Exists(SelectedPath) ? SelectedPath : "C:\\"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedPath = dialog.FolderName;
                _ = StartScanAsync();
            }
        }

        public async Task StartScanAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedPath) || !Directory.Exists(SelectedPath))
            {
                StatusText = "Geçersiz dizin yolu!";
                return;
            }

            CancelScan();

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            IsScanning = true;
            StatusText = "Tarama başlatılıyor...";
            CurrentScanningPath = SelectedPath;
            VisibleNodes.Clear();
            RootNode = null;
            TotalSizeBytes = 0;
            TotalFilesCount = 0;
            TotalFoldersCount = 0;

            var stopwatch = Stopwatch.StartNew();
            long lastLiveRefresh = 0;

            var progress = new Progress<ScanProgress>(p =>
            {
                CurrentScanningPath = p.CurrentPath;
                TotalFilesCount = p.TotalFiles;
                TotalFoldersCount = p.TotalFolders;
                TotalSizeBytes = p.TotalBytes;
                StatusText = $"Taranıyor: {p.TotalFiles:N0} dosya ({FileSystemNode.FormatBytes(p.TotalBytes)})";

                // LIVE STREAMING: Show root and top folders right away!
                if (p.RootNode != null)
                {
                    if (RootNode == null)
                    {
                        RootNode = p.RootNode;
                        VisibleNodes.Clear();
                        VisibleNodes.Add(RootNode);
                        foreach (var child in RootNode.Children)
                        {
                            VisibleNodes.Add(child);
                        }
                        CurrentDrillDownNode = RootNode;
                        SetDrillDownNode(RootNode, addToHistory: false);
                    }
                    else
                    {
                        long currentRootSize = p.TotalBytes > 0 ? p.TotalBytes : RootNode.SizeBytes;
                        if (currentRootSize > 0)
                        {
                            foreach (var child in RootNode.Children)
                            {
                                child.PercentOfParent = Math.Min(100.0, (double)child.SizeBytes / currentRootSize * 100.0);
                            }
                        }

                        // Periodic live treemap and chart updates (~500ms)
                        long now = stopwatch.ElapsedMilliseconds;
                        if (now - lastLiveRefresh > 500)
                        {
                            lastLiveRefresh = now;
                            NotifyTreeStructureChanged();
                            UpdateTopFolders();
                            UpdateExtensionDistribution();
                        }
                    }
                }
            });

            try
            {
                var result = await _scannerService.ScanPathAsync(SelectedPath, progress, token);
                stopwatch.Stop();

                if (result != null)
                {
                    RootNode = result;
                    TotalSizeBytes = result.SizeBytes;
                    TotalFilesCount = result.FileCount;
                    TotalFoldersCount = result.FolderCount;

                    // Rebuild visible list with root and first level children
                    VisibleNodes.Clear();
                    VisibleNodes.Add(RootNode);
                    RootNode.IsExpanded = true;

                    foreach (var child in RootNode.Children)
                    {
                        VisibleNodes.Add(child);
                    }

                    SetDrillDownNode(RootNode, addToHistory: false);
                    SelectedNode = RootNode.Children.Count > 0 ? RootNode.Children[0] : RootNode;
                    UpdateTopFolders();
                    UpdateExtensionDistribution();
                    UpdateSelectedFolderFiles();
                    NotifyTreeStructureChanged();

                    StatusText = $"Tarama tamamlandı ({stopwatch.Elapsed.TotalSeconds:F1} sn). Toplam: {FormattedTotalSize}, {TotalFilesCount:N0} dosya, {TotalFoldersCount:N0} klasör.";
                    CurrentScanningPath = "Hazır";
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "Tarama kullanıcı tarafından durduruldu.";
                CurrentScanningPath = "Durduruldu";
            }
            catch (Exception ex)
            {
                StatusText = $"Tarama sırasında hata oluştu: {ex.Message}";
                CurrentScanningPath = "Hata";
            }
            finally
            {
                IsScanning = false;
            }
        }

        public void CancelScan()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                }
                catch
                {
                }
                _cancellationTokenSource = null;
            }
        }

        public void ToggleNodeExpansion(FileSystemNode? node)
        {
            if (node == null || !node.HasChildren) return;

            if (node.IsExpanded)
            {
                CollapseNode(node);
            }
            else
            {
                ExpandNode(node);
            }
        }

        private void ExpandNode(FileSystemNode node)
        {
            node.IsExpanded = true;
            int index = VisibleNodes.IndexOf(node);
            if (index >= 0)
            {
                InsertChildrenRecursive(node, ref index);
            }
            NotifyTreeStructureChanged();
        }

        private void InsertChildrenRecursive(FileSystemNode parent, ref int index)
        {
            foreach (var child in parent.Children)
            {
                index++;
                if (!VisibleNodes.Contains(child))
                {
                    VisibleNodes.Insert(index, child);
                }

                if (child.IsExpanded)
                {
                    InsertChildrenRecursive(child, ref index);
                }
            }
        }

        private void CollapseNode(FileSystemNode node)
        {
            node.IsExpanded = false;
            RemoveChildrenRecursive(node);
            NotifyTreeStructureChanged();
        }

        private void RemoveChildrenRecursive(FileSystemNode parent)
        {
            foreach (var child in parent.Children)
            {
                if (child.IsExpanded)
                {
                    RemoveChildrenRecursive(child);
                }
                VisibleNodes.Remove(child);
            }
        }

        public void SortBy(string columnName)
        {
            if (_currentSortColumn == columnName)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _currentSortColumn = columnName;
                _sortAscending = false; // default descending for size/count
            }

            if (RootNode != null)
            {
                SortNodeChildrenRecursive(RootNode, _currentSortColumn, _sortAscending);

                // Re-render visible list
                var expandedPaths = new HashSet<string>(VisibleNodes.Where(n => n.IsExpanded).Select(n => n.FullPath));
                VisibleNodes.Clear();
                RebuildVisibleList(RootNode);
                NotifyTreeStructureChanged();
            }
        }

        private void RebuildVisibleList(FileSystemNode node)
        {
            VisibleNodes.Add(node);
            if (node.IsExpanded)
            {
                foreach (var child in node.Children)
                {
                    RebuildVisibleList(child);
                }
            }
        }

        private void SortNodeChildrenRecursive(FileSystemNode node, string column, bool ascending)
        {
            switch (column)
            {
                case "Name":
                    node.Children.Sort((a, b) => ascending ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) : string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
                    break;
                case "Size":
                case "Percent":
                    node.Children.Sort((a, b) => ascending ? a.SizeBytes.CompareTo(b.SizeBytes) : b.SizeBytes.CompareTo(a.SizeBytes));
                    break;
                case "Allocated":
                    node.Children.Sort((a, b) => ascending ? a.AllocatedBytes.CompareTo(b.AllocatedBytes) : b.AllocatedBytes.CompareTo(a.AllocatedBytes));
                    break;
                case "Files":
                    node.Children.Sort((a, b) => ascending ? a.FileCount.CompareTo(b.FileCount) : b.FileCount.CompareTo(a.FileCount));
                    break;
                case "Folders":
                    node.Children.Sort((a, b) => ascending ? a.FolderCount.CompareTo(b.FolderCount) : b.FolderCount.CompareTo(a.FolderCount));
                    break;
                case "LastModified":
                    node.Children.Sort((a, b) =>
                    {
                        var tA = a.LastModified ?? DateTime.MinValue;
                        var tB = b.LastModified ?? DateTime.MinValue;
                        return ascending ? tA.CompareTo(tB) : tB.CompareTo(tA);
                    });
                    break;
            }

            foreach (var child in node.Children)
            {
                SortNodeChildrenRecursive(child, column, ascending);
            }
        }

        private bool _isTreeUpdating;

        public void ExpandNextLevel()
        {
            if (RootNode == null || _isTreeUpdating) return;

            _isTreeUpdating = true;
            try
            {
                // Find all directory nodes in VisibleNodes that have children and are NOT yet expanded
                // Exclude summary file nodes ([XX Files]) and non-directories so files don't block folder expansion!
                var unexpandedVisibleFolders = VisibleNodes
                    .Where(n => n.IsDirectory && !n.IsSummaryFilesNode && n.HasChildren && !n.IsExpanded)
                    .ToList();

                if (unexpandedVisibleFolders.Count == 0)
                {
                    return;
                }

                // Expand all currently visible unexpanded folders across all active branches
                // Even if some branches have already reached maximum depth, all other branches with subdirectories continue expanding!
                foreach (var node in unexpandedVisibleFolders.Take(500))
                {
                    node.IsExpanded = true;
                }

                RebuildVisibleNodes();
            }
            finally
            {
                _isTreeUpdating = false;
            }
        }

        public void CollapseLevel()
        {
            if (RootNode == null || _isTreeUpdating) return;

            _isTreeUpdating = true;
            try
            {
                var expandedFolders = VisibleNodes
                    .Where(n => n.IsExpanded && n.HasChildren && n != RootNode && n.IsDirectory && !n.IsSummaryFilesNode)
                    .ToList();

                if (expandedFolders.Count == 0) return;

                int maxLevel = expandedFolders.Max(n => n.Level);

                foreach (var node in expandedFolders.Where(n => n.Level == maxLevel))
                {
                    node.IsExpanded = false;
                }

                RebuildVisibleNodes();
            }
            finally
            {
                _isTreeUpdating = false;
            }
        }

        private void ExpandAll()
        {
            if (RootNode == null || _isTreeUpdating) return;

            _isTreeUpdating = true;
            try
            {
                // Controlled expansion up to depth 3 and max 1500 items to prevent UI freezing
                int expandedCount = 0;
                ExpandRecursiveSafe(RootNode, 0, 3, ref expandedCount, 1500);

                RebuildVisibleNodes();
            }
            finally
            {
                _isTreeUpdating = false;
            }
        }

        private void ExpandRecursiveSafe(FileSystemNode node, int currentDepth, int maxDepth, ref int count, int maxCount)
        {
            if (count >= maxCount || currentDepth > maxDepth) return;

            if (node.HasChildren)
            {
                node.IsExpanded = true;
                count++;
                foreach (var child in node.Children)
                {
                    if (count >= maxCount) break;
                    ExpandRecursiveSafe(child, currentDepth + 1, maxDepth, ref count, maxCount);
                }
            }
        }

        private void CollapseAll()
        {
            if (RootNode == null || _isTreeUpdating) return;

            _isTreeUpdating = true;
            try
            {
                CollapseRecursive(RootNode);
                RootNode.IsExpanded = true;
                RebuildVisibleNodes();
            }
            finally
            {
                _isTreeUpdating = false;
            }
        }

        private void RebuildVisibleNodes()
        {
            if (RootNode == null) return;

            var list = new List<FileSystemNode>();
            var visited = new HashSet<FileSystemNode>();
            AddExpandedNodesRecursive(RootNode, list, visited, 0, 15);

            VisibleNodes.Clear();
            foreach (var item in list)
            {
                VisibleNodes.Add(item);
            }
            NotifyTreeStructureChanged();
        }

        private void AddExpandedNodesRecursive(FileSystemNode node, List<FileSystemNode> list, HashSet<FileSystemNode> visited, int currentDepth, int maxDepth)
        {
            if (currentDepth > maxDepth || !visited.Add(node)) return;

            list.Add(node);
            if (node.IsExpanded && node.HasChildren)
            {
                foreach (var child in node.Children)
                {
                    AddExpandedNodesRecursive(child, list, visited, currentDepth + 1, maxDepth);
                }
            }
        }

        private void CollapseRecursive(FileSystemNode node)
        {
            node.IsExpanded = false;
            foreach (var child in node.Children)
            {
                CollapseRecursive(child);
            }
        }

        private void OpenInExplorer(object? arg)
        {
            string? path = null;
            if (arg is FileSystemNode node) path = node.FullPath;
            else if (arg is TopFileItem tfi) path = tfi.FullPath;
            else if (arg is string s) path = s;
            else if (SelectedNode != null) path = SelectedNode.FullPath;

            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", $"\"{path}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Klasör açılamadı: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyPathToClipboard(object? arg)
        {
            string? path = null;
            if (arg is FileSystemNode node) path = node.FullPath;
            else if (arg is TopFileItem tfi) path = tfi.FullPath;
            else if (arg is string s) path = s;
            else if (SelectedNode != null) path = SelectedNode.FullPath;

            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                Clipboard.SetText(path);
            }
            catch
            {
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
