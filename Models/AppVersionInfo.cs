using System;
using System.Text.Json.Serialization;

namespace ShopHoa.Models;

/// <summary>
/// Thông tin siêu dữ liệu (metadata) của một phiên bản cập nhật phần mềm OTA.
/// </summary>
public sealed class AppVersionInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("release_notes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("file_size")]
    public long? FileSize { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }

    [JsonPropertyName("minimum_supported_version")]
    public string? MinimumSupportedVersion { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    /// <summary>
    /// Định dạng dung lượng hiển thị dạng thân thiện (KB, MB, GB).
    /// </summary>
    [JsonIgnore]
    public string FileSizeFormatted
    {
        get
        {
            if (!FileSize.HasValue || FileSize.Value <= 0)
                return "Không xác định";

            double bytes = FileSize.Value;
            if (bytes >= 1024 * 1024 * 1024)
                return $"{bytes / (1024 * 1024 * 1024):0.##} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024 * 1024):0.##} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024:0.##} KB";

            return $"{bytes:N0} B";
        }
    }
}
