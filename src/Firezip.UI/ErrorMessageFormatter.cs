using Firezip.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Firezip.UI;

internal static class ErrorMessageFormatter
{
    public static string Format(string title, string technicalMessage, ILocalizationService? localizationService = null)
    {
        var loc = localizationService ?? (App.Services != null ? App.Services.GetService<ILocalizationService>() : null);
        var message = technicalMessage ?? string.Empty;

        if (ContainsAny(message, "password", "encrypted", "cryptographic"))
            return loc?.GetString("Error_PasswordIncorrect") ?? "The password may be incorrect, or this archive may use encryption that Firezip cannot read. Check the password and try again.";

        if (ContainsAny(message, "security violation", "unsafe path", "path traversal", "reparse point"))
            return loc?.GetString("Error_SecurityViolation") ?? "Firezip blocked an unsafe archive entry to protect files outside the selected destination.";

        if (ContainsAny(message, "unauthorized", "access is denied", "permission"))
            return loc?.GetString("Error_AccessDenied") ?? "Windows denied access to a file or folder. Check its permissions and make sure it is not being used by another app.";

        if (ContainsAny(message, "directorynotfound", "directory not found", "path not found", "could not find a part of the path"))
            return loc?.GetString("Error_NotFound") ?? "A file or folder needed for this operation could not be found. Check the selected location and try again.";

        if (ContainsAny(message, "invalid data", "corrupt", "damaged", "unexpected end", "central directory", "invalid signature", "not a valid archive", "unsupported format"))
            return loc?.GetString("Error_CorruptArchive") ?? "Firezip could not read this archive. It may be damaged, incomplete, or in a format this build cannot open.";

        if (ContainsAny(message, "used by another process", "being used by another process", "sharing violation"))
            return loc?.GetString("Error_SharingViolation") ?? "A file needed for this operation is open in another app. Close it and try again.";

        if (title.Contains("integrity", StringComparison.OrdinalIgnoreCase))
            return loc?.GetString("Error_IntegrityFailed") ?? "Firezip could not verify this archive. It may be damaged or incomplete.";

        if (title.Contains("extract", StringComparison.OrdinalIgnoreCase))
            return loc?.GetString("Error_ExtractFailed") ?? "Firezip could not finish extracting the selected files. Check the destination and archive, then try again.";

        if (title.Contains("compress", StringComparison.OrdinalIgnoreCase))
            return loc?.GetString("Error_CompressFailed") ?? "Firezip could not create the archive. Check the selected files and destination, then try again.";

        return loc?.GetString("Error_Generic") ?? "Firezip could not complete this operation. Check the file or folder and try again.";
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
