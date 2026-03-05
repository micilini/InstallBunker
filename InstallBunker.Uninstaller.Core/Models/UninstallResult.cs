namespace InstallBunker.Uninstaller.Core.Models;

public sealed class UninstallResult
{
    public string ReceiptFilePath { get; set; } = string.Empty;

    public string InstallDirectory { get; set; } = string.Empty;

    public int RemovedFilesCount { get; set; }

    public int RemovedShortcutsCount { get; set; }

    public int RemovedRegistryKeysCount { get; set; }

    public int FailedFileRemovalsCount { get; set; }

    public int FailedShortcutRemovalsCount { get; set; }

    public int FailedRegistryRemovalsCount { get; set; }
}