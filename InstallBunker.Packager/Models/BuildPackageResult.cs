namespace InstallBunker.Packager.Models;

public sealed class BuildPackageResult
{
    public string OutputDirectory { get; set; } = string.Empty;

    public string SetupFilePath { get; set; } = string.Empty;

    public string PackageFilePath { get; set; } = string.Empty;

    public string UninstallerFilePath { get; set; } = string.Empty;

    public string ManifestFilePath { get; set; } = string.Empty;

    public string PayloadDirectory { get; set; } = string.Empty;

    public int FilesCopiedCount { get; set; }

    public bool LicenseCopied { get; set; }
}