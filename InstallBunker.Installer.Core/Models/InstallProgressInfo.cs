namespace InstallBunker.Installer.Core.Models;

public sealed class InstallProgressInfo
{
    public int Percentage { get; set; }

    public string Message { get; set; } = string.Empty;
}