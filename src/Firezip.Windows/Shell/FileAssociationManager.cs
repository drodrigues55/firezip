using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Firezip.Windows.Shell;

/// <summary>
/// Registers and manages Windows file associations, ProgIDs, and Windows Default Programs Capabilities.
/// Operates under HKCU so no administrative elevation is required.
/// </summary>
public static class FileAssociationManager
{
    public const string MainProgId = "Firezip.Archive";
    private const string ProgIdPrefix = "Firezip.Archive";

    public static readonly string[] DefaultExtensions =
    [
        ".zip", ".zipx", ".jar", ".apk", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".tbz2", ".txz"
    ];

    private static readonly string[] LegacyExtensions = [".iso", ".cab"];

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    /// <summary>
    /// Notifies Windows Explorer shell that file associations and icons have changed.
    /// </summary>
    public static void NotifyShellAssociationChanged()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Shell notification failure ignored
        }
    }

    /// <summary>
    /// Opens the Windows Settings page for Default Apps so the user can assign Firezip as default.
    /// </summary>
    public static void OpenDefaultAppsSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch
        {
            // Fallback ignored
        }
    }

    public static void RegisterAssociations(string executablePath, IEnumerable<string>? extensions = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            return;

        var extList = (extensions ?? DefaultExtensions).ToList();
        var exeName = Path.GetFileName(executablePath);

        try
        {
            // 1. Register main ProgID: HKCU\Software\Classes\Firezip.Archive
            using (var mainKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{MainProgId}"))
            {
                mainKey.SetValue(string.Empty, "Firezip Archive");

                using var iconKey = mainKey.CreateSubKey("DefaultIcon");
                iconKey.SetValue(string.Empty, $"\"{executablePath}\",0");

                using var openKey = mainKey.CreateSubKey(@"shell\open");
                openKey.SetValue(string.Empty, "Open with Firezip");
                openKey.SetValue("Icon", $"\"{executablePath}\",0");

                using var cmdKey = openKey.CreateSubKey("command");
                cmdKey.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");

                // Dedicated verbs
                using var hereKey = mainKey.CreateSubKey(@"shell\ExtractHere");
                hereKey.SetValue(string.Empty, "Extract Here");
                hereKey.SetValue("Icon", $"\"{executablePath}\",0");
                using var hereCmd = hereKey.CreateSubKey("command");
                hereCmd.SetValue(string.Empty, $"\"{executablePath}\" --extract-here \"%1\"");

                using var toFolderKey = mainKey.CreateSubKey(@"shell\ExtractToFolder");
                toFolderKey.SetValue(string.Empty, "Extract to Folder");
                toFolderKey.SetValue("Icon", $"\"{executablePath}\",0");
                using var toFolderCmd = toFolderKey.CreateSubKey("command");
                toFolderCmd.SetValue(string.Empty, $"\"{executablePath}\" --extract-to-folder \"%1\"");

                using var toKey = mainKey.CreateSubKey(@"shell\ExtractTo");
                toKey.SetValue(string.Empty, "Extract to...");
                toKey.SetValue("Icon", $"\"{executablePath}\",0");
                using var toCmd = toKey.CreateSubKey("command");
                toCmd.SetValue(string.Empty, $"\"{executablePath}\" --extract-to \"%1\"");
            }

            // 2. Register Windows Default Programs Capabilities in HKCU
            using (var capKey = Registry.CurrentUser.CreateSubKey(@"Software\Firezip\Capabilities"))
            {
                capKey.SetValue("ApplicationName", "Firezip");
                capKey.SetValue("ApplicationDescription", "Firezip Archive Manager");

                using var capAssocKey = capKey.CreateSubKey("FileAssociations");
                foreach (var ext in extList)
                {
                    var cleanExt = ext.StartsWith('.') ? ext : "." + ext;
                    capAssocKey.SetValue(cleanExt, MainProgId);
                }
            }

            using (var regAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                regAppsKey.SetValue("Firezip", @"Software\Firezip\Capabilities");
            }

            // 3. Register Applications\<exe>
            using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{exeName}"))
            {
                appKey.SetValue("FriendlyAppName", "Firezip");

                using var appIcon = appKey.CreateSubKey("DefaultIcon");
                appIcon.SetValue(string.Empty, $"\"{executablePath}\",0");

                using var appCmd = appKey.CreateSubKey(@"shell\open\command");
                appCmd.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");

                using var appTypes = appKey.CreateSubKey("SupportedTypes");
                foreach (var ext in extList)
                {
                    var cleanExt = ext.StartsWith('.') ? ext : "." + ext;
                    appTypes.SetValue(cleanExt, string.Empty);
                }
            }

            // 4. Register extensions and OpenWithProgids
            foreach (var ext in extList)
            {
                var cleanExt = ext.StartsWith('.') ? ext : "." + ext;
                var progId = $"{ProgIdPrefix}{cleanExt}";

                // Extension-specific ProgID
                using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    progIdKey.SetValue(string.Empty, $"Firezip {cleanExt.ToUpperInvariant().TrimStart('.')} Archive");

                    using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
                    iconKey.SetValue(string.Empty, $"\"{executablePath}\",0");

                    using var commandKey = progIdKey.CreateSubKey(@"shell\open\command");
                    commandKey.SetValue(string.Empty, $"\"{executablePath}\" \"%1\"");
                }

                // Extension key
                using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{cleanExt}"))
                {
                    extKey.SetValue(string.Empty, MainProgId);
                }

                // OpenWithProgids for Explorer
                using (var openWithKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{cleanExt}\OpenWithProgids"))
                {
                    openWithKey.SetValue(MainProgId, string.Empty);
                    openWithKey.SetValue(progId, string.Empty);
                }
            }

            NotifyShellAssociationChanged();
        }
        catch
        {
            // Ignore registry access permissions errors
        }
    }

    public static void UnregisterAssociations(IEnumerable<string>? extensions = null)
    {
        var extList = (extensions ?? DefaultExtensions.Concat(LegacyExtensions)).ToList();

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{MainProgId}", false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Firezip\Capabilities", false);

            using (var regAppsKey = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            {
                regAppsKey?.DeleteValue("Firezip", false);
            }

            foreach (var ext in extList)
            {
                var cleanExt = ext.StartsWith('.') ? ext : "." + ext;
                var progId = $"{ProgIdPrefix}{cleanExt}";

                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{progId}", false);

                using var openWithKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{cleanExt}\OpenWithProgids", true);
                openWithKey?.DeleteValue(MainProgId, false);
                openWithKey?.DeleteValue(progId, false);
            }

            NotifyShellAssociationChanged();
        }
        catch
        {
            // Ignore registry cleanup errors
        }
    }

    public static bool IsAssociated(string extension)
    {
        var cleanExt = extension.StartsWith('.') ? extension : "." + extension;

        try
        {
            using var mainKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{MainProgId}");
            if (mainKey != null) return true;

            var progId = $"{ProgIdPrefix}{cleanExt}";
            using var progIdKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}");
            return progIdKey != null;
        }
        catch
        {
            return false;
        }
    }
}
