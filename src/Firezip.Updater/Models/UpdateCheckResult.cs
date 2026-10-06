using System.Text.Json.Serialization;

namespace Firezip.Updater.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,
    Downloading,
    DownloadedAndVerified,
    ReadyToInstall,
    InstalledSuccessfully,
    PendingRestart,
    Failed,
    OfflineOrNetworkError,
    SignatureInvalid,
    HashMismatch,
    DowngradeRejected,
    IncompatibleVersion
}

public class UpdateCheckResult
{
    [JsonPropertyName("status")]
    public UpdateStatus Status { get; set; } = UpdateStatus.UpToDate;

    [JsonIgnore]
    public bool IsUpdateAvailable => Status == UpdateStatus.UpdateAvailable;

    [JsonPropertyName("currentVersion")]
    public string CurrentVersion { get; set; } = string.Empty;

    [JsonPropertyName("latestVersion")]
    public string LatestVersion { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("minimumSupportedVersion")]
    public string? MinimumSupportedVersion { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
