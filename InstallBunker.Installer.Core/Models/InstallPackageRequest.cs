using InstallBunker.Domain.Enums;

namespace InstallBunker.Installer.Core.Models;

public sealed class InstallPackageRequest
{
    public string PackageRootDirectory { get; set; } = string.Empty;

    public InstallScope? InstallScopeOverride { get; set; }

    public string? OverrideInstallDirectory { get; set; }

    public bool? CreateDesktopShortcutOverride { get; set; }

    public bool? CreateStartMenuShortcutOverride { get; set; }
}