namespace Firezip.Core.Models;

/// <summary>
/// Supported archive formats.
/// </summary>
public enum ArchiveFormat
{
    Unknown = 0,
    Zip = 1,
    SevenZip = 2,
    Tar = 3,
    GZip = 4,
    BZip2 = 5,
    Xz = 6,
    Rar = 7,
    Iso = 8,
    Cab = 9
}

public static class ArchiveFormatExtensions
{
    public static string GetDefaultExtension(this ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => ".zip",
        ArchiveFormat.SevenZip => ".7z",
        ArchiveFormat.Tar => ".tar",
        ArchiveFormat.GZip => ".tar.gz",
        ArchiveFormat.BZip2 => ".tar.bz2",
        ArchiveFormat.Xz => ".tar.xz",
        ArchiveFormat.Rar => ".rar",
        ArchiveFormat.Iso => ".iso",
        ArchiveFormat.Cab => ".cab",
        _ => ".zip"
    };

    public static string GetDisplayName(this ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => "ZIP Archive (.zip)",
        ArchiveFormat.SevenZip => "7-Zip Archive (.7z)",
        ArchiveFormat.Tar => "Tape Archive (.tar)",
        ArchiveFormat.GZip => "GZip Archive (.gz, .tgz)",
        ArchiveFormat.BZip2 => "BZip2 Archive (.bz2, .tbz2)",
        ArchiveFormat.Xz => "XZ Archive (.xz, .txz)",
        ArchiveFormat.Rar => "RAR Archive (.rar)",
        ArchiveFormat.Iso => "ISO Disk Image (.iso)",
        ArchiveFormat.Cab => "Cabinet Archive (.cab)",
        _ => "Unknown Format"
    };

    public static bool CanCreate(this ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => true,
        ArchiveFormat.SevenZip => true,
        ArchiveFormat.Tar => true,
        ArchiveFormat.GZip => true,
        ArchiveFormat.BZip2 => true,
        ArchiveFormat.Xz => true,
        _ => false // RAR creation is not supported or licensed
    };
}
