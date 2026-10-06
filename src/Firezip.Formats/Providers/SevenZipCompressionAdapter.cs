using System.Diagnostics;
using System.IO.Enumeration;
using Firezip.Core.Models;
using SharpSevenZip;
using SharpSevenZip.EventArguments;
using FirezipCompressionLevel = Firezip.Core.Models.CompressionLevel;
using SevenZipLevel = SharpSevenZip.CompressionLevel;
using OperationResult = Firezip.Core.Models.OperationResult;

namespace Firezip.Formats.Providers;

/// <summary>
/// Creates ZIP and 7z archives with the 7-Zip engine. Keeping the native-engine boundary
/// here lets the archive providers keep their public contract independent of SharpSevenZip.
/// </summary>
internal static class SevenZipCompressionAdapter
{
    private static readonly EnumerationOptions ImmediateChildrenOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false
    };

    public static Task<OperationResult> CreateArchiveAsync(
        CompressionRequest request,
        ArchiveFormat format,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => CreateArchive(request, format, cancellationToken), CancellationToken.None);
    }

    private static OperationResult CreateArchive(
        CompressionRequest request,
        ArchiveFormat format,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var temporaryDirectory = string.Empty;
        var preserveTemporaryDirectory = false;
        var streams = new List<Stream>();
        var progress = new CompressionProgress(request.Progress, stopwatch);

        try
        {
            var options = request.Options;
            if (format is not (ArchiveFormat.Zip or ArchiveFormat.SevenZip))
                return OperationResult.Failed("The 7-Zip compression adapter only creates ZIP and 7z archives.");

            if (format == ArchiveFormat.Zip && options.EncryptHeader)
                return OperationResult.Failed("Encrypted file names are supported only in 7z archives.");

            if (options.SplitArchive && format != ArchiveFormat.SevenZip)
                return OperationResult.Failed("Split volumes are currently supported only for 7z archives.");

            if (options.EncryptHeader && string.IsNullOrWhiteSpace(options.Password))
                return OperationResult.Failed("Enter a password before enabling 7z file-name encryption.");

            if (options.SplitArchive && options.VolumeSizeBytes is not > 0)
                return OperationResult.Failed("Choose a valid volume size before splitting the archive.");

            cancellationToken.ThrowIfCancellationRequested();

            var outputPath = Path.GetFullPath(request.OutputArchiveFilePath);
            var inputEntries = CollectInputEntries(request.SourcePaths, outputPath, options.ExcludePatterns);
            var files = inputEntries.Where(entry => !entry.IsDirectory).ToArray();
            if (files.Length == 0 && inputEntries.Count == 0)
                return OperationResult.Failed("There are no files to add to the archive.");

            var totalBytes = files.Aggregate(0L, (total, entry) => checked(total + entry.Length));
            progress.Configure(totalBytes, files.Length);

            var outputDirectory = Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException("The output archive must have a parent folder.");
            Directory.CreateDirectory(outputDirectory);

            temporaryDirectory = Path.Combine(outputDirectory, $".firezip-{Guid.NewGuid():N}.tmp");
            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                File.SetAttributes(temporaryDirectory, FileAttributes.Hidden | FileAttributes.Directory);
            }
            catch (IOException)
            {
                // Hidden attributes are cosmetic; archive creation remains valid without them.
            }
            catch (UnauthorizedAccessException)
            {
                // Hidden attributes are cosmetic; archive creation remains valid without them.
            }

            var stagedArchivePath = Path.Combine(temporaryDirectory, Path.GetFileName(outputPath));
            var streamEntries = new Dictionary<string, StreamWithAttributes>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in inputEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.IsDirectory)
                {
                    continue;
                }

                var fileStream = new FileStream(entry.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    128 * 1024, FileOptions.SequentialScan);
                var cancellationStream = new CancellationCheckingStream(fileStream, progress.AddBytesRead, cancellationToken);
                streams.Add(cancellationStream);
                streamEntries.Add(entry.EntryPath, new StreamWithAttributes(
                    cancellationStream,
                    null,
                    options.PreserveTimestamps ? entry.LastWriteTimeUtc : null,
                    null));
            }

            var compressor = new SharpSevenZipCompressor
            {
                ArchiveFormat = format == ArchiveFormat.Zip ? OutArchiveFormat.Zip : OutArchiveFormat.SevenZip,
                CompressionMode = SharpSevenZip.CompressionMode.Create,
                CompressionLevel = ToSevenZipLevel(options.Level),
                CompressionMethod = format == ArchiveFormat.Zip
                    ? SharpSevenZip.CompressionMethod.Deflate
                    : SharpSevenZip.CompressionMethod.Lzma2,
                ZipEncryptionMethod = ZipEncryptionMethod.Aes256,
                EncryptHeaders = format == ArchiveFormat.SevenZip && options.EncryptHeader,
                VolumeSize = options.SplitArchive ? options.VolumeSizeBytes!.Value : 0,
                IncludeEmptyDirectories = true,
                DirectoryStructure = true,
                PreserveDirectoryRoot = true,
                FastCompression = false,
                EventSynchronization = EventSynchronizationStrategy.AlwaysSynchronous
            };
            compressor.FileCompressionStarted += OnFileCompressionStarted;
            compressor.FileCompressionFinished += OnFileCompressionFinished;
            compressor.Compressing += OnCompressing;

            try
            {
                compressor.CompressStreamDictionary(streamEntries, stagedArchivePath, options.Password ?? string.Empty);
            }
            finally
            {
                compressor.FileCompressionStarted -= OnFileCompressionStarted;
                compressor.FileCompressionFinished -= OnFileCompressionFinished;
                compressor.Compressing -= OnCompressing;
            }

            void OnFileCompressionStarted(object? sender, FileNameEventArgs args)
            {
                args.Cancel = cancellationToken.IsCancellationRequested;
                progress.SetCurrentItem(args.FileName);
            }

            void OnFileCompressionFinished(object? sender, EventArgs args) => progress.FileCompleted();

            void OnCompressing(object? sender, ProgressEventArgs args)
            {
                if (cancellationToken.IsCancellationRequested)
                    progress.ReportCurrent();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var stagedOutputs = GetArchiveOutputGroup(temporaryDirectory, Path.GetFileName(outputPath));
            if (stagedOutputs.Count == 0)
                throw new IOException("The 7-Zip engine did not produce an archive file.");

            CommitArchiveOutputGroup(temporaryDirectory, outputDirectory, Path.GetFileName(outputPath), stagedOutputs);
            progress.Complete();
            stopwatch.Stop();
            return OperationResult.Succeeded(files.Length, totalBytes, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return OperationResult.Cancelled(progress.FilesCompleted, progress.BytesRead, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (ex is ArchiveCommitException { RecoveryDirectory: not null } commitException)
            {
                preserveTemporaryDirectory = true;
                return OperationResult.Failed(
                    "Could not safely replace the existing archive. Recovery copies were kept in the temporary folder.",
                    $"{commitException.Message}{Environment.NewLine}Recovery folder: {commitException.RecoveryDirectory}",
                    progress.FilesCompleted,
                    progress.BytesRead) with
                { ElapsedTime = stopwatch.Elapsed };
            }

            if (cancellationToken.IsCancellationRequested)
                return OperationResult.Cancelled(progress.FilesCompleted, progress.BytesRead, stopwatch.Elapsed);

            return OperationResult.Failed(
                $"Compression error: {ex.Message}", ex.ToString(), progress.FilesCompleted, progress.BytesRead)
                with
            { ElapsedTime = stopwatch.Elapsed };
        }
        finally
        {
            foreach (var stream in streams)
            {
                try { stream.Dispose(); } catch (IOException) { }
            }

            if (!preserveTemporaryDirectory && temporaryDirectory.Length > 0 && Directory.Exists(temporaryDirectory))
            {
                try { Directory.Delete(temporaryDirectory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        SevenZipLevel ToSevenZipLevel(FirezipCompressionLevel level) => level switch
        {
            FirezipCompressionLevel.Store => SevenZipLevel.None,
            FirezipCompressionLevel.Fast => SevenZipLevel.Fast,
            FirezipCompressionLevel.Normal => SevenZipLevel.Normal,
            FirezipCompressionLevel.Maximum => SevenZipLevel.High,
            FirezipCompressionLevel.Ultra => SevenZipLevel.Ultra,
            _ => SevenZipLevel.Normal
        };
    }

    private static List<CompressionInputEntry> CollectInputEntries(
        IReadOnlyList<string> sourcePaths,
        string outputPath,
        IReadOnlyList<string> excludePatterns)
    {
        var entries = new Dictionary<string, CompressionInputEntry>(StringComparer.OrdinalIgnoreCase);
        var normalizedPatterns = excludePatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim().Replace('\\', '/'))
            .ToArray();

        foreach (var source in sourcePaths)
        {
            var fullSourcePath = Path.GetFullPath(source);
            if (File.Exists(fullSourcePath))
            {
                var info = new FileInfo(fullSourcePath);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Symbolic links and reparse points are not added to archives: {source}");

                if (PathsEqual(fullSourcePath, outputPath))
                    throw new InvalidDataException("The output archive cannot also be one of its source files.");

                AddFile(info, Path.GetFileName(fullSourcePath));
            }
            else if (Directory.Exists(fullSourcePath))
            {
                var root = new DirectoryInfo(fullSourcePath);
                if ((root.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Symbolic links and reparse points are not added to archives: {source}");

                var baseDirectory = Path.GetDirectoryName(fullSourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    ?? fullSourcePath;
                AddDirectory(root, baseDirectory);
            }
            else
            {
                throw new FileNotFoundException("A source file or folder no longer exists.", source);
            }
        }

        return entries.Values.ToList();

        void AddDirectory(DirectoryInfo directory, string baseDirectory)
        {
            var relativeDirectory = NormalizeEntryPath(Path.GetRelativePath(baseDirectory, directory.FullName));
            if (IsExcluded(relativeDirectory, Path.GetFileName(directory.Name), normalizedPatterns))
                return;

            var includedChildCount = 0;
            foreach (var filePath in Directory.EnumerateFiles(directory.FullName, "*", ImmediateChildrenOptions))
            {
                var info = new FileInfo(filePath);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                var relativeFile = NormalizeEntryPath(Path.GetRelativePath(baseDirectory, filePath));
                if (IsOutputFile(filePath, outputPath) || IsExcluded(relativeFile, info.Name, normalizedPatterns))
                    continue;

                AddFile(info, relativeFile);
                includedChildCount++;
            }

            foreach (var subdirectoryPath in Directory.EnumerateDirectories(directory.FullName, "*", ImmediateChildrenOptions))
            {
                var subdirectory = new DirectoryInfo(subdirectoryPath);
                var before = entries.Count;
                AddDirectory(subdirectory, baseDirectory);
                if (entries.Count > before)
                    includedChildCount++;
            }

            if (includedChildCount == 0)
            {
                var directoryEntryPath = relativeDirectory.EndsWith('/') ? relativeDirectory : relativeDirectory + "/";
                entries.TryAdd(directoryEntryPath, new CompressionInputEntry(
                    directoryEntryPath, string.Empty, 0, directory.CreationTimeUtc, directory.LastWriteTimeUtc, IsDirectory: true));
            }
        }

        void AddFile(FileInfo info, string entryPath)
        {
            if (IsExcluded(entryPath, info.Name, normalizedPatterns))
                return;

            if (IsOutputFile(info.FullName, outputPath))
                return;

            if (!entries.TryAdd(entryPath, new CompressionInputEntry(
                    entryPath, info.FullName, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, IsDirectory: false)))
            {
                throw new InvalidDataException($"More than one source maps to the archive entry '{entryPath}'.");
            }
        }
    }

    private static bool IsExcluded(string relativePath, string name, IReadOnlyList<string> patterns)
    {
        var normalizedPath = NormalizeEntryPath(relativePath);
        return patterns.Any(pattern =>
            FileSystemName.MatchesSimpleExpression(pattern, normalizedPath, ignoreCase: true) ||
            FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true));
    }

    private static bool IsOutputFile(string fullPath, string outputPath)
    {
        if (PathsEqual(fullPath, outputPath))
            return true;

        var outputVolumePrefix = outputPath + ".";
        if (!fullPath.StartsWith(outputVolumePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = fullPath[outputVolumePrefix.Length..];
        return suffix.Length >= 3 && suffix.All(char.IsAsciiDigit);
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEntryPath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static List<string> GetArchiveOutputGroup(string folder, string archiveName)
    {
        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => IsArchiveOutputName(Path.GetFileName(path), archiveName))
            .ToList();
    }

    private static bool IsArchiveOutputName(string candidate, string archiveName)
    {
        if (candidate.Equals(archiveName, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = archiveName + ".";
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = candidate[prefix.Length..];
        return suffix.Length >= 3 && suffix.All(char.IsAsciiDigit);
    }

    private static void CommitArchiveOutputGroup(
        string temporaryDirectory,
        string outputDirectory,
        string archiveName,
        IReadOnlyList<string> stagedOutputs)
    {
        var existingOutputs = GetArchiveOutputGroup(outputDirectory, archiveName);
        var backupDirectory = Path.Combine(temporaryDirectory, "previous");
        Directory.CreateDirectory(backupDirectory);
        var backups = new List<(string Original, string Backup)>();
        var installedOutputs = new List<string>();

        try
        {
            foreach (var existing in existingOutputs)
            {
                var backup = Path.Combine(backupDirectory, Path.GetFileName(existing));
                File.Move(existing, backup);
                backups.Add((existing, backup));
            }

            foreach (var staged in stagedOutputs)
            {
                var target = Path.Combine(outputDirectory, Path.GetFileName(staged));
                File.Move(staged, target);
                installedOutputs.Add(target);
            }
        }
        catch (Exception commitError)
        {
            foreach (var installed in installedOutputs)
            {
                try { File.Delete(installed); } catch (IOException) { }
            }

            var recoveryErrors = new List<Exception>();
            foreach (var (original, backup) in backups.AsEnumerable().Reverse())
            {
                try { File.Move(backup, original); }
                catch (Exception ex) { recoveryErrors.Add(ex); }
            }

            if (recoveryErrors.Count > 0)
            {
                throw new ArchiveCommitException(
                    $"Archive replacement failed ({commitError.Message}) and restoring the previous archive also failed ({string.Join("; ", recoveryErrors.Select(error => error.Message))}).",
                    temporaryDirectory,
                    commitError);
            }

            throw;
        }
    }

    private sealed record CompressionInputEntry(
        string EntryPath,
        string SourcePath,
        long Length,
        DateTime CreationTimeUtc,
        DateTime LastWriteTimeUtc,
        bool IsDirectory);

    private sealed class CompressionProgress(IProgress<OperationProgress>? sink, Stopwatch stopwatch)
    {
        private long _bytesRead;
        private long _lastReportedBytes;
        private long _totalBytes;
        private int _filesCompleted;
        private int _totalFiles;
        private string _currentItem = string.Empty;

        public long BytesRead => Interlocked.Read(ref _bytesRead);
        public int FilesCompleted => Math.Min(Volatile.Read(ref _filesCompleted), Volatile.Read(ref _totalFiles));

        public void Configure(long totalBytes, int totalFiles)
        {
            _totalBytes = totalBytes;
            _totalFiles = totalFiles;
        }

        public void SetCurrentItem(string name)
        {
            Volatile.Write(ref _currentItem, name);
            ReportCurrent();
        }

        public void AddBytesRead(int bytes)
        {
            if (bytes <= 0)
                return;

            var totalRead = Interlocked.Add(ref _bytesRead, bytes);
            if (totalRead - Interlocked.Read(ref _lastReportedBytes) >= 1024 * 1024)
                ReportCurrent();
        }

        public void FileCompleted()
        {
            Interlocked.Increment(ref _filesCompleted);
            ReportCurrent();
        }

        public void Complete()
        {
            Interlocked.Exchange(ref _bytesRead, _totalBytes);
            Interlocked.Exchange(ref _filesCompleted, _totalFiles);
            ReportCurrent();
        }

        public void ReportCurrent()
        {
            var bytes = BytesRead;
            Interlocked.Exchange(ref _lastReportedBytes, bytes);
            var seconds = stopwatch.Elapsed.TotalSeconds;
            var speed = seconds > 0.1 ? bytes / seconds : 0;
            var remainingBytes = Math.Max(0, _totalBytes - bytes);
            sink?.Report(new OperationProgress
            {
                BytesProcessed = bytes,
                TotalBytes = _totalBytes,
                FilesProcessed = FilesCompleted,
                TotalFiles = _totalFiles,
                CurrentItemName = Volatile.Read(ref _currentItem),
                BytesPerSecond = speed,
                EstimatedTimeRemaining = speed > 0
                    ? TimeSpan.FromSeconds(remainingBytes / speed)
                    : null,
                OperationPhase = "Compressing"
            });
        }
    }

    private sealed class CancellationCheckingStream(
        Stream inner,
        Action<int> reportBytes,
        CancellationToken operationToken) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            operationToken.ThrowIfCancellationRequested();
            var read = inner.Read(buffer, offset, count);
            reportBytes(read);
            operationToken.ThrowIfCancellationRequested();
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            operationToken.ThrowIfCancellationRequested();
            var read = inner.Read(buffer);
            reportBytes(read);
            operationToken.ThrowIfCancellationRequested();
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operationToken);
            cancellationToken.ThrowIfCancellationRequested();
            operationToken.ThrowIfCancellationRequested();
            var read = await inner.ReadAsync(buffer, linkedSource.Token).ConfigureAwait(false);
            reportBytes(read);
            operationToken.ThrowIfCancellationRequested();
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class ArchiveCommitException(string message, string recoveryDirectory, Exception innerException)
        : IOException(message, innerException)
    {
        public string? RecoveryDirectory { get; } = recoveryDirectory;
    }
}
