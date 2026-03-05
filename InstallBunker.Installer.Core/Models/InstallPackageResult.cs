namespace InstallBunker.Installer.Core.Models;

public sealed class InstallPackageResult
{
    public string PackageRootDirectory { get; set; } = string.Empty;

    public string InstallDirectory { get; set; } = string.Empty;

    public string MainExecutablePath { get; set; } = string.Empty;

    public string ReceiptFilePath { get; set; } = string.Empty;

    public string UninstallCommandPath { get; set; } = string.Empty;

    public string RegistryKeyPath { get; set; } = string.Empty;

    public int InstalledFilesCount { get; set; }

    public int CreatedShortcutsCount { get; set; }
}