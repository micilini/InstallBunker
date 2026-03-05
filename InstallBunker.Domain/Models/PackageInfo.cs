namespace InstallBunker.Domain.Models;

public sealed class PackageInfo
{
    public string AppName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public string MainExecutable { get; set; } = string.Empty;

    public string IconPath { get; set; } = string.Empty;
}
