using InstallBunker.Domain.Enums;
using InstallBunker.Domain.Models;

namespace InstallBunker.Installer.Core.Models;

public sealed class InstallerSessionInfo
{
    public string PackageRootDirectory { get; set; } = string.Empty;

    public PackageManifest Manifest { get; set; } = new();

    public string WindowTitle { get; set; } = string.Empty;

    public string SidebarAppName { get; set; } = string.Empty;

    public string SidebarVersion { get; set; } = string.Empty;

    public string WelcomeSummary { get; set; } = string.Empty;

    public string LicenseText { get; set; } = string.Empty;

    public bool ShowLicensePage { get; set; }

    public bool AllowPerUser { get; set; }

    public bool AllowPerMachine { get; set; }

    public InstallScope DefaultInstallScope { get; set; } = InstallScope.PerUser;

    public bool DefaultDesktopShortcut { get; set; }

    public bool DefaultStartMenuShortcut { get; set; }

    public bool AllowLaunchAfterInstall { get; set; }
}