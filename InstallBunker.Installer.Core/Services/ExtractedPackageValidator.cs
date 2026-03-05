using InstallBunker.Common.Serialization;
using InstallBunker.Domain.Models;
using InstallBunker.Installer.Core.Models;

namespace InstallBunker.Installer.Core.Services;

public sealed class ExtractedPackageValidator
{
    public ExtractedPackageValidationResult Validate(string packageRootDirectory)
    {
        var result = new ExtractedPackageValidationResult();

        if (string.IsNullOrWhiteSpace(packageRootDirectory))
        {
            result.AddError("Package root directory is required.");
            return result;
        }

        if (!Directory.Exists(packageRootDirectory))
        {
            result.AddError($"Package root directory not found: {packageRootDirectory}");
            return result;
        }

        var packageRootFullPath = Path.GetFullPath(packageRootDirectory);

        var manifestPath = Path.Combine(packageRootFullPath, "manifest.json");
        var uninstallPath = Path.Combine(packageRootFullPath, "Uninstall.exe");
        var payloadDirectory = Path.Combine(packageRootFullPath, "payload");

        if (!File.Exists(manifestPath))
        {
            result.AddError("manifest.json was not found in the extracted package root.");
        }

        if (!File.Exists(uninstallPath))
        {
            result.AddError("Uninstall.exe was not found in the extracted package root.");
        }

        if (!Directory.Exists(payloadDirectory))
        {
            result.AddError("payload folder was not found in the extracted package root.");
        }
        else
        {
            var payloadHasFiles = Directory.EnumerateFiles(
                payloadDirectory,
                "*",
                SearchOption.AllDirectories).Any();

            if (!payloadHasFiles)
            {
                result.AddError("payload folder is empty.");
            }
        }

        if (!File.Exists(manifestPath))
        {
            return result;
        }

        PackageManifest manifest;

        try
        {
            manifest = JsonFileStore.Load<PackageManifest>(manifestPath);
        }
        catch (Exception ex)
        {
            result.AddError($"manifest.json could not be loaded: {ex.Message}");
            return result;
        }

        var manifestErrors = manifest.Validate();

        foreach (var manifestError in manifestErrors)
        {
            result.AddError($"Manifest: {manifestError}");
        }

        if (!string.IsNullOrWhiteSpace(manifest.PackageInfo.MainExecutable))
        {
            var mainExecutablePath = ResolvePathInsideRoot(
                payloadDirectory,
                manifest.PackageInfo.MainExecutable);

            if (string.IsNullOrWhiteSpace(mainExecutablePath))
            {
                result.AddError("PackageInfo.MainExecutable points to an invalid path inside payload.");
            }
            else if (!File.Exists(mainExecutablePath))
            {
                result.AddError(
                    $"PackageInfo.MainExecutable was not found inside payload: {manifest.PackageInfo.MainExecutable}");
            }
        }

        if (manifest.Ui.ShowLicensePage)
        {
            if (string.IsNullOrWhiteSpace(manifest.Ui.LicenseFile))
            {
                result.AddError("Ui.LicenseFile is required when Ui.ShowLicensePage is true.");
            }
            else
            {
                var licensePath = ResolvePathInsideRoot(
                    packageRootFullPath,
                    manifest.Ui.LicenseFile);

                if (string.IsNullOrWhiteSpace(licensePath))
                {
                    result.AddError("Ui.LicenseFile points to an invalid path.");
                }
                else if (!File.Exists(licensePath))
                {
                    result.AddError(
                        $"License file declared in manifest was not found: {manifest.Ui.LicenseFile}");
                }
            }
        }

        return result;
    }

    private static string? ResolvePathInsideRoot(string rootDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalizedRelativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedRelativePath))
        {
            return null;
        }

        var fullRootPath = Path.GetFullPath(rootDirectory);
        var candidatePath = Path.GetFullPath(Path.Combine(fullRootPath, normalizedRelativePath));

        var normalizedRootWithSeparator = fullRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(normalizedRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return candidatePath;
    }
}