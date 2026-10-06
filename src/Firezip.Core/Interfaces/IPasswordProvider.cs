namespace Firezip.Core.Interfaces;

/// <summary>
/// Callback provider for requesting passwords from the user when opening or extracting encrypted archives.
/// </summary>
public interface IPasswordProvider
{
    /// <summary>
    /// Prompts the user for a password for the given archive path. Returns null if cancelled.
    /// </summary>
    Task<string?> RequestPasswordAsync(string archiveFilePath, string? contextMessage = null);
}
