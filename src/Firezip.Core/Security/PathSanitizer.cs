namespace Firezip.Core.Security;

/// <summary>
/// Hardened security validator preventing Zip Slip, path traversal, UNC escapes,
/// drive-root injection, Windows reserved device names, null bytes, and reparse point attacks.
/// </summary>
public static class PathSanitizer
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Windows reserved device names that must never appear as filenames or path components.
    /// Writing to these names on Windows silently targets the device, not a file.
    /// </summary>
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Canonicalizes the destination directory ensuring it ends with a separator.
    /// </summary>
    public static string NormalizeDirectory(string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var fullPath = Path.GetFullPath(destinationDirectory);
        if (!fullPath.EndsWith(Path.DirectorySeparatorChar) && !fullPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            fullPath += Path.DirectorySeparatorChar;
        }
        return fullPath;
    }

    /// <summary>
    /// Validates and resolves an archive entry path against a target destination directory.
    /// Throws <see cref="SecurityValidationException"/> if any traversal, injection, or illegal
    /// name pattern is detected.
    /// </summary>
    public static string GetSafeExtractionPath(string destinationDirectory, string entryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            throw new SecurityValidationException(
                "Archive entry path cannot be empty.",
                entryPath ?? string.Empty,
                "EmptyEntryPath");
        }

        // Block null bytes — can truncate paths on some filesystems/implementations
        if (entryPath.Contains('\0'))
        {
            throw new SecurityValidationException(
                $"Null byte detected in archive entry path: '{entryPath}'.",
                entryPath,
                "NullByteInPath");
        }

        var normalizedDest = NormalizeDirectory(destinationDirectory);

        // Normalize slashes to forward slashes for uniform inspection
        var cleaned = entryPath.Replace('\\', '/');

        // Block UNC paths (e.g. "//server/share" or "\\server\share")
        if (cleaned.StartsWith("//") || entryPath.StartsWith(@"\\"))
        {
            throw new SecurityValidationException(
                $"UNC network path detected in archive entry: '{entryPath}'. Extraction blocked for security.",
                entryPath,
                "UNCPathNotAllowed");
        }

        // Block drive-letter specifications (e.g., "C:", "D:/something")
        if (cleaned.Length >= 2 && char.IsLetter(cleaned[0]) && cleaned[1] == ':')
        {
            throw new SecurityValidationException(
                $"Absolute drive path detected in archive entry: '{entryPath}'. Extraction blocked for security.",
                entryPath,
                "AbsoluteDrivePathNotAllowed");
        }

        // Trim leading separators to prevent root escapes
        cleaned = cleaned.TrimStart('/');

        // Split into components and validate each segment individually
        var segments = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var safeSegments = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            if (segment == ".")
                continue;

            if (segment == "..")
            {
                throw new SecurityValidationException(
                    $"Path traversal sequence ('..') detected in entry '{entryPath}'.",
                    entryPath,
                    "PathTraversalSequence");
            }

            // Block Alternate Data Streams (e.g. "file.txt:hidden:$DATA")
            if (segment.Contains(':'))
            {
                throw new SecurityValidationException(
                    $"Alternate data stream or illegal colon character detected in entry '{entryPath}'.",
                    entryPath,
                    "IllegalColonInSegment");
            }

            // Block Windows reserved device names (NUL, CON, COM1, LPT1, etc.)
            // Strip extension first — "NUL.txt" still refers to the NUL device on Windows.
            var nameWithoutExt = Path.GetFileNameWithoutExtension(segment);
            if (WindowsReservedNames.Contains(nameWithoutExt) || WindowsReservedNames.Contains(segment))
            {
                throw new SecurityValidationException(
                    $"Windows reserved device name '{segment}' detected in entry '{entryPath}'. Extraction blocked.",
                    entryPath,
                    "ReservedDeviceName");
            }

            safeSegments.Add(segment);
        }

        var relativePath = string.Join(Path.DirectorySeparatorChar, safeSegments);
        var combinedPath = Path.Combine(normalizedDest, relativePath);
        var resolvedFullPath = Path.GetFullPath(combinedPath);

        // Final Zip Slip invariant: resolved absolute path MUST start with destination root.
        if (!resolvedFullPath.StartsWith(normalizedDest, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityValidationException(
                $"Resolved path '{resolvedFullPath}' escaped target directory '{normalizedDest}'.",
                entryPath,
                "DirectoryEscape");
        }

        return resolvedFullPath;
    }

    /// <summary>
    /// Validates that an already-resolved output path does not point to or through a reparse
    /// point (symbolic link or directory junction) that could redirect extraction outside the
    /// intended destination. Must be called after <see cref="GetSafeExtractionPath"/> and
    /// before opening the output FileStream.
    /// </summary>
    /// <param name="resolvedOutputPath">The fully-resolved, sanitized output file path.</param>
    /// <param name="destinationDirectory">The canonical destination root (may or may not end with separator).</param>
    public static void ValidateExtractionTarget(string resolvedOutputPath, string destinationDirectory)
    {
        var destinationRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationDirectory));
        var normalizedDest = NormalizeDirectory(destinationRoot);

        // Walk every existing directory component beneath the destination root.
        // If any intermediate directory is a reparse point, block extraction.
        var dir = Path.GetDirectoryName(resolvedOutputPath);

        while (!string.IsNullOrEmpty(dir))
        {
            var isDestinationRoot = string.Equals(dir, destinationRoot, StringComparison.OrdinalIgnoreCase);
            // The trailing separator enforces a path-component boundary for descendants.
            if (!isDestinationRoot && !dir.StartsWith(normalizedDest, StringComparison.OrdinalIgnoreCase))
                break;

            var directoryAttributes = TryGetPathAttributes(dir);
            if (directoryAttributes.HasValue)
            {
                if ((directoryAttributes.Value & FileAttributes.ReparsePoint) != 0)
                {
                    throw new SecurityValidationException(
                        $"Reparse point (symbolic link or junction) detected at '{dir}'. " +
                        "Extraction blocked to prevent path-redirect attacks.",
                        resolvedOutputPath,
                        "ReparsePointInExtractionPath");
                }
            }

            if (isDestinationRoot)
                break;

            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break; // reached filesystem root
            dir = parent;
        }

        // Check the target itself too. Directory entries can already exist as a junction or
        // symlink; checking only its parent components would miss that redirect.
        var targetAttributes = TryGetPathAttributes(resolvedOutputPath);
        if (targetAttributes.HasValue)
        {
            if ((targetAttributes.Value & FileAttributes.ReparsePoint) != 0)
            {
                throw new SecurityValidationException(
                    $"Reparse point (symbolic link or junction) detected at target '{resolvedOutputPath}'. Extraction blocked.",
                    resolvedOutputPath,
                    "ReparsePointTargetFile");
            }
        }
    }

    private static FileAttributes? TryGetPathAttributes(string path)
    {
        try
        {
            // Unlike File.Exists/Directory.Exists, GetAttributes can identify dangling
            // symbolic links, which still redirect a later create/open operation.
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Generates a non-conflicting unique filename (e.g., "Report (1).pdf", "Report (2).pdf").
    /// </summary>
    public static string GenerateUniqueFilePath(string fullPath)
    {
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            return fullPath;

        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullPath);
        var extension = Path.GetExtension(fullPath);

        int counter = 1;
        while (counter < 10000)
        {
            var candidate = Path.Combine(directory, $"{fileNameWithoutExt} ({counter}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;

            counter++;
        }

        return Path.Combine(directory, $"{fileNameWithoutExt}_{Guid.NewGuid():N}{extension}");
    }
}
