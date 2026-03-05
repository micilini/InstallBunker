namespace InstallBunker.Uninstaller.Core.Models;

public sealed class UninstallRequest
{
    public string ReceiptFilePath { get; set; } = string.Empty;

    public bool RemoveInstallDirectoryIfEmpty { get; set; } = true;
}