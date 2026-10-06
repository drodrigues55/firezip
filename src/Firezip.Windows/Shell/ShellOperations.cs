using System.Diagnostics;

namespace Firezip.Windows.Shell;

public static class ShellOperations
{
    /// <summary>
    /// Opens the specified folder in Windows Explorer and selects the item if it's a file.
    /// </summary>
    public static void OpenFolderAndSelect(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            if (File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", $"\"{path}\"");
            }
        }
        catch
        {
            // Non-critical shell launch failure
        }
    }

    /// <summary>
    /// Opens the specified file with its default registered Windows application.
    /// </summary>
    public static void OpenFileWithDefaultProgram(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // Non-critical file launch failure
        }
    }
}
