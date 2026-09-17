using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BytePeeX.Services
{
    public class LocalizationService : INotifyPropertyChanged
    {
        public static LocalizationService Instance { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _currentLanguage = "tr";

        public LocalizationService()
        {
            try
            {
                string savedLang = SettingsService.Instance.Settings.Language;
                if (savedLang == "tr" || savedLang == "en")
                {
                    _currentLanguage = savedLang;
                }
                else
                {
                    string systemLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                    _currentLanguage = systemLang == "tr" ? "tr" : "en";
                    SettingsService.Instance.Settings.Language = _currentLanguage;
                    SettingsService.Instance.Save();
                }
            }
            catch
            {
                _currentLanguage = "tr";
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
                    SettingsService.Instance.Settings.Language = value;
                    SettingsService.Instance.Save();
                    OnPropertyChanged(string.Empty);
                }
            }
        }

        public bool IsTurkish => _currentLanguage == "tr";
        public bool IsEnglish => _currentLanguage == "en";

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
        public string ColAction => IsTurkish ? "İşlem" : "Action";
        public string ExpandLevel => IsTurkish ? "+1 Seviye Genişlet" : "+1 Expand Level";
        public string CollapseLevel => IsTurkish ? "-1 Seviye Daralt" : "-1 Collapse Level";
        public string ExpandAll => IsTurkish ? "Tümünü Genişlet" : "Expand All";
        public string CollapseAll => IsTurkish ? "Tümünü Daralt" : "Collapse All";

        // Context Menu
        public string MenuSendToRecycleBin => IsTurkish ? "🗑️ Geri Dönüşüm Kutusuna Gönder (Sil)" : "🗑️ Send to Recycle Bin (Delete)";
        public string MenuOpenInExplorer => IsTurkish ? "📂 Dosya Gezgini ile Aç" : "📂 Open in File Explorer";
        public string MenuCopyPath => IsTurkish ? "📋 Yolu Kopyala" : "📋 Copy Path";

        // System Folder Badge
        public string SystemBadge => IsTurkish ? "Sistem" : "System";
        public string SystemBadgeToolTip => IsTurkish 
            ? "İşletim Sistemi Klasörü - Windows için gereklidir, değiştirilmesi veya silinmesi önerilmez." 
            : "Operating System Folder - Required for Windows, modifying or deleting is not recommended.";

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

        // Lower Tabs (Summary, Details, Files)
        public string TabSummary => IsTurkish ? "Özet" : "Summary";
        public string TabDetails => IsTurkish ? "Ayrıntılar" : "Details";
        public string TabFiles => IsTurkish ? "Dosyalar" : "Files";

        // Cards & File Types
        public string CardFolderInfo => IsTurkish ? "Klasör Bilgisi" : "Folder Information";
        public string CardTop5Folders => IsTurkish ? "En Büyük 5 Klasör" : "Top 5 Largest Folders";
        public string CardFileTypes => IsTurkish ? "Dosya Türleri" : "File Types";
        public string LabelName => IsTurkish ? "Ad" : "Name";
        public string LabelPath => IsTurkish ? "Yol" : "Path";
        public string LabelSize => IsTurkish ? "Boyut" : "Size";
        public string LabelFiles => IsTurkish ? "Dosyalar" : "Files";
        public string LabelFolders => IsTurkish ? "Klasörler" : "Folders";
        public string LabelModified => IsTurkish ? "Değiştirilme" : "Modified";
        public string LabelOther => IsTurkish ? "Diğer" : "Other";
        public string LabelNoExtension => IsTurkish ? "[Uzantısız]" : "[No Extension]";
        public string FilesCountFormatSuffix => IsTurkish ? "{0:N0} dosya" : "{0:N0} files";

        // Details Tab
        public string DetailsBasicInfo => IsTurkish ? "Temel Bilgiler" : "Basic Information";
        public string DetailsFullPath => IsTurkish ? "Tam Yol:" : "Full Path:";
        public string DetailsActualSize => IsTurkish ? "Gerçek Boyut:" : "Actual Size:";
        public string DetailsAllocatedSize => IsTurkish ? "Diskte Ayrılan Alan (Küme):" : "Allocated Space (Cluster):";
        public string DetailsFileTimeStats => IsTurkish ? "Dosya ve Zaman İstatistikleri" : "File & Time Statistics";
        public string DetailsFileCount => IsTurkish ? "Dosya Sayısı:" : "File Count:";
        public string DetailsSubfolderCount => IsTurkish ? "Alt Klasör Sayısı:" : "Subfolder Count:";
        public string DetailsLastModified => IsTurkish ? "Son Değiştirilme:" : "Last Modified:";
        public string DetailsStatusHierarchy => IsTurkish ? "Durum ve Hiyerarşi" : "Status & Hierarchy";
        public string DetailsHierarchyDepth => IsTurkish ? "Hiyerarşi Derinliği (Level):" : "Hierarchy Depth (Level):";
        public string DetailsRatioToParent => IsTurkish ? "Üst Klasöre Oranı:" : "Ratio to Parent:";
        public string DetailsIsSystemFolder => IsTurkish ? "Sistem Klasörü mü?:" : "Is System Folder?:";

        // Files Tab
        public string FilesInFolderTitle => IsTurkish ? "Klasör İçindeki Dosyalar" : "Files in Folder";
        public string FilesInFolderCountFormat => IsTurkish ? " ({0} Dosya)" : " ({0} Files)";

        // Visualization Toolbar & Controls
        public string VisualizationLabel => IsTurkish ? "Görselleştirme:" : "Visualization:";
        public string BtnTreemap => IsTurkish ? "📊 Treemap (Harita)" : "📊 Treemap";
        public string BtnSunburst => IsTurkish ? "☀️ Sunburst (Halka)" : "☀️ Sunburst";
        public string BtnHeatmap => IsTurkish ? "🔥 Isı Haritası (Kartlar)" : "🔥 Heatmap";
        public string BtnUpLevel => IsTurkish ? "▲ Üst Dizin" : "▲ Up Level";
        public string BtnBack => IsTurkish ? "◀ Geri" : "◀ Back";
        public string TreemapScaleBalanced => IsTurkish ? "⚖ Dengeli Görünüm" : "⚖ Balanced View";
        public string TreemapScaleExact => IsTurkish ? "📏 Gerçek Oran (1:1)" : "📏 Exact Ratio (1:1)";
        public string TreemapScaleTooltip => IsTurkish
            ? "Büyük klasörleri dengeler; küçük klasörlerin rahat okunmasını sağlar. Gerçek orana geçmek için tıklayın."
            : "Balances large folders so smaller folders remain visible. Click to toggle exact ratio.";
        public string DoubleTapUpLevel => IsTurkish ? "Çift tık: Üst Dizin" : "Double-click: Up Level";

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
        public string SizeFilterLabel => IsTurkish ? "Boyut Filtresi:" : "Size Filter:";

        // Settings Modal
        public string SettingsTitle => IsTurkish ? "Ayarlar" : "Settings";
        public string LanguageLabel => IsTurkish ? "Dil / Language" : "Language / Dil";
        public string LanguageDesc => IsTurkish ? "Uygulama arayüz dilini anında değiştirir." : "Instantly switches the application interface language.";
        public string HideSystemFolders => IsTurkish ? "Sağ blok haritasında (Treemap) sistem klasörlerini gizle" : "Hide system folders in Treemap";
        public string HideSystemFoldersDesc => IsTurkish ? "Windows, System Volume Information, $Recycle.Bin vb. bloklarını grafikten kaldırır." : "Removes Windows, System Volume Information, $Recycle.Bin from charts.";
        public string UseBalancedScale => IsTurkish ? "Treemap'te Dengeli (Dinamik) Ölçekleme Kullan" : "Use Balanced (Dynamic) Scaling in Treemap";
        public string UseBalancedScaleDesc => IsTurkish ? "Çok büyük klasörlerin tüm alanı kaplamasını dengeler; küçük ve orta boy klasörlerin rahat okunmasını sağlar." : "Balances huge directories so smaller folders remain readable.";
        // Admin Option
        public string RunAsAdmin => IsTurkish ? "Yönetici Yetkisiyle Çalıştır" : "Run as Administrator";
        public string RunAsAdminDesc => IsTurkish ? "Kilitli tüm sistem dosyalarını eksiksiz taramak için gereklidir." : "Required to scan locked system files without access errors.";
        public string RunAsAdminBtn => IsTurkish ? "Yönetici Yap" : "Restart as Admin";
        public string RunAsAdminActive => IsTurkish ? "Yönetici Yetkisiyle Çalışıyor" : "Running as Administrator";
        public string RunAsAdminActiveDesc => IsTurkish ? "BytePeeX şu anda en yüksek yönetici yetkileriyle çalışıyor. Kilitli sistem dosyaları dahil tüm disk eksiksiz taranabilir." : "BytePeeX is running with administrator privileges. All directories including locked system files can be scanned.";
        public string AdminActive => IsTurkish ? "✓ Aktif (Yönetici)" : "✓ Active (Admin)";
        public string AdminBadge => IsTurkish ? "YÖNETİCİ" : "ADMIN";
        public string CloseBtn => IsTurkish ? "Kapat" : "Close";
        public string SaveSettingsBtn => IsTurkish ? "Ayarları Kaydet" : "Save Settings";
        public string SettingsSavedNotice => IsTurkish ? "✓ Seçili ayarlar başarıyla kaydedildi" : "✓ Selected settings saved successfully";

        // Status Bar
        public string StatusReady => IsTurkish ? "Hazır" : "Ready";
        public string StatusInitial => IsTurkish ? "Hazır. Lütfen taranacak bir dizin veya sürücü seçin." : "Ready. Please select a folder or drive to scan.";
        public string StatusStarting => IsTurkish ? "Tarama başlatılıyor..." : "Starting scan...";
        public string StatusInvalidPath => IsTurkish ? "Geçersiz dizin yolu!" : "Invalid folder path!";
        public string StatusScanningFormat => IsTurkish ? "Taranıyor: {0:N0} dosya ({1})" : "Scanning: {0:N0} files ({1})";
        public string StatusCompleteFormat => IsTurkish ? "Tarama tamamlandı ({0:F1} sn). Toplam: {1}, {2:N0} dosya, {3:N0} klasör." : "Scan completed ({0:F1} s). Total: {1}, {2:N0} files, {3:N0} folders.";
        public string StatusStoppedFormat => IsTurkish ? "Tarama durduruldu (taranan kısım): {0}, {1:N0} dosya, {2:N0} klasör." : "Scan stopped (partial): {0}, {1:N0} files, {2:N0} folders.";
        public string StatusStoppedByUser => IsTurkish ? "Tarama kullanıcı tarafından durduruldu." : "Scan stopped by user.";
        public string StatusScanError => IsTurkish ? "Tarama sırasında hata oluştu: {0}" : "Error occurred during scan: {0}";
        public string StatusMovedToRecycleBin => IsTurkish ? "{0} öğe ({1}) Geri Dönüşüm Kutusu'na taşındı." : "{0} items ({1}) moved to Recycle Bin.";
        public string StatusTotal => IsTurkish ? "Toplam" : "Total";
        public string StatusFiles => IsTurkish ? "Dosyalar" : "Files";
        public string StatusFolders => IsTurkish ? "Klasörler" : "Folders";

        // Splash Screen Loading Steps
        public string SplashStep1 => IsTurkish ? "Sistem çekirdeği başlatılıyor..." : "Initializing system core...";
        public string SplashStep2 => IsTurkish ? "Fiziksel disk sürücüleri taranıyor..." : "Detecting physical disk drives...";
        public string SplashStep3 => IsTurkish ? "Yüklü uygulamalar ve dizin mimarisi taranıyor..." : "Loading installed apps and directory structure...";
        public string SplashStep4 => IsTurkish ? "Arayüz teması ve grafik motoru hazırlanıyor..." : "Preparing UI theme and rendering engine...";
        public string SplashReady => IsTurkish ? "BytePeeX hazır!" : "BytePeeX ready!";
        public string SplashSubtitle => IsTurkish ? "Disk Alanı ve Klasör Analiz Aracı" : "Disk & Folder Size Analyzer";

        // Folder Picker Window
        public string FolderPickerTitle => IsTurkish ? "Taranacak Konumu Seçin" : "Select Location to Scan";
        public string FolderPickerSubtitle => IsTurkish ? "Hemen taramak istediğiniz sürücüyü veya sık kullanılan bir klasörü tıklayın" : "Click a drive or frequently used folder to start scanning immediately";
        public string LocalDrivesTitle => IsTurkish ? "💾 YEREL SÜRÜCÜLER" : "💾 LOCAL DRIVES";
        public string LocalDrivesSubtitle => IsTurkish ? "Doğrudan tüm sürücüyü tarar" : "Scans the entire drive directly";
        public string QuickAccessTitle => IsTurkish ? "⚡ HIZLI ERİŞİM KLASÖRLERİ" : "⚡ QUICK ACCESS FOLDERS";
        public string FolderDesktop => IsTurkish ? "Masaüstü (Desktop)" : "Desktop";
        public string FolderDownloads => IsTurkish ? "İndirilenler (Downloads)" : "Downloads";
        public string FolderDocuments => IsTurkish ? "Belgeler (Documents)" : "Documents";
        public string FolderUserProfile => IsTurkish ? "Kullanıcı Klasörü (Home)" : "User Folder (Home)";
        public string Cancel => IsTurkish ? "İptal" : "Cancel";
        public string BrowseOtherFolder => IsTurkish ? "Başka Bir Klasör Seç... (Gözat)" : "Browse Another Folder...";
    }
}
