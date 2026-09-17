using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Folderize.Services
{
    public class LocalizationService : INotifyPropertyChanged
    {
        public static LocalizationService Instance { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _currentLanguage = "tr";
        private readonly string _configFilePath;

        public LocalizationService()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "Folderize");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                _configFilePath = Path.Combine(folder, "config.json");

                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Language", out var langProp))
                    {
                        string? lang = langProp.GetString();
                        if (lang == "en" || lang == "tr")
                        {
                            _currentLanguage = lang;
                            return;
                        }
                    }
                }

                // Default detection from system culture
                string systemLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                _currentLanguage = systemLang == "tr" ? "tr" : "en";
            }
            catch
            {
                _currentLanguage = "tr";
                _configFilePath = "";
            }
        }

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value && (value == "tr" || value == "en"))
                {
                    _currentLanguage = value;
                    SavePreference(value);
                    OnPropertyChanged(string.Empty);
                }
            }
        }

        public bool IsTurkish => _currentLanguage == "tr";
        public bool IsEnglish => _currentLanguage == "en";

        private void SavePreference(string lang)
        {
            try
            {
                if (!string.IsNullOrEmpty(_configFilePath))
                {
                    string json = JsonSerializer.Serialize(new { Language = lang });
                    File.WriteAllText(_configFilePath, json);
                }
            }
            catch
            {
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ==================== LOCALIZED STRINGS ====================

        // Top Toolbar
        public string SelectFolder => IsTurkish ? "Dizin Seç" : "Select Folder";
        public string StartScan => IsTurkish ? "Tara" : "Scan";
        public string StopScan => IsTurkish ? "Durdur" : "Stop";
        public string Scanning => IsTurkish ? "Taranıyor..." : "Scanning...";
        public string RescanToolTip => IsTurkish ? "Yeniden Tara" : "Rescan";
        public string SettingsToolTip => IsTurkish ? "Ayarlar" : "Settings";
        public string SearchPlaceholder => IsTurkish ? "Klasör veya dosya ara..." : "Search files or folders...";

        // Sidebar Navigation
        public string NavOverview => IsTurkish ? "Genel Bakış" : "Overview";
        public string NavInstalledApps => IsTurkish ? "Yüklü Uygulamalar" : "Installed Apps";
        public string NavFileExplorer => IsTurkish ? "Dosya Gezgini" : "File Explorer";
        public string NavTopFiles => IsTurkish ? "En Büyük Dosyalar" : "Top Files";
        public string NavLargeFiles => IsTurkish ? "Büyük Dosyalar" : "Large Files";
        public string NavSettings => IsTurkish ? "Ayarlar" : "Settings";
        public string LocalDrives => IsTurkish ? "YEREL SÜRÜCÜLER" : "LOCAL DRIVES";
        public string FreeSuffix => IsTurkish ? "boş" : "free";
        public string UsedSuffix => IsTurkish ? "kullanılan" : "used";

        // Tree List Columns & Toolbar
        public string ColName => IsTurkish ? "Ad" : "Name";
        public string ColSize => IsTurkish ? "Boyut" : "Size";
        public string ColPercent => IsTurkish ? "Oran (%)" : "Percent (%)";
        public string ColAllocated => IsTurkish ? "Ayrılan Alan" : "Allocated Space";
        public string ColFiles => IsTurkish ? "Dosyalar" : "Files";
        public string ColFolders => IsTurkish ? "Klasörler" : "Folders";
        public string ExpandLevel => IsTurkish ? "+1 Seviye Genişlet" : "+1 Expand Level";
        public string CollapseLevel => IsTurkish ? "-1 Seviye Daralt" : "-1 Collapse Level";
        public string ExpandAll => IsTurkish ? "Tümünü Genişlet" : "Expand All";
        public string CollapseAll => IsTurkish ? "Tümünü Daralt" : "Collapse All";

        // Charts & Tabs
        public string ChartView => IsTurkish ? "Grafik Görünümü" : "Chart View";
        public string Treemap => IsTurkish ? "Blok Harita" : "Treemap";
        public string Sunburst => IsTurkish ? "Güneş" : "Sunburst";
        public string ScaleLinear => IsTurkish ? "Lineer Ölçek" : "Linear Scale";
        public string ScaleBalanced => IsTurkish ? "Dengeli Ölçek" : "Balanced Scale";
        public string TabTreemap => IsTurkish ? "Blok Harita" : "Treemap";
        public string TabExtensions => IsTurkish ? "Dosya Türleri" : "Extensions";
        public string TabLargeFiles => IsTurkish ? "Büyük Dosyalar" : "Large Files";
        public string TabSunburst => IsTurkish ? "Güneş Grafiği" : "Sunburst";

        // Installed Apps View
        public string InstalledAppsTitle => IsTurkish ? "Yüklü Uygulamalar ve Disk Kullanımı" : "Installed Applications & Disk Usage";
        public string CloseToDashboard => IsTurkish ? "✕ Kapat (Dashboard'a Dön)" : "✕ Close (Return to Dashboard)";
        public string Refresh => IsTurkish ? "Yenile" : "Refresh";
        public string RefreshAppsToolTip => IsTurkish ? "Yüklü Uygulamaları Yeniden Tara" : "Rescan Installed Applications";
        public string ColAppName => IsTurkish ? "Uygulama Adı" : "Application Name";
        public string ColPublisher => IsTurkish ? "Yayıncı" : "Publisher";
        public string ColVersion => IsTurkish ? "Sürüm" : "Version";
        public string ColLastRun => IsTurkish ? "En Son Çalıştırma" : "Last Run";
        public string ColAppSize => IsTurkish ? "Kapladığı Alan" : "Disk Size";
        public string ColAppPercent => IsTurkish ? "Oran" : "Ratio";
        public string Unknown => IsTurkish ? "Bilinmiyor" : "Unknown";
        public string AppsFoundFormat => IsTurkish ? "Toplam {0} uygulama bulundu." : "Total {0} applications found.";
        public string AppsCountFormat => IsTurkish ? "({0} Uygulama)" : "({0} Apps)";

        // Top Files View
        public string TopFilesTitle => IsTurkish ? "En Çok Yer Kaplayan 100 Dosya (Top Files)" : "Top 100 Largest Files";
        public string FilesCountFormat => IsTurkish ? "({0} Dosya)" : "({0} Files)";
        public string ColRank => "#";
        public string ColFileName => IsTurkish ? "Dosya Adı" : "File Name";
        public string ColPath => IsTurkish ? "Klasör Konumu" : "Folder Location";
        public string ColExt => IsTurkish ? "Uzantı" : "Extension";
        public string ColLastModified => IsTurkish ? "Son Değiştirilme" : "Last Modified";
        public string ActionOpenFolder => IsTurkish ? "Klasörde Aç" : "Open in Folder";

        // Large Files View
        public string LargeFilesTitle => IsTurkish ? "1 GB'tan Büyük Dosyalar (Large Files)" : "Files Larger than 1 GB";

        // Settings Modal
        public string SettingsTitle => IsTurkish ? "Ayarlar" : "Settings";
        public string LanguageLabel => IsTurkish ? "Dil / Language" : "Language / Dil";
        public string LanguageDesc => IsTurkish ? "Uygulama arayüz dilini anında değiştirir." : "Instantly switches the application interface language.";
        public string HideSystemFolders => IsTurkish ? "Sağ blok haritasında (Treemap) sistem klasörlerini gizle" : "Hide system folders in Treemap";
        public string HideSystemFoldersDesc => IsTurkish ? "Windows, System Volume Information, $Recycle.Bin vb. bloklarını grafikten kaldırır." : "Removes Windows, System Volume Information, $Recycle.Bin from charts.";
        public string UseBalancedScale => IsTurkish ? "Treemap'te Dengeli (Dinamik) Ölçekleme Kullan" : "Use Balanced (Dynamic) Scaling in Treemap";
        public string UseBalancedScaleDesc => IsTurkish ? "Çok büyük klasörlerin tüm alanı kaplamasını dengeler; küçük ve orta boy klasörlerin rahat okunmasını sağlar." : "Balances huge directories so smaller folders remain readable.";
        public string RunAsAdmin => IsTurkish ? "Yönetici Yetkisiyle Çalıştır" : "Run as Administrator";
        public string RunAsAdminDesc => IsTurkish ? "Kilitli tüm sistem dosyalarını eksiksiz taramak için gereklidir." : "Required to scan locked system files without access errors.";
        public string RunAsAdminBtn => IsTurkish ? "Yönetici Yap" : "Restart as Admin";
        public string CloseBtn => IsTurkish ? "Kapat" : "Close";

        // Status Bar
        public string StatusReady => IsTurkish ? "Hazır" : "Ready";
        public string StatusScanningFormat => IsTurkish ? "Taranıyor... {0} dosya, {1} klasör bulundu" : "Scanning... {0} files, {1} folders found";
        public string StatusCompleteFormat => IsTurkish ? "Tarama tamamlandı: {0} dosya, {1} klasör ({2})" : "Scan completed: {0} files, {1} folders ({2})";
    }
}
