using System.Buffers;
using System.Diagnostics;
using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Core.Security;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Readers;
using SharpCompress.Writers;


namespace Firezip.Formats.Providers;

public abstract class ArchiveProviderBase : IArchiveProvider
{
    private const int BufferSize = 81920; // 80 KB streaming buffer

    public abstract ArchiveFormat Format { get; }

    public abstract bool CanHandle(string filePath);

    public virtual Task<ArchiveInfo> OpenArchiveAsync(
        string filePath,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return CommonOpenArchiveAsync(filePath, Format, password, cancellationToken);
    }

    public virtual Task<OperationResult> ExtractAsync(
        ExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        return CommonExtractAsync(request, cancellationToken);
    }

    public abstract Task<OperationResult> CreateArchiveAsync(
        CompressionRequest request,
        CancellationToken cancellationToken = default);

    public virtual Task<OperationResult> TestArchiveAsync(
        TestArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        return CommonTestArchiveAsync(request, cancellationToken);
    }

    public virtual Task<OperationResult> DeleteEntriesAsync(
        string archivePath,
        IReadOnlyList<string> entryPaths,
        CancellationToken cancellationToken = default)
    {
        return CommonDeleteEntriesAsync(archivePath, entryPaths, cancellationToken);
    }

    public virtual Task<OperationResult> AddEntriesAsync(
        string archivePath,
        IReadOnlyList<string> sourceFilePaths,
        string? targetSubfolder = null,
        CancellationToken cancellationToken = default)
    {
        return CommonAddEntriesAsync(archivePath, sourceFilePaths, targetSubfolder, cancellationToken);
    }

    protected async Task<ArchiveInfo> CommonOpenArchiveAsync(
        string filePath,
        ArchiveFormat format,
        string? password,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var readerOptions = new ReaderOptions { Password = password };
            using var archive = OpenArchiveInstance(filePath, format, readerOptions, out var streamToDispose);
            using var _ = streamToDispose;

            var entries = new List<ArchiveEntry>();
            bool isEncrypted = archive.IsEncrypted;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsEncrypted)
                    isEncrypted = true;

                var rawKey = entry.Key ?? string.Empty;
                var normalized = rawKey.Replace('\\', '/').Trim('/');
                if (string.IsNullOrEmpty(normalized))
                    continue;

                var lastSlash = normalized.LastIndexOf('/');
                var name = lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;
                var parent = lastSlash >= 0 ? normalized[..lastSlash] : string.Empty;
                if (string.IsNullOrEmpty(name) && entry.IsDirectory)
                {
                    name = normalized;
                }

                string? attributes = null;
                try
                {
                    attributes = entry.Attrib?.ToString();
                }
                catch
                {
                    // Some formats (e.g. Tar) don't support file attributes
                }

                entries.Add(new ArchiveEntry
                {
                    FullPath = normalized,
                    Name = name,
                    ParentDirectory = parent,
                    Size = entry.Size,
                    CompressedSize = entry.CompressedSize,
                    ModifiedDate = entry.LastModifiedTime,
                    IsDirectory = entry.IsDirectory,
                    IsEncrypted = entry.IsEncrypted,
                    Crc = entry.Crc,
                    Attributes = attributes
                });
            }

            return new ArchiveInfo
            {
                FilePath = filePath,
                Format = format,
                Entries = entries,
                IsEncrypted = isEncrypted,
                IsSolid = archive.IsSolid
            };
        }, cancellationToken);
    }

    protected async Task<OperationResult> CommonExtractAsync(
        ExtractionRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        int filesProcessed = 0;
        int filesSkipped = 0;
        long bytesProcessed = 0;
        var bombDetector = new DecompressionBombDetector();

        try
        {
            var destinationCanonical = PathSanitizer.NormalizeDirectory(request.DestinationDirectory);
            if (!Directory.Exists(destinationCanonical))
            {
                Directory.CreateDirectory(destinationCanonical);
            }

            var readerOptions = new ReaderOptions { Password = request.Password };
            using var archive = OpenArchiveInstance(request.ArchiveFilePath, Format, readerOptions, out var streamToDispose);
            using var _ = streamToDispose;

            var requestedSet = request.EntriesToExtract != null
                ? new HashSet<string>(request.EntriesToExtract.Select(e => e.FullPath.Replace('\\', '/').Trim('/')), StringComparer.OrdinalIgnoreCase)
                : null;

            var entriesToExtract = archive.Entries
                .Where(e =>
                {
                    if (string.IsNullOrWhiteSpace(e.Key)) return false;
                    var key = e.Key.Replace('\\', '/').Trim('/');
                    return requestedSet == null || requestedSet.Contains(key);
                })
                .ToList();

            long totalBytes = entriesToExtract.Where(e => !e.IsDirectory).Sum(e => e.Size);
            int totalFiles = entriesToExtract.Count(e => !e.IsDirectory);

            ConflictResolution? globalResolution = null;

            foreach (var entry in entriesToExtract)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawKey = entry.Key ?? string.Empty;
                var safePath = PathSanitizer.GetSafeExtractionPath(destinationCanonical, rawKey);

                if (entry.IsDirectory)
                {
                    PathSanitizer.ValidateExtractionTarget(safePath, destinationCanonical);
                    if (!Directory.Exists(safePath))
                    {
                        Directory.CreateDirectory(safePath);
                    }
                    PathSanitizer.ValidateExtractionTarget(safePath, destinationCanonical);
                    continue;
                }

                // Pre-extraction decompression bomb check using archive header metadata.
                // This catches obvious bombs (e.g. 42.zip) before reading any data.
                if (bombDetector.IsEntrySuspicious(entry.Size, entry.CompressedSize, out var bombReason))
                {
                    return OperationResult.Failed(
                        $"Extraction of '{rawKey}' blocked: {bombReason}",
                        $"Entry: {rawKey}, Uncompressed: {entry.Size}, Compressed: {entry.CompressedSize}",
                        filesProcessed,
                        bytesProcessed);
                }

                // Check existing components before creating anything: Directory.CreateDirectory
                // follows junctions and symlinks, which could otherwise create directories outside
                // the chosen extraction destination.
                PathSanitizer.ValidateExtractionTarget(safePath, destinationCanonical);

                var parentDir = Path.GetDirectoryName(safePath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }
                PathSanitizer.ValidateExtractionTarget(safePath, destinationCanonical);

                // Conflict Resolution
                var (targetPath, cancel, updatedGlobal) = await ResolveConflictWithGlobalAsync(
                    safePath,
                    request.DefaultConflictPolicy,
                    request.ConflictCallback,
                    globalResolution);

                if (updatedGlobal != null)
                {
                    globalResolution = updatedGlobal;
                }

                if (cancel)
                {
                    return OperationResult.Cancelled(filesProcessed, bytesProcessed, stopwatch.Elapsed);
                }

                if (targetPath == null)
                {
                    filesSkipped++;
                    continue;
                }

                // Reparse point / symlink check: block extraction if any path component
                // beneath the destination root is a junction or symbolic link.
                PathSanitizer.ValidateExtractionTarget(targetPath, destinationCanonical);

                // Streaming extraction
                bombDetector.Reset();
                var tempPath = Path.Combine(
                    parentDir ?? destinationCanonical,
                    $".firezip-{Guid.NewGuid():N}.tmp");
                try
                {
                    using (var entryStream = entry.OpenEntryStream())
                    using (var outputStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, true))
                    {
                        var written = await CopyStreamWithProgressAsync(
                            entryStream,
                            outputStream,
                            entry.Size,
                            Path.GetFileName(safePath),
                            totalBytes,
                            bytesProcessed,
                            filesProcessed + 1,
                            totalFiles,
                            request.Progress,
                            stopwatch,
                            bombDetector,
                            cancellationToken);

                        await outputStream.FlushAsync(cancellationToken);
                        bytesProcessed += written;
                    }

                    // Keep incomplete or cancelled data out of the destination. Commit only
                    // after the stream has completed and the destination path is revalidated.
                    PathSanitizer.ValidateExtractionTarget(targetPath, destinationCanonical);
                    File.Move(tempPath, targetPath, overwrite: true);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                    catch
                    {
                        // Preserve the extraction result; temporary-file cleanup is best effort.
                    }
                }

                if (entry.LastModifiedTime.HasValue)
                {
                    try
                    {
                        File.SetLastWriteTime(targetPath, entry.LastModifiedTime.Value);
                    }
                    catch
                    {
                        // Ignore non-critical timestamp restore errors
                    }
                }

                filesProcessed++;
            }

            stopwatch.Stop();
            return OperationResult.Succeeded(filesProcessed, bytesProcessed, stopwatch.Elapsed, filesSkipped);
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Cancelled(filesProcessed, bytesProcessed, stopwatch.Elapsed);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return OperationResult.Failed("Incorrect archive password or archive encryption error.");
        }
        catch (SecurityValidationException secEx)
        {
            return OperationResult.Failed($"Security violation blocked: {secEx.Message}", secEx.ToString(), filesProcessed, bytesProcessed);
        }
        catch (Exception ex)
        {
            return OperationResult.Failed($"Extraction error: {ex.Message}", ex.ToString(), filesProcessed, bytesProcessed);
        }
    }

    protected async Task<OperationResult> CommonTestArchiveAsync(
        TestArchiveRequest request,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            int filesProcessed = 0;
            long bytesProcessed = 0;
            var corrupted = new List<string>();

            try
            {
                var readerOptions = new ReaderOptions { Password = request.Password };
                using var archive = OpenArchiveInstance(request.ArchiveFilePath, Format, readerOptions, out var streamToDispose);
                using var _ = streamToDispose;

                long totalBytes = archive.Entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
                int totalFiles = archive.Entries.Count(e => !e.IsDirectory);
                var buffer = new byte[BufferSize];

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        using var stream = entry.OpenEntryStream();
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            bytesProcessed += read;
                        }
                        filesProcessed++;

                        if (request.Progress != null)
                        {
                            request.Progress.Report(new OperationProgress
                            {
                                BytesProcessed = bytesProcessed,
                                TotalBytes = totalBytes,
                                FilesProcessed = filesProcessed,
                                TotalFiles = totalFiles,
                                CurrentItemName = entry.Key ?? string.Empty,
                                OperationPhase = "Testing"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        corrupted.Add($"{entry.Key}: {ex.Message}");
                    }
                }

                stopwatch.Stop();
                if (corrupted.Count > 0)
                {
                    return new OperationResult
                    {
                        Success = false,
                        ErrorMessage = $"{corrupted.Count} files failed integrity verification.",
                        DetailedError = string.Join(Environment.NewLine, corrupted),
                        CorruptedEntries = corrupted,
                        FilesProcessed = filesProcessed,
                        BytesProcessed = bytesProcessed,
                        ElapsedTime = stopwatch.Elapsed
                    };
                }

                return OperationResult.Succeeded(filesProcessed, bytesProcessed, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed($"Integrity test failed: {ex.Message}", ex.ToString());
            }
        }, cancellationToken);
    }

    protected async Task<OperationResult> CommonCreateWithWriterAsync(
        CompressionRequest request,
        ArchiveType archiveType,
        IWriterOptions writerOptions,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        int filesProcessed = 0;
        long bytesProcessed = 0;

        if (!string.IsNullOrWhiteSpace(request.Options.Password))
            return OperationResult.Failed("Password protection is supported only for ZIP and 7z archives.");
        if (request.Options.EncryptHeader)
            return OperationResult.Failed("Encrypted file names are supported only in 7z archives.");
        if (request.Options.SplitArchive)
            return OperationResult.Failed("Split volumes are currently supported only for 7z archives.");
        if (request.Options.Level != CompressionLevel.Normal)
            return OperationResult.Failed("Adjustable compression levels are supported only for ZIP and 7z archives.");

        try
        {
            var filesToAdd = new List<(string SourcePath, string RelativeEntryPath)>();

            foreach (var source in request.SourcePaths)
            {
                if (File.Exists(source))
                {
                    filesToAdd.Add((source, Path.GetFileName(source)));
                }
                else if (Directory.Exists(source))
                {
                    var baseDir = Path.GetDirectoryName(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar)) ?? source;
                    var allFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                    foreach (var file in allFiles)
                    {
                        var relative = Path.GetRelativePath(baseDir, file).Replace('\\', '/');
                        if (IsExcluded(relative, Path.GetFileName(file), request.Options.ExcludePatterns))
                            continue;
                        filesToAdd.Add((file, relative));
                    }
                }
                else
                {
                    return OperationResult.Failed($"The source file or folder does not exist: {source}");
                }
            }

            if (filesToAdd.Count == 0)
                return OperationResult.Failed("There are no files to add to the archive.");

            long totalBytes = filesToAdd.Sum(f => new FileInfo(f.SourcePath).Length);
            int totalFiles = filesToAdd.Count;

            var outDir = Path.GetDirectoryName(request.OutputArchiveFilePath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            using (var fileStream = new FileStream(request.OutputArchiveFilePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true))
            using (var writer = WriterFactory.OpenWriter(fileStream, archiveType, writerOptions))
            {
                foreach (var (src, relativePath) in filesToAdd)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileInfo = new FileInfo(src);
                    using (var sourceStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true))
                    {
                        writer.Write(relativePath, sourceStream,
                            request.Options.PreserveTimestamps ? fileInfo.LastWriteTimeUtc : null);
                    }

                    filesProcessed++;
                    bytesProcessed += fileInfo.Length;

                    if (request.Progress != null)
                    {
                        var elapsedSec = stopwatch.Elapsed.TotalSeconds;
                        var speed = elapsedSec > 0.1 ? bytesProcessed / elapsedSec : 0.0;
                        var remaining = Math.Max(0, totalBytes - bytesProcessed);
                        var eta = speed > 1024 ? TimeSpan.FromSeconds(remaining / speed) : (TimeSpan?)null;

                        request.Progress.Report(new OperationProgress
                        {
                            BytesProcessed = bytesProcessed,
                            TotalBytes = totalBytes,
                            FilesProcessed = filesProcessed,
                            TotalFiles = totalFiles,
                            CurrentItemName = relativePath,
                            BytesPerSecond = speed,
                            EstimatedTimeRemaining = eta,
                            OperationPhase = "Compressing"
                        });
                    }
                }
            }

            stopwatch.Stop();
            return OperationResult.Succeeded(filesProcessed, bytesProcessed, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(request.OutputArchiveFilePath))
            {
                try { File.Delete(request.OutputArchiveFilePath); } catch { }
            }
            return OperationResult.Cancelled(filesProcessed, bytesProcessed, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            if (File.Exists(request.OutputArchiveFilePath))
            {
                try { File.Delete(request.OutputArchiveFilePath); } catch { }
            }
            return OperationResult.Failed($"Compression error: {ex.Message}", ex.ToString(), filesProcessed, bytesProcessed);
        }
    }

    private static bool IsExcluded(string relativePath, string fileName, IReadOnlyList<string> patterns)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        return patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim().Replace('\\', '/'))
            .Any(pattern =>
                System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, normalizedPath, ignoreCase: true) ||
                System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true));
    }

    private static async Task<(string? TargetPath, bool Cancel, ConflictResolution? GlobalResolution)> ResolveConflictWithGlobalAsync(
        string destinationPath,
        ConflictPolicy defaultPolicy,
        Func<string, Task<ConflictResolution>>? conflictCallback,
        ConflictResolution? activeGlobalResolution)
    {
        if (!File.Exists(destinationPath))
            return (destinationPath, false, null);

        var resolution = activeGlobalResolution;
        ConflictResolution? newlyAppliedGlobal = null;

        if (resolution == null)
        {
            if (conflictCallback != null)
            {
                resolution = await conflictCallback(destinationPath);
                if (resolution.ApplyToAllRemaining)
                {
                    newlyAppliedGlobal = resolution;
                }
            }
            else
            {
                resolution = defaultPolicy switch
                {
                    ConflictPolicy.Overwrite => ConflictResolution.OverwriteCurrent,
                    ConflictPolicy.Skip => ConflictResolution.SkipCurrent,
                    ConflictPolicy.Rename => ConflictResolution.RenameCurrent(PathSanitizer.GenerateUniqueFilePath(destinationPath)),
                    _ => ConflictResolution.OverwriteCurrent
                };
            }
        }

        switch (resolution.Policy)
        {
            case ConflictPolicy.Skip:
                return (null, false, newlyAppliedGlobal);

            case ConflictPolicy.Rename:
                var newPath = resolution.CustomNewPath ?? PathSanitizer.GenerateUniqueFilePath(destinationPath);
                return (newPath, false, newlyAppliedGlobal);

            case ConflictPolicy.Cancel:
                return (null, true, newlyAppliedGlobal);

            case ConflictPolicy.Overwrite:
            default:
                return (destinationPath, false, newlyAppliedGlobal);
        }
    }

    protected async Task<long> CopyStreamWithProgressAsync(
        Stream source,
        Stream destination,
        long entrySize,
        string itemName,
        long totalBytes,
        long currentBytesProcessed,
        int filesProcessed,
        int totalFiles,
        IProgress<OperationProgress>? progress,
        Stopwatch stopwatch,
        DecompressionBombDetector bombDetector,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int read;
            long entryBytesWritten = 0;
            long lastReportTime = stopwatch.ElapsedMilliseconds;

            while ((read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                entryBytesWritten += read;
                // SharpCompress does not expose reliable compressed-byte deltas per entry. Check
                // emitted bytes against the header's declared size before writing each buffer.
                bombDetector.TrackProgress(read, read, entrySize);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                var now = stopwatch.ElapsedMilliseconds;
                if (progress != null && (entryBytesWritten == read || now - lastReportTime >= 50))
                {
                    lastReportTime = now;
                    var totalProcessedSoFar = currentBytesProcessed + entryBytesWritten;
                    var elapsedSec = stopwatch.Elapsed.TotalSeconds;
                    var speed = elapsedSec > 0.1 ? totalProcessedSoFar / elapsedSec : 0.0;
                    var remainingBytes = Math.Max(0, totalBytes - totalProcessedSoFar);
                    var eta = speed > 1024 ? TimeSpan.FromSeconds(remainingBytes / speed) : (TimeSpan?)null;

                    progress.Report(new OperationProgress
                    {
                        BytesProcessed = totalProcessedSoFar,
                        TotalBytes = totalBytes,
                        FilesProcessed = filesProcessed,
                        TotalFiles = totalFiles,
                        CurrentItemName = itemName,
                        ItemBytesProcessed = entryBytesWritten,
                        ItemTotalBytes = entrySize,
                        BytesPerSecond = speed,
                        EstimatedTimeRemaining = eta,
                        OperationPhase = "Extracting"
                    });
                }
            }

            return entryBytesWritten;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    protected async Task<OperationResult> CommonDeleteEntriesAsync(
        string archivePath,
        IReadOnlyList<string> entryPaths,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return OperationResult.Failed("Archive file does not exist.");

        if (entryPaths == null || entryPaths.Count == 0)
            return OperationResult.Failed("No entries specified for deletion.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"firezip-delete-{Guid.NewGuid():N}");
        string? stagedArchive = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var archiveInfo = await OpenArchiveAsync(archivePath, null, cancellationToken);
            var normalizedTargets = new HashSet<string>(
                entryPaths.Select(p => p.Replace('\\', '/').Trim('/')),
                StringComparer.OrdinalIgnoreCase);

            var entriesToKeep = archiveInfo.Entries.Where(e =>
            {
                var norm = e.FullPath.Replace('\\', '/').Trim('/');
                return !normalizedTargets.Contains(norm) &&
                       !normalizedTargets.Any(t => norm.StartsWith(t + "/", StringComparison.OrdinalIgnoreCase));
            }).ToList();

            if (entriesToKeep.Count == archiveInfo.Entries.Count)
            {
                return OperationResult.Succeeded(0, 0, TimeSpan.Zero);
            }

            Directory.CreateDirectory(tempDir);

            if (entriesToKeep.Count > 0)
            {
                var extractResult = await ExtractAsync(new ExtractionRequest
                {
                    ArchiveFilePath = archivePath,
                    DestinationDirectory = tempDir,
                    EntriesToExtract = entriesToKeep,
                    DefaultConflictPolicy = ConflictPolicy.Overwrite
                }, cancellationToken);

                if (!extractResult.Success)
                    return OperationResult.Failed($"Failed to extract entries during deletion: {extractResult.ErrorMessage}");

                var outputDir = Path.GetDirectoryName(Path.GetFullPath(archivePath)) ?? Path.GetTempPath();
                stagedArchive = Path.Combine(outputDir, $".firezip-staged-{Guid.NewGuid():N}{Path.GetExtension(archivePath)}");

                var items = Directory.GetFileSystemEntries(tempDir);
                var compressResult = await CreateArchiveAsync(new CompressionRequest
                {
                    OutputArchiveFilePath = stagedArchive,
                    Format = Format,
                    SourcePaths = items,
                    Options = new CompressionOptions { Level = CompressionLevel.Normal }
                }, cancellationToken);

                if (!compressResult.Success)
                    return OperationResult.Failed($"Failed to rebuild archive: {compressResult.ErrorMessage}");

                File.Move(stagedArchive, archivePath, overwrite: true);
                stagedArchive = null;
            }
            else
            {
                // All entries deleted: remove archive file
                File.Delete(archivePath);
            }

            var deletedCount = archiveInfo.Entries.Count - entriesToKeep.Count;
            return OperationResult.Succeeded(deletedCount, 0, TimeSpan.Zero);
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Cancelled();
        }
        catch (Exception ex)
        {
            return OperationResult.Failed($"Error deleting entries: {ex.Message}", ex.ToString());
        }
        finally
        {
            if (stagedArchive != null && File.Exists(stagedArchive))
            {
                try { File.Delete(stagedArchive); } catch { }
            }
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    protected async Task<OperationResult> CommonAddEntriesAsync(
        string archivePath,
        IReadOnlyList<string> sourceFilePaths,
        string? targetSubfolder = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            return OperationResult.Failed("Archive file does not exist.");

        if (sourceFilePaths == null || sourceFilePaths.Count == 0)
            return OperationResult.Failed("No source files specified to add.");

        var tempDir = Path.Combine(Path.GetTempPath(), $"firezip-add-{Guid.NewGuid():N}");
        string? stagedArchive = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(tempDir);

            var extractResult = await ExtractAsync(new ExtractionRequest
            {
                ArchiveFilePath = archivePath,
                DestinationDirectory = tempDir,
                DefaultConflictPolicy = ConflictPolicy.Overwrite
            }, cancellationToken);

            if (!extractResult.Success)
                return OperationResult.Failed($"Failed to read existing archive: {extractResult.ErrorMessage}");

            var destBase = tempDir;
            if (!string.IsNullOrWhiteSpace(targetSubfolder))
            {
                var sub = targetSubfolder.Replace('\\', '/').Trim('/');
                destBase = Path.Combine(tempDir, sub.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(destBase);
            }

            foreach (var src in sourceFilePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(src))
                {
                    var destFile = Path.Combine(destBase, Path.GetFileName(src));
                    File.Copy(src, destFile, overwrite: true);
                }
                else if (Directory.Exists(src))
                {
                    var dirName = Path.GetFileName(Path.TrimEndingDirectorySeparator(src));
                    var destSubDir = Path.Combine(destBase, dirName);
                    CopyDirectoryRecursive(src, destSubDir);
                }
            }

            var outputDir = Path.GetDirectoryName(Path.GetFullPath(archivePath)) ?? Path.GetTempPath();
            stagedArchive = Path.Combine(outputDir, $".firezip-staged-{Guid.NewGuid():N}{Path.GetExtension(archivePath)}");

            var items = Directory.GetFileSystemEntries(tempDir);
            var compressResult = await CreateArchiveAsync(new CompressionRequest
            {
                OutputArchiveFilePath = stagedArchive,
                Format = Format,
                SourcePaths = items,
                Options = new CompressionOptions { Level = CompressionLevel.Normal }
            }, cancellationToken);

            if (!compressResult.Success)
                return OperationResult.Failed($"Failed to rebuild archive: {compressResult.ErrorMessage}");

            File.Move(stagedArchive, archivePath, overwrite: true);
            stagedArchive = null;
            return OperationResult.Succeeded(sourceFilePaths.Count, 0, TimeSpan.Zero);
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Cancelled();
        }
        catch (Exception ex)
        {
            return OperationResult.Failed($"Error adding entries: {ex.Message}", ex.ToString());
        }
        finally
        {
            if (stagedArchive != null && File.Exists(stagedArchive))
            {
                try { File.Delete(stagedArchive); } catch { }
            }
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    protected static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }
        foreach (var sub in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(targetDir, Path.GetFileName(sub));
            CopyDirectoryRecursive(sub, destSub);
        }
    }

    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var d in disposables)
            {
                try { d?.Dispose(); } catch { }
            }
        }
    }

    protected static IArchive OpenArchiveInstance(
        string filePath,
        ArchiveFormat format,
        ReaderOptions readerOptions,
        out IDisposable? streamToDispose)
    {
        streamToDispose = null;
        try
        {
            if (format == ArchiveFormat.GZip)
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
                var mem = new MemoryStream();
                gz.CopyTo(mem);
                mem.Position = 0;
                streamToDispose = mem;
                return ArchiveFactory.OpenArchive(mem, readerOptions);
            }
            if (format == ArchiveFormat.BZip2)
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var bz = SharpCompress.Compressors.BZip2.BZip2Stream.Create(fs, SharpCompress.Compressors.CompressionMode.Decompress, false, false, false);
                var mem = new MemoryStream();
                bz.CopyTo(mem);
                mem.Position = 0;
                streamToDispose = mem;
                return ArchiveFactory.OpenArchive(mem, readerOptions);
            }
            if (format == ArchiveFormat.Xz)
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var xz = new SharpCompress.Compressors.Xz.XZStream(fs);
                var mem = new MemoryStream();
                xz.CopyTo(mem);
                mem.Position = 0;
                streamToDispose = mem;
                return ArchiveFactory.OpenArchive(mem, readerOptions);
            }
        }
        catch
        {
            streamToDispose?.Dispose();
            streamToDispose = null;
        }

        return ArchiveFactory.OpenArchive(filePath, readerOptions);
    }
}
