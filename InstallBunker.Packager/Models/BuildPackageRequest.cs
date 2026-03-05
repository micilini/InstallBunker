using InstallBunker.Domain.Enums;
using InstallBunker.Domain.Models;

namespace InstallBunker.Packager.Models;

public sealed class BuildPackageRequest
{
    public string AppName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public string SourceDirectory { get; set; } = string.Empty;

    public string MainExecutableRelativePath { get; set; } = string.Empty;

    public string IconRelativePath { get; set; } = string.Empty;

    public string? LicenseFilePath { get; set; }

    public bool AllowPerUser { get; set; } = true;

    public bool AllowPerMachine { get; set; } = true;

    public InstallScope DefaultInstallScope { get; set; } = InstallScope.PerUser;

    public bool DesktopShortcut { get; set; } = true;

    public bool StartMenuShortcut { get; set; } = true;

    public PackageBrandingOptions Branding { get; set; } = new();

    public string OutputDirectory { get; set; } = string.Empty;

    public string PackagePassword { get; set; } = string.Empty;

    public string SetupExecutablePath { get; set; } = string.Empty;

    public string UninstallerExecutablePath { get; set; } = string.Empty;
}