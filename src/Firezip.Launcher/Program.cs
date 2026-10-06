using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Firezip.Launcher;

internal static class Program
{
    private const uint MessageBoxIconError = 0x10;
    private const int ShowWindowNormal = 1;
    private const uint GetWindowOwner = 4;
    private static readonly EnumWindowsCallback EnumWindowsHandler = CaptureWindowForProcess;

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.1";
            // Keep versioned installs beside the legacy flat App directory. The old
            // launcher stored runtime/resource subfolders directly in App, so treating
            // those folders as versions could remove files from a running installation.
            var installsDirectory = Path.Combine(localAppData, "Firezip", "Versions");
            var installDirectory = Path.Combine(installsDirectory, version);
            var appPath = Path.Combine(installDirectory, "Firezip.UI.exe");
            var markerPath = Path.Combine(installDirectory, ".launcher-version");

            CleanupUnusedInstallations(installsDirectory, installDirectory);

            var priPath = Path.Combine(installDirectory, "Firezip.UI.pri");
            var needsInstall = !File.Exists(appPath) ||
                !File.Exists(priPath) ||
                !File.Exists(markerPath) ||
                !string.Equals(File.ReadAllText(markerPath).Trim(), version, StringComparison.Ordinal);

            if (needsInstall)
            {
                Directory.CreateDirectory(installDirectory);
                using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("FirezipPayload.zip")
                    ?? throw new InvalidOperationException("O pacote do Firezip não foi encontrado dentro deste executável.");
                ZipFile.ExtractToDirectory(payload, installDirectory, overwriteFiles: true);
                File.WriteAllText(markerPath, version);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = installDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            foreach (var argument in args)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var appProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("O Windows não conseguiu iniciar o Firezip.");

            var window = WaitForMainWindow(appProcess.Id);
            if (window == IntPtr.Zero)
            {
                throw new InvalidOperationException("O Firezip iniciou, mas não criou uma janela visível.");
            }

            ShowWindow(window, ShowWindowNormal);
            SetForegroundWindow(window);
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, ex.Message, "Firezip", MessageBoxIconError);
        }
    }

    private static void CleanupUnusedInstallations(string installsDirectory, string currentInstallDirectory)
    {
        if (!Directory.Exists(installsDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(installsDirectory))
        {
            if (!Version.TryParse(Path.GetFileName(directory), out _))
            {
                continue;
            }

            if (string.Equals(
                    Path.GetFullPath(directory),
                    Path.GetFullPath(currentInstallDirectory),
                    StringComparison.OrdinalIgnoreCase) ||
                IsInstallationInUse(directory))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Cleanup is best effort; a locked or inaccessible previous version is harmless.
            }
        }
    }

    private static bool IsInstallationInUse(string installDirectory)
    {
        var fullDirectory = Path.GetFullPath(installDirectory) + Path.DirectorySeparatorChar;

        foreach (var process in Process.GetProcessesByName("Firezip.UI"))
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(processPath) &&
                    Path.GetFullPath(processPath).StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // If a Firezip process cannot be inspected, preserve old files to avoid deleting a live install.
                return true;
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    private static IntPtr WaitForMainWindow(int processId)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            _windowForProcess = IntPtr.Zero;
            _targetProcessId = processId;
            EnumWindows(EnumWindowsHandler, IntPtr.Zero);
            if (_windowForProcess != IntPtr.Zero)
            {
                return _windowForProcess;
            }

            Thread.Sleep(100);
        }

        return IntPtr.Zero;
    }

    private static IntPtr _windowForProcess;
    private static int _targetProcessId;

    private static bool CaptureWindowForProcess(IntPtr window, IntPtr _)
    {
        GetWindowThreadProcessId(window, out var processId);
        if (processId == (uint)_targetProcessId && GetWindow(window, GetWindowOwner) == IntPtr.Zero)
        {
            _windowForProcess = window;
            return false;
        }

        return true;
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}
