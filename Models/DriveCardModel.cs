using System;

namespace Folderize.Models
{
    public class DriveCardModel
    {
        public string DriveName { get; set; } = string.Empty;
        public long TotalSizeBytes { get; set; }
        public long UsedSizeBytes { get; set; }
        public long FreeSizeBytes { get; set; }
        public double UsedPercent { get; set; }
        public bool IsSelected { get; set; }
        public bool IsRemovable { get; set; }

        public string DisplayText => $"{DriveName}   {FileSystemNode.FormatBytes(UsedSizeBytes)} / {FileSystemNode.FormatBytes(TotalSizeBytes)} ({FileSystemNode.FormatBytes(FreeSizeBytes)} boş)";
        public string FormattedTotal => FileSystemNode.FormatBytes(TotalSizeBytes);
        public string FormattedUsed => FileSystemNode.FormatBytes(UsedSizeBytes);
        public string FormattedFree => FileSystemNode.FormatBytes(FreeSizeBytes);
        public string FormattedFreeText => $"{FormattedFree} boş";
        public string UsageSummary => $"{FormattedUsed} used · {FormattedFree} free";
    }
}
