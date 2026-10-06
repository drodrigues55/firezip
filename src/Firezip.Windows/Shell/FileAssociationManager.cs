using Microsoft.Win32;

namespace Firezip.Windows.Shell;

/// <summary>
/// Registers and manages per-user Windows file associations and ProgIDs.
/// Operates under HKCU so no administrative elevation is required.
/// </summary>
public static class FileAssociationManager
{
    private const string ProgIdPrefix = "Firezip.Archive";

    public static readonly string[] DefaultExtensions =
    [
        ".zip", ".zipx", ".jar", ".apk", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".tbz2", ".txz"
    ];

    private static readonly string[] LegacyExtensions = [".iso", ".cab"];

    public static void RegisterAssociations(string executablePath, IEnumerable<string>? extensions = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            return;

        var extList = extensions ?? DefaultExtensions;

        foreach (var ext in extList)
        {
            var cleanExt = ext.StartsWith('.') ? ext : "." + ext;
            var progId = $"{ProgIdPrefix}{cleanExt}";

            try
            {
                // Register ProgID in HKCU\Software\Classes\Firezip.Archive.<ext>
                using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    progIdKey.SetValue(string.Empty, $"Firezip {cleanExt.ToUpperInvariant().TrimStart('.')} Archive");

                    using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
                    iconKey.SetValue(string.Empty, $"\"{executablePath}\",0");

                    using var commandKey = progIdKey.CreateSubKey(@"shell\open\command");
                    commandKey.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
                }

                // Register OpenWithProgids for the extension
                using (var openWithKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{cleanExt}\OpenWithProgids"))
                {
                    openWithKey.SetValue(progId, string.Empty);
                }
            }
            catch
            {
                // Ignore registry access permissions errors
            }
        }
    }

    public static void UnregisterAssociations(IEnumerable<string>? extensions = null)
    {
        var extList = extensions ?? DefaultExtensions.Concat(LegacyExtensions);

        foreach (var ext in extList)
        {
            var cleanExt = ext.StartsWith('.') ? ext : "." + ext;
            var progId = $"{ProgIdPrefix}{cleanExt}";

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{progId}", false);

                using var openWithKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{cleanExt}\OpenWithProgids", true);
                openWithKey?.DeleteValue(progId, false);
            }
            catch
            {
                // Ignore registry cleanup errors
            }
        }
    }

    public static bool IsAssociated(string extension)
    {
        var cleanExt = extension.StartsWith('.') ? extension : "." + extension;
        var progId = $"{ProgIdPrefix}{cleanExt}";

        try
        {
            using var progIdKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}");
            return progIdKey != null;
        }
        catch
        {
            return false;
        }
    }
}
