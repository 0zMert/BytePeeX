using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Folderize.Models
{
    public class DriveCardModel : INotifyPropertyChanged
    {
        private string _driveName = string.Empty;
        private long _totalSizeBytes;
        private long _usedSizeBytes;
        private long _freeSizeBytes;
        private double _usedPercent;
        private bool _isSelected;
        private bool _isRemovable;

        public string DriveName
        {
            get => _driveName;
            set
            {
                if (_driveName != value)
                {
                    _driveName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayText));
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
                    NotifyCalculatedProperties();
                }
            }
        }

        public long UsedSizeBytes
        {
            get => _usedSizeBytes;
            set
            {
                if (_usedSizeBytes != value)
                {
                    _usedSizeBytes = value;
                    OnPropertyChanged();
                    NotifyCalculatedProperties();
                }
            }
        }

        public long FreeSizeBytes
        {
            get => _freeSizeBytes;
            set
            {
                if (_freeSizeBytes != value)
                {
                    _freeSizeBytes = value;
                    OnPropertyChanged();
                    NotifyCalculatedProperties();
                }
            }
        }

        public double UsedPercent
        {
            get => _usedPercent;
            set
            {
                if (Math.Abs(_usedPercent - value) > 0.01)
                {
                    _usedPercent = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedPercent));
                    OnPropertyChanged(nameof(BarBrush));
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

        public bool IsRemovable
        {
            get => _isRemovable;
            set
            {
                if (_isRemovable != value)
                {
                    _isRemovable = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DisplayText => $"{DriveName}   {FileSystemNode.FormatBytes(UsedSizeBytes)} / {FileSystemNode.FormatBytes(TotalSizeBytes)} ({FileSystemNode.FormatBytes(FreeSizeBytes)} {(Services.LocalizationService.Instance.IsTurkish ? "boş" : "free")})";
        public string FormattedTotal => FileSystemNode.FormatBytes(TotalSizeBytes);
        public string FormattedUsed => FileSystemNode.FormatBytes(UsedSizeBytes);
        public string FormattedFree => FileSystemNode.FormatBytes(FreeSizeBytes);
        public string FormattedFreeText => $"{FormattedFree} {(Services.LocalizationService.Instance.IsTurkish ? "boş" : "free")}";
        public string UsageSummary => $"{FormattedUsed} {(Services.LocalizationService.Instance.IsTurkish ? "kullanılan" : "used")} · {FormattedFree} {(Services.LocalizationService.Instance.IsTurkish ? "boş" : "free")}";
        public string FormattedPercent => $"{UsedPercent:F0}%";
        public string BarBrush => UsedPercent >= 90.0 ? "#EF4444" : "#38BDF8";

        public void NotifyCalculatedProperties()
        {
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(FormattedTotal));
            OnPropertyChanged(nameof(FormattedUsed));
            OnPropertyChanged(nameof(FormattedFree));
            OnPropertyChanged(nameof(FormattedFreeText));
            OnPropertyChanged(nameof(UsageSummary));
            OnPropertyChanged(nameof(FormattedPercent));
            OnPropertyChanged(nameof(BarBrush));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
