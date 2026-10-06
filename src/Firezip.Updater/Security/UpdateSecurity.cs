using System.Security.Cryptography;
using System.Text;
using Firezip.Updater.Models;

namespace Firezip.Updater.Security;

/// <summary>
/// Cryptographic validation and security policy enforcement for software updates.
/// Enforces HTTPS, SHA-256 hashes, RSA digital signatures, downgrade prevention, and version constraints.
/// </summary>
public static class UpdateSecurity
{
    /// <summary>
    /// Default embedded RSA 2048-bit Public Key for Firezip release verification.
    /// The corresponding private key is strictly kept external in release engineering pipelines.
    /// </summary>
    public const string DefaultPublicKeyXml =
        "<RSAKeyValue><Modulus>2+WfBWlsSHRFvia0hYNxINfn/uZdbbGEfXU0lbwXGCUJCTakh9mnpCuX1LxSOZjvJ4XShnSdhB6UG2A/CR70SXolHaCNS574SkKANa53INQNpRrbqZbtvVs+KoqqOfoHioaLFXfsVBUcfm0REoWIH135NfQRv0LwOw4nB9mFO54NGv+LeekZM+RoMNFThw8iKmJUWhKog7QRIUe+LdPsSmx5uQ8Se1H2X/mAUWkocv7wWFHBZMaoJc76Gdib653J7UxVXXZdd2Zce/Q2lFybMjDqw8kWGoNbstq5AopxzmNZMppLcMAaV7NuPVO/cFd+gNZm/LiVcaHPr+ksOs72EQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    /// <summary>
    /// Enforces that a given URL strictly uses HTTPS (TLS).
    /// </summary>
    public static bool EnforceHttps(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes the SHA-256 hash of a file on disk and verifies it against the expected hash.
    /// </summary>
    public static bool VerifyFileHash(string filePath, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || string.IsNullOrWhiteSpace(expectedSha256))
        {
            return false;
        }

        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            var computedHex = Convert.ToHexString(hashBytes);

            return string.Equals(computedHex, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies the digital RSA-SHA256 signature of the remote manifest.
    /// The signed payload combines version, SHA256, and download URL.
    /// </summary>
    public static bool VerifyManifestSignature(UpdateManifest manifest, string? customPublicKeyXml = null)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Signature))
        {
            return false;
        }

        try
        {
            var publicKeyXml = !string.IsNullOrWhiteSpace(customPublicKeyXml)
                ? customPublicKeyXml
                : DefaultPublicKeyXml;

            using var rsa = RSA.Create();
            rsa.FromXmlString(publicKeyXml);

            var payload = GetSignablePayload(manifest);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var signatureBytes = Convert.FromBase64String(manifest.Signature.Trim());

            return rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the canonical payload string used for signing and verifying the manifest.
    /// Strictly binds Version, SHA-256, DownloadUrl, MinimumSupportedVersion, and Mandatory.
    /// </summary>
    public static string GetSignablePayload(UpdateManifest manifest)
    {
        var cleanVer = manifest.Version?.TrimStart('v', 'V').Trim() ?? string.Empty;
        var cleanHash = manifest.Sha256?.Trim().ToUpperInvariant() ?? string.Empty;
        var cleanUrl = manifest.DownloadUrl?.Trim() ?? string.Empty;
        var cleanMinVer = manifest.MinimumSupportedVersion?.TrimStart('v', 'V').Trim() ?? string.Empty;
        var isMandatory = manifest.Mandatory ? "true" : "false";
        return $"{cleanVer}|{cleanHash}|{cleanUrl}|{cleanMinVer}|{isMandatory}";
    }

    /// <summary>
    /// Signs a payload string using a private key (for release engineering and automated testing).
    /// </summary>
    public static string SignPayload(string payload, string privateKeyXml)
    {
        using var rsa = RSA.Create();
        rsa.FromXmlString(privateKeyXml);

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signatureBytes = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signatureBytes);
    }

    /// <summary>
    /// Checks whether the target version is a downgrade (or same version) compared to the current version.
    /// Returns true if the target version is less than or equal to the current version.
    /// </summary>
    public static bool IsDowngrade(string currentVersion, string targetVersion)
    {
        if (!TryParseCleanVersion(currentVersion, out var currentVer) ||
            !TryParseCleanVersion(targetVersion, out var targetVer))
        {
            return true; // Reject unrecognized version strings as unsafe
        }

        return targetVer <= currentVer;
    }

    /// <summary>
    /// Checks if current version satisfies minimum supported version constraints.
    /// </summary>
    public static bool IsVersionSupported(string currentVersion, string? minimumSupportedVersion)
    {
        if (string.IsNullOrWhiteSpace(minimumSupportedVersion))
        {
            return true;
        }

        if (!TryParseCleanVersion(currentVersion, out var currentVer) ||
            !TryParseCleanVersion(minimumSupportedVersion, out var minVer))
        {
            return true;
        }

        return currentVer >= minVer;
    }

    /// <summary>
    /// Safely parses clean System.Version objects from tags like "v1.0.11" or "1.0.11-rc1".
    /// </summary>
    public static bool TryParseCleanVersion(string versionString, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(versionString)) return false;

        var clean = versionString.TrimStart('v', 'V').Trim();
        var dashIndex = clean.IndexOf('-');
        if (dashIndex > 0)
        {
            clean = clean[..dashIndex];
        }

        // If version has only 2 parts (e.g. 1.0), append .0
        var parts = clean.Split('.');
        if (parts.Length == 1) clean += ".0.0.0";
        else if (parts.Length == 2) clean += ".0.0";
        else if (parts.Length == 3) clean += ".0";

        return Version.TryParse(clean, out version!);
    }
}
