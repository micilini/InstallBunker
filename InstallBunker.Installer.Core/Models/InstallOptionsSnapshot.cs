using InstallBunker.Domain.Enums;

namespace InstallBunker.Installer.Core.Models;

public sealed class InstallOptionsSnapshot
{
    public InstallScope InstallScope { get; set; } = InstallScope.PerUser;

    public string InstallDirectory { get; set; } = string.Empty;

    public bool DesktopShortcut { get; set; }

    public bool StartMenuShortcut { get; set; }
}