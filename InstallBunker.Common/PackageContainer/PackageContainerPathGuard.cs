using System.Text.RegularExpressions;

namespace InstallBunker.Common.PackageContainer;

public static class PackageContainerPathGuard
{
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON","PRN","AUX","NUL",
        "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
    };

    public static string GetSafeDestinationPath(string destinationRoot, string entryFullName)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot))
            throw new PackageContainerReadException(PackageContainerErrorKind.ExtractionFailed, "Destination root is required.");

        if (string.IsNullOrWhiteSpace(entryFullName))
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryName, "Package contains an empty entry name.");

        var normalized = entryFullName.Replace('\\', '/').Trim();

        if (normalized.EndsWith("/", StringComparison.Ordinal))
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryName, "Package contains a directory entry where a file was expected.");

        if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.StartsWith("//", StringComparison.Ordinal))
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryPath, "Package contains an absolute path entry.");

        if (Regex.IsMatch(normalized, @"^[A-Za-z]:"))
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryPath, "Package contains a rooted drive path entry.");

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryName, "Package contains an invalid entry name.");

        foreach (var seg in segments)
        {
            if (seg is "." or "..")
                throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryPath, "Package contains a traversal path segment.");

            ValidateSegment(seg);
        }

        var destRootFull = Path.GetFullPath(destinationRoot);
        var candidate = Path.GetFullPath(Path.Combine(destRootFull, string.Join(Path.DirectorySeparatorChar, segments)));

        var rootWithSep = destRootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryPath, "Package contains an invalid entry path.");

        return candidate;
    }

    private static void ValidateSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryName, "Package contains an empty path segment.");

        var invalidFileChars = Path.GetInvalidFileNameChars();
        if (segment.IndexOfAny(invalidFileChars) >= 0)
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryName, $"Package contains invalid characters in entry name: {segment}");

        var baseName = segment.Split('.', StringSplitOptions.RemoveEmptyEntries)[0];
        if (WindowsReservedNames.Contains(baseName))
            throw new PackageContainerReadException(PackageContainerErrorKind.InvalidEntryName, $"Package contains a reserved Windows name: {segment}");
    }
}