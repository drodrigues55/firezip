using System.Text.Json.Serialization;

namespace Firezip.Updater.Models;

/// <summary>
/// Remote update manifest schema. Cryptographically signed by the release authority.
/// </summary>
public class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("minimum_supported_version")]
    public string? MinimumSupportedVersion { get; set; }

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; } = false;

    [JsonPropertyName("release_notes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public string? PublishedAt { get; set; }
}
