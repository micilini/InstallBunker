using InstallBunker.Common.Serialization;
using InstallBunker.Domain.Enums;
using InstallBunker.Domain.Models;
using InstallBunker.Installer.Core.Models;
using Microsoft.Win32;

namespace InstallBunker.Installer.Core.Services;

public sealed class PackageInstaller
{
    public InstallPackageResult Install(
        InstallPackageRequest request,
        IProgress<InstallProgressInfo>? progress = null)
    {
        Report(progress, 0, "Loading package information...");
        ValidateRequest(request);

        var packageRootDirectory = Path.GetFullPath(request.PackageRootDirectory);
        var manifestPath = Path.Combine(packageRootDirectory, "manifest.json");
        var payloadDirectory = Path.Combine(packageRootDirectory, "payload");
        var packagedUninstallerPath = Path.Combine(packageRootDirectory, "Uninstall.exe");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("manifest.json not found.", manifestPath);
        }

        if (!Directory.Exists(payloadDirectory))
        {
            throw new DirectoryNotFoundException($"Payload directory not found: {payloadDirectory}");
        }

        Report(progress, 5, "Reading manifest.json...");
        var manifest = JsonFileStore.Load<PackageManifest>(manifestPath);
        var manifestErrors = manifest.Validate();

        if (manifestErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Package manifest is invalid: " + string.Join(" | ", manifestErrors));
        }

        Report(progress, 10, "Validating payload files...");
        ValidatePayloadFiles(manifest, payloadDirectory);

        var installScope = request.InstallScopeOverride ?? manifest.InstallSettings.DefaultInstallScope;

        var installDirectory = !string.IsNullOrWhiteSpace(request.OverrideInstallDirectory)
            ? Path.GetFullPath(request.OverrideInstallDirectory)
            : ResolveInstallDirectory(manifest, installScope);

        var createDesktopShortcut = request.CreateDesktopShortcutOverride ?? manifest.Shortcuts.Desktop;
        var createStartMenuShortcut = request.CreateStartMenuShortcutOverride ?? manifest.Shortcuts.StartMenu;

        Directory.CreateDirectory(installDirectory);

        var mainExecutablePath = Path.Combine(installDirectory, manifest.PackageInfo.MainExecutable);
        var receiptPath = Path.Combine(installDirectory, "install.receipt.json");
        var installedUninstallerPath = Path.Combine(installDirectory, "Uninstall.exe");
        var uninstallCommandPath = installedUninstallerPath;

        var receipt = new InstallReceipt
        {
            AppName = manifest.PackageInfo.AppName,
            Version = manifest.PackageInfo.Version,
            InstallScope = installScope,
            InstallDirectory = installDirectory,
            Branding = manifest.Branding,
            InstalledAtUtc = DateTime.UtcNow
        };

        string registryKeyPath = string.Empty;

        try
        {
            Report(progress, 15, "Copying application files...");

            var totalFiles = manifest.Files.Count == 0 ? 1 : manifest.Files.Count;

            for (int i = 0; i < manifest.Files.Count; i++)
            {
                var file = manifest.Files[i];
                var sourceFilePath = Path.Combine(payloadDirectory, file.Source);
                var destinationFilePath = Path.Combine(installDirectory, file.RelativeTarget);
                var destinationDirectory = Path.GetDirectoryName(destinationFilePath);

                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourceFilePath, destinationFilePath, overwrite: file.Overwrite);

                receipt.InstalledFiles.Add(new InstalledFileRecord
                {
                    Path = destinationFilePath
                });

                var percentage = 15 + (int)Math.Round(((i + 1d) / totalFiles) * 55d);
                Report(progress, percentage, $"Copying: {file.RelativeTarget}");
            }

            if (File.Exists(packagedUninstallerPath))
            {
                Report(progress, 74, "Installing visual uninstaller...");
                File.Copy(packagedUninstallerPath, installedUninstallerPath, overwrite: true);
            }
            else
            {
                Report(progress, 74, "Visual uninstaller not found in package. Falling back to placeholder...");
                CreatePlaceholderUninstallCommand(
                    installedUninstallerPath,
                    manifest.PackageInfo.AppName,
                    installDirectory);
            }

            if (createDesktopShortcut)
            {
                Report(progress, 82, "Creating desktop shortcut...");

                var desktopShortcutPath = CreateShortcut(
                    GetDesktopShortcutDirectory(installScope),
                    $"{manifest.PackageInfo.AppName}.lnk",
                    mainExecutablePath,
                    installDirectory,
                    mainExecutablePath);

                receipt.CreatedShortcuts.Add(new InstalledShortcutRecord
                {
                    Path = desktopShortcutPath
                });
            }

            if (createStartMenuShortcut)
            {
                Report(progress, 88, "Creating Start Menu shortcut...");

                var startMenuShortcutPath = CreateShortcut(
                    GetStartMenuShortcutDirectory(installScope),
                    $"{manifest.PackageInfo.AppName}.lnk",
                    mainExecutablePath,
                    installDirectory,
                    mainExecutablePath);

                receipt.CreatedShortcuts.Add(new InstalledShortcutRecord
                {
                    Path = startMenuShortcutPath
                });
            }

            Report(progress, 93, "Registering application in Windows...");
            registryKeyPath = CreateUninstallRegistryEntry(
                manifest,
                installScope,
                installDirectory,
                uninstallCommandPath,
                mainExecutablePath);

            receipt.RegistryKeys.Add(new InstalledRegistryRecord
            {
                Path = registryKeyPath
            });

            Report(progress, 97, "Writing install receipt...");
            JsonFileStore.Save(receiptPath, receipt);

            receipt.InstalledFiles.Add(new InstalledFileRecord
            {
                Path = receiptPath
            });

            JsonFileStore.Save(receiptPath, receipt);

            Report(progress, 100, "Installation completed successfully.");

            return new InstallPackageResult
            {
                PackageRootDirectory = packageRootDirectory,
                InstallDirectory = installDirectory,
                MainExecutablePath = mainExecutablePath,
                ReceiptFilePath = receiptPath,
                UninstallCommandPath = uninstallCommandPath,
                RegistryKeyPath = registryKeyPath,
                InstalledFilesCount = receipt.InstalledFiles.Count,
                CreatedShortcutsCount = receipt.CreatedShortcuts.Count
            };
        }
        catch
        {
            Report(progress, 100, "Installation failed. Rolling back changes...");
            Rollback(receipt, registryKeyPath, installedUninstallerPath);
            throw;
        }
    }

    private static void ValidateRequest(InstallPackageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PackageRootDirectory))
        {
            throw new InvalidOperationException("PackageRootDirectory is required.");
        }

        if (!Directory.Exists(request.PackageRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Package root directory not found: {request.PackageRootDirectory}");
        }
    }

    private static void ValidatePayloadFiles(PackageManifest manifest, string payloadDirectory)
    {
        foreach (var file in manifest.Files)
        {
            var sourceFilePath = Path.Combine(payloadDirectory, file.Source);

            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException(
                    $"Payload file declared in manifest was not found: {file.Source}",
                    sourceFilePath);
            }
        }

        var mainExecutablePayloadPath = Path.Combine(payloadDirectory, manifest.PackageInfo.MainExecutable);

        if (!File.Exists(mainExecutablePayloadPath))
        {
            throw new FileNotFoundException(
                "Main executable declared in manifest was not found in payload.",
                mainExecutablePayloadPath);
        }

        if (manifest.Ui.ShowLicensePage && !string.IsNullOrWhiteSpace(manifest.Ui.LicenseFile))
        {
            var licensePath = Path.Combine(
                Path.GetDirectoryName(payloadDirectory) ?? string.Empty,
                manifest.Ui.LicenseFile);

            if (!File.Exists(licensePath))
            {
                throw new FileNotFoundException(
                    "License file declared in manifest was not found beside manifest.json.",
                    licensePath);
            }
        }
    }

    private static string ResolveInstallDirectory(PackageManifest manifest, InstallScope installScope)
    {
        var rawPath = installScope == InstallScope.PerMachine
            ? manifest.InstallSettings.DefaultInstallDirPerMachine
            : manifest.InstallSettings.DefaultInstallDirPerUser;

        return Environment.ExpandEnvironmentVariables(rawPath);
    }

    private static string CreateShortcut(
        string baseDirectory,
        string shortcutFileName,
        string targetPath,
        string workingDirectory,
        string iconPath)
    {
        Directory.CreateDirectory(baseDirectory);

        var shortcutPath = Path.Combine(baseDirectory, shortcutFileName);
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell COM type not found.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Failed to create WScript.Shell COM instance.");

        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.IconLocation = iconPath;
        shortcut.Save();

        return shortcutPath;
    }

    private static string GetDesktopShortcutDirectory(InstallScope installScope)
    {
        return installScope == InstallScope.PerMachine
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private static string GetStartMenuShortcutDirectory(InstallScope installScope)
    {
        return installScope == InstallScope.PerMachine
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
            : Environment.GetFolderPath(Environment.SpecialFolder.Programs);
    }

    private static void CreatePlaceholderUninstallCommand(
        string uninstallCommandPath,
        string appName,
        string installDirectory)
    {
        var content =
$@"@echo off
title {appName} - Uninstall Placeholder
echo ==========================================
echo InstallBunker MVP
echo.
echo The visual uninstaller was not bundled in this package.
echo.
echo App: {appName}
echo Install folder: {installDirectory}
echo.
pause";

        File.WriteAllText(uninstallCommandPath, content);
    }

    private static string CreateUninstallRegistryEntry(
        PackageManifest manifest,
        InstallScope installScope,
        string installDirectory,
        string uninstallCommandPath,
        string mainExecutablePath)
    {
        var root = installScope == InstallScope.PerMachine
            ? Registry.LocalMachine
            : Registry.CurrentUser;

        var basePath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        var appToken = SanitizeToken(manifest.PackageInfo.AppName);
        var subKeyName = $"{appToken}_{Guid.NewGuid():N}";
        var fullSubKeyPath = $@"{basePath}\{subKeyName}";

        using var key = root.CreateSubKey(fullSubKeyPath, writable: true)
            ?? throw new InvalidOperationException("Failed to create uninstall registry key.");

        key.SetValue("DisplayName", manifest.PackageInfo.AppName);
        key.SetValue("DisplayVersion", manifest.PackageInfo.Version);
        key.SetValue("Publisher", manifest.PackageInfo.Publisher);
        key.SetValue("InstallLocation", installDirectory);
        key.SetValue("DisplayIcon", mainExecutablePath);
        key.SetValue("UninstallString", $@"""{uninstallCommandPath}""");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

        return installScope == InstallScope.PerMachine
            ? $@"HKLM\{fullSubKeyPath}"
            : $@"HKCU\{fullSubKeyPath}";
    }

    private static string SanitizeToken(string value)
    {
        var filteredChars = value
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return filteredChars.Length == 0
            ? "InstallBunkerApp"
            : new string(filteredChars);
    }

    private static void Rollback(InstallReceipt receipt, string registryKeyPath, string installedUninstallerPath)
    {
        foreach (var shortcut in receipt.CreatedShortcuts)
        {
            TryDeleteFile(shortcut.Path);
        }

        if (!string.IsNullOrWhiteSpace(registryKeyPath))
        {
            TryDeleteRegistryKey(registryKeyPath);
        }

        foreach (var file in receipt.InstalledFiles
                     .Select(x => x.Path)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(x => x.Length))
        {
            TryDeleteFile(file);
        }

        TryDeleteFile(installedUninstallerPath);
        TryDeleteEmptyDirectoriesUpward(receipt.InstallDirectory);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteRegistryKey(string registryKeyPath)
    {
        try
        {
            if (registryKeyPath.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase))
            {
                var subKeyPath = registryKeyPath.Substring(5);
                Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
                return;
            }

            if (registryKeyPath.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase))
            {
                var subKeyPath = registryKeyPath.Substring(5);
                Registry.LocalMachine.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
            }
        }
        catch
        {
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
        IProgress<InstallProgressInfo>? progress,
        int percentage,
        string message)
    {
        progress?.Report(new InstallProgressInfo
        {
            Percentage = Math.Clamp(percentage, 0, 100),
            Message = message
        });
    }
}