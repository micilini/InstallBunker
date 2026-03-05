namespace InstallBunker.Uninstaller.Core.Models;

public sealed class UninstallProgressInfo
{
    public int Percentage { get; set; }

    public string Message { get; set; } = string.Empty;
}