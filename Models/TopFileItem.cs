using System;
using System.IO;

namespace BytePeeX.Models
{
    public class TopFileItem
    {
        public int Rank { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string DirectoryPath => Path.GetDirectoryName(FullPath) ?? "";
        public string Extension
        {
            get
            {
                string ext = Path.GetExtension(FullPath);
                return string.IsNullOrEmpty(ext) ? Services.LocalizationService.Instance.LabelNoExtension : ext.ToLowerInvariant();
            }
        }
        public long SizeBytes { get; set; }
        public double PercentOfTotal { get; set; }
        public DateTime? LastModified { get; set; }

        public string FormattedSize => FileSystemNode.FormatBytes(SizeBytes);
        public string FormattedLastModified => LastModified.HasValue ? LastModified.Value.ToString("dd.MM.yyyy HH:mm") : "-";
        public string FormattedPercent => PercentOfTotal > 0 ? $"{PercentOfTotal:F1}%" : "<0.1%";
    }
}
