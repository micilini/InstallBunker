using InstallBunker.Common.Serialization;
using InstallBunker.Domain.Models;
using InstallBunker.Uninstaller.Core.Models;
using Microsoft.Win32;

namespace InstallBunker.Uninstaller.Core.Services;

public sealed class PackageUninstaller
{
    public UninstallResult Uninstall(
        UninstallRequest request,
        IProgress<UninstallProgressInfo>? progress = null)
    {
        ValidateRequest(request);

        var receiptPath = Path.GetFullPath(request.ReceiptFilePath);

        Report(progress, 5, "Loading install receipt...");

        if (!File.Exists(receiptPath))
        {
            throw new FileNotFoundException("Install receipt file not found.", receiptPath);
        }

        var receipt = JsonFileStore.Load<InstallReceipt>(receiptPath);
        var receiptErrors = receipt.Validate();

        if (receiptErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Install receipt is invalid: " + string.Join(" | ", receiptErrors));
        }

        var result = new UninstallResult
        {
            ReceiptFilePath = receiptPath,
            InstallDirectory = receipt.InstallDirectory
        };

        Report(progress, 20, "Removing shortcuts...");
        RemoveShortcuts(receipt, result);

        Report(progress, 40, "Removing Windows uninstall registration...");
        RemoveRegistryKeys(receipt, result);

        Report(progress, 55, "Removing installed files...");
        RemoveInstalledFiles(receipt, receiptPath, result, progress);

        if (request.RemoveInstallDirectoryIfEmpty)
        {
            Report(progress, 95, "Cleaning empty directories...");
            TryDeleteEmptyDirectoriesUpward(receipt.InstallDirectory);
        }

        Report(progress, 100, "Uninstall completed successfully.");
        return result;
    }

    private static void ValidateRequest(UninstallRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReceiptFilePath))
        {
            throw new InvalidOperationException("ReceiptFilePath is required.");
        }
    }

    private static void RemoveShortcuts(InstallReceipt receipt, UninstallResult result)
    {
        foreach (var shortcut in receipt.CreatedShortcuts
                     .Select(x => x.Path)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryDeleteFile(shortcut))
            {
                result.RemovedShortcutsCount++;
            }
            else
            {
                result.FailedShortcutRemovalsCount++;
            }
        }
    }

    private static void RemoveRegistryKeys(InstallReceipt receipt, UninstallResult result)
    {
        foreach (var registryKey in receipt.RegistryKeys
                     .Select(x => x.Path)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryDeleteRegistryKey(registryKey))
            {
                result.RemovedRegistryKeysCount++;
            }
            else
            {
                result.FailedRegistryRemovalsCount++;
            }
        }
    }

    private static void RemoveInstalledFiles(
        InstallReceipt receipt,
        string receiptPath,
        UninstallResult result,
        IProgress<UninstallProgressInfo>? progress)
    {
        var installedFiles = receipt.InstalledFiles
            .Select(x => x.Path)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .ToList();

        var total = installedFiles.Count == 0 ? 1 : installedFiles.Count;

        for (int i = 0; i < installedFiles.Count; i++)
        {
            var filePath = installedFiles[i];

            if (string.Equals(
                Path.GetFullPath(filePath),
                Path.GetFullPath(receiptPath),
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryDeleteFile(filePath))
            {
                result.RemovedFilesCount++;
            }
            else
            {
                result.FailedFileRemovalsCount++;
            }

            var percentage = 55 + (int)Math.Round(((i + 1d) / total) * 35d);
            Report(progress, percentage, $"Removing: {Path.GetFileName(filePath)}");
        }

        if (TryDeleteFile(receiptPath))
        {
            result.RemovedFilesCount++;
        }
        else
        {
            result.FailedFileRemovalsCount++;
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return true;
            }

            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeleteRegistryKey(string registryKeyPath)
    {
        try
        {
            if (registryKeyPath.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
            {
                var subKeyPath = registryKeyPath.Substring(5);
                Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
                return true;
            }

            if (registryKeyPath.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase))
            {
                var subKeyPath = registryKeyPath.Substring(5);
                Registry.LocalMachine.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteEmptyDirectoriesUpward(string startDirectory)
    {
        try
        {
            var current = new DirectoryInfo(startDirectory);

            while (current is not null && current.Exists)
            {
                if (current.EnumerateFileSystemInfos().Any())
                {
                    break;
                }

                var parent = current.Parent;
                current.Delete();
                current = parent;
            }
        }
        catch
        {
        }
    }

    private static void Report(
        IProgress<UninstallProgressInfo>? progress,
        int percentage,
        string message)
    {
        progress?.Report(new UninstallProgressInfo
        {
            Percentage = Math.Clamp(percentage, 0, 100),
            Message = message
        });
    }
}