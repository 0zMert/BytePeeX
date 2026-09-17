namespace BytePeeX.Models
{
    public class FileExtensionSummary
    {
        public string Extension { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public double Percentage { get; set; }
        public string ColorHex { get; set; } = "#38BDF8";

        public string FormattedSize => FileSystemNode.FormatBytes(TotalSizeBytes);
        public string FormattedCount => Services.LocalizationService.Instance.IsTurkish ? $"{FileCount:N0} dosya" : $"{FileCount:N0} files";
        public string FormattedPercent => $"{Percentage:F1}%";
    }
}
