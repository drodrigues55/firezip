namespace Firezip.Core.Interfaces;

/// <summary>
/// Provides localized UI string lookups and dynamic culture switching.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets or sets the active culture code (e.g. "en-US", "pt-BR", or "System").
    /// </summary>
    string CurrentCulture { get; set; }

    /// <summary>
    /// Supported culture identifiers.
    /// </summary>
    IReadOnlyList<string> SupportedCultures { get; }

    /// <summary>
    /// Resolves the localized string for the specified key.
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Resolves and formats the localized string for the specified key with arguments.
    /// </summary>
    string GetString(string key, params object[] args);

    /// <summary>
    /// Indexer for quick string lookup.
    /// </summary>
    string this[string key] { get; }

    /// <summary>
    /// Fired when the active culture is switched.
    /// </summary>
    event EventHandler? CultureChanged;
}
