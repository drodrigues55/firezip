using System.IO.Compression;
using Firezip.Core.Models;

namespace Firezip.Formats.Providers;

public class ZipArchiveProvider : ArchiveProviderBase
{
    public override ArchiveFormat Format => ArchiveFormat.Zip;

    public override bool CanHandle(string filePath)
    {
        return FormatDetector.DetectFormat(filePath) == ArchiveFormat.Zip;
    }

    public override Task<OperationResult> CreateArchiveAsync(
        CompressionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SevenZipCompressionAdapter.CreateArchiveAsync(request, Format, cancellationToken);
    }

    public override async Task<OperationResult> DeleteEntriesAsync(
        string archivePath,
        IReadOnlyList<string> entryPaths,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return OperationResult.Failed("Archive file does not exist.");

        if (entryPaths == null || entryPaths.Count == 0)
            return OperationResult.Failed("No entries specified for deletion.");

        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var backupPath = archivePath + $".firezip-bak-{Guid.NewGuid():N}";
                File.Copy(archivePath, backupPath, overwrite: true);
                try
                {
                    var normalizedTargets = new HashSet<string>(
                        entryPaths.Select(p => p.Replace('\\', '/').Trim('/')),
                        StringComparer.OrdinalIgnoreCase);

                    int deletedCount = 0;
                    using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                    {
                        var toDelete = archive.Entries.Where(e =>
                        {
                            var norm = e.FullName.Replace('\\', '/').Trim('/');
                            return normalizedTargets.Contains(norm) ||
                                   normalizedTargets.Any(t => norm.StartsWith(t + "/", StringComparison.OrdinalIgnoreCase));
                        }).ToList();

                        foreach (var entry in toDelete)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            entry.Delete();
                            deletedCount++;
                        }
                    }

                    File.Delete(backupPath);
                    return OperationResult.Succeeded(deletedCount, 0, TimeSpan.Zero);
                }
                catch
                {
                    if (File.Exists(backupPath))
                    {
                        try { File.Move(backupPath, archivePath, overwrite: true); } catch { }
                    }
                    throw;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Cancelled();
        }
        catch (Exception)
        {
            // Fall back to general staging extraction and rebuilding
            return await CommonDeleteEntriesAsync(archivePath, entryPaths, cancellationToken);
        }
    }

    public override async Task<OperationResult> AddEntriesAsync(
        string archivePath,
        IReadOnlyList<string> sourceFilePaths,
        string? targetSubfolder = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return OperationResult.Failed("Archive file does not exist.");

        if (sourceFilePaths == null || sourceFilePaths.Count == 0)
            return OperationResult.Failed("No source files specified to add.");

        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var backupPath = archivePath + $".firezip-bak-{Guid.NewGuid():N}";
                File.Copy(archivePath, backupPath, overwrite: true);
                try
                {
                    int addedCount = 0;
                    using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                    {
                        foreach (var src in sourceFilePaths)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (File.Exists(src))
                            {
                                var fileName = Path.GetFileName(src);
                                var entryName = string.IsNullOrWhiteSpace(targetSubfolder)
                                    ? fileName
                                    : $"{targetSubfolder.Replace('\\', '/').Trim('/')}/{fileName}";

                                var existing = archive.GetEntry(entryName);
                                existing?.Delete();

                                archive.CreateEntryFromFile(src, entryName, System.IO.Compression.CompressionLevel.Optimal);
                                addedCount++;
                            }
                            else if (Directory.Exists(src))
                            {
                                var dirName = Path.GetFileName(Path.TrimEndingDirectorySeparator(src));
                                var baseTarget = string.IsNullOrWhiteSpace(targetSubfolder)
                                    ? dirName
                                    : $"{targetSubfolder.Replace('\\', '/').Trim('/')}/{dirName}";

                                foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    var rel = Path.GetRelativePath(src, file).Replace('\\', '/');
                                    var entryName = $"{baseTarget}/{rel}";

                                    var existing = archive.GetEntry(entryName);
                                    existing?.Delete();

                                    archive.CreateEntryFromFile(file, entryName, System.IO.Compression.CompressionLevel.Optimal);
                                    addedCount++;
                                }
                            }
                        }
                    }

                    File.Delete(backupPath);
                    return OperationResult.Succeeded(addedCount, 0, TimeSpan.Zero);
                }
                catch
                {
                    if (File.Exists(backupPath))
                    {
                        try { File.Move(backupPath, archivePath, overwrite: true); } catch { }
                    }
                    throw;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Cancelled();
        }
        catch (Exception)
        {
            // Fall back to general staging extraction and rebuilding
            return await CommonAddEntriesAsync(archivePath, sourceFilePaths, targetSubfolder, cancellationToken);
        }
    }
}

