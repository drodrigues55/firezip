using Firezip.Core.Models;

namespace Firezip.Formats;

/// <summary>
/// Detects archive format by examining magic headers (signatures) and fallback file extensions.
/// </summary>
public static class FormatDetector
{
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] ZipEmptySignature = [0x50, 0x4B, 0x05, 0x06];
    private static readonly byte[] ZipSpannedSignature = [0x50, 0x4B, 0x07, 0x08];
    private static readonly byte[] SevenZipSignature = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];
    private static readonly byte[] RarSignatureV4 = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00];
    private static readonly byte[] RarSignatureV5 = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00];
    private static readonly byte[] GzipSignature = [0x1F, 0x8B];
    private static readonly byte[] Bzip2Signature = [0x42, 0x5A, 0x68];
    private static readonly byte[] XzSignature = [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00];
    private static readonly byte[] TarUstarSignature = [0x75, 0x73, 0x74, 0x61, 0x72]; // "ustar"

    public static ArchiveFormat DetectFormat(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return DetectByExtension(filePath);

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[512];
            int read = stream.Read(buffer, 0, buffer.Length);

            if (read >= 2)
            {
                if (Match(buffer, GzipSignature)) return ArchiveFormat.GZip;
                if (read >= 3 && Match(buffer, Bzip2Signature)) return ArchiveFormat.BZip2;
                if (read >= 4 && (Match(buffer, ZipSignature) || Match(buffer, ZipEmptySignature) || Match(buffer, ZipSpannedSignature)))
                    return ArchiveFormat.Zip;
                if (read >= 6 && Match(buffer, SevenZipSignature)) return ArchiveFormat.SevenZip;
                if (read >= 6 && Match(buffer, XzSignature)) return ArchiveFormat.Xz;
                if (read >= 7 && (Match(buffer, RarSignatureV4) || Match(buffer, RarSignatureV5))) return ArchiveFormat.Rar;

                // Check TAR ustar signature at offset 257
                if (read >= 262 && MatchAtOffset(buffer, 257, TarUstarSignature))
                    return ArchiveFormat.Tar;

                // Check ISO 9660 signature at offset 0x8000 (32768)
                if (stream.Length > 32773)
                {
                    stream.Seek(32768, SeekOrigin.Begin);
                    var isoBuffer = new byte[6];
                    if (stream.Read(isoBuffer, 0, isoBuffer.Length) == 6 &&
                        isoBuffer[1] == 'C' && isoBuffer[2] == 'D' && isoBuffer[3] == '0' && isoBuffer[4] == '0' && isoBuffer[5] == '1')
                    {
                        return ArchiveFormat.Iso;
                    }
                }
            }
        }
        catch
        {
            // Fall back to extension detection on stream read error
        }

        return DetectByExtension(filePath);
    }

    public static ArchiveFormat DetectByExtension(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return ArchiveFormat.Unknown;

        var lower = filePath.ToLowerInvariant();
        if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz")) return ArchiveFormat.GZip;
        if (lower.EndsWith(".tar.bz2") || lower.EndsWith(".tbz2")) return ArchiveFormat.BZip2;
        if (lower.EndsWith(".tar.xz") || lower.EndsWith(".txz")) return ArchiveFormat.Xz;
        if (lower.EndsWith(".zip") || lower.EndsWith(".zipx") || lower.EndsWith(".jar") || lower.EndsWith(".apk")) return ArchiveFormat.Zip;
        if (lower.EndsWith(".7z")) return ArchiveFormat.SevenZip;
        if (lower.EndsWith(".tar")) return ArchiveFormat.Tar;
        if (lower.EndsWith(".gz")) return ArchiveFormat.GZip;
        if (lower.EndsWith(".bz2")) return ArchiveFormat.BZip2;
        if (lower.EndsWith(".xz")) return ArchiveFormat.Xz;
        if (lower.EndsWith(".rar")) return ArchiveFormat.Rar;
        if (lower.EndsWith(".iso")) return ArchiveFormat.Iso;
        if (lower.EndsWith(".cab")) return ArchiveFormat.Cab;

        return ArchiveFormat.Unknown;
    }

    private static bool Match(byte[] buffer, byte[] signature)
    {
        if (buffer.Length < signature.Length) return false;
        for (int i = 0; i < signature.Length; i++)
        {
            if (buffer[i] != signature[i]) return false;
        }
        return true;
    }

    private static bool MatchAtOffset(byte[] buffer, int offset, byte[] signature)
    {
        if (buffer.Length < offset + signature.Length) return false;
        for (int i = 0; i < signature.Length; i++)
        {
            if (buffer[offset + i] != signature[i]) return false;
        }
        return true;
    }
}
