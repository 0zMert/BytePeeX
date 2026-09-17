using System;

namespace Folderize.Models
{
    public class InstalledAppModel
    {
        public string Name { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string InstallLocation { get; set; } = string.Empty;
        public double PercentOfTotal { get; set; }
        public DateTime? LastRunTime { get; set; }

        public string FormattedSize => SizeBytes > 0 ? FileSystemNode.FormatBytes(SizeBytes) : "-";
        public string FormattedPercent => PercentOfTotal > 0 ? $"{PercentOfTotal:F1}%" : "";

        public string FormattedLastRun => LastRunTime.HasValue && LastRunTime.Value > DateTime.MinValue
            ? LastRunTime.Value.ToString("dd.MM.yyyy HH:mm")
            : "Bilinmiyor";

        public string RelativeLastRun
        {
            get
            {
                if (!LastRunTime.HasValue || LastRunTime.Value <= DateTime.MinValue)
                    return "Bilinmiyor";

                var diff = DateTime.Now - LastRunTime.Value;
                if (diff.TotalDays < 0) return LastRunTime.Value.ToString("dd.MM.yyyy");
                if (diff.TotalHours < 1) return $"{(int)Math.Max(1, diff.TotalMinutes)} dk önce";
                if (diff.TotalDays < 1) return $"Bugün {LastRunTime.Value:HH:mm}";
                if (diff.TotalDays < 2) return $"Dün {LastRunTime.Value:HH:mm}";
                if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} gün önce";
                if (diff.TotalDays < 365) return $"{(int)(diff.TotalDays / 30)} ay önce";
                return LastRunTime.Value.ToString("dd.MM.yyyy");
            }
        }
    }
}
