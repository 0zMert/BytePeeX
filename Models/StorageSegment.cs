using System;

namespace Folderize.Models
{
    public class StorageSegment
    {
        public string Name { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public double Percentage { get; set; }
        public string ColorHex { get; set; } = "#00C0EF";
        public string FormattedSize => FileSystemNode.FormatBytes(SizeBytes);
        public string FormattedPercentage => $"{Percentage:F1}%";
    }
}
