namespace Firezip.Core.Models;

public record UpdateInfo
{
    public bool IsUpdateAvailable { get; init; }
    public string LatestVersion { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string ReleaseNotes { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string Status { get; init; } = "UpToDate";
    public string? ErrorMessage { get; init; }
}
