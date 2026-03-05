using InstallBunker.Domain.Models;

namespace InstallBunker.Uninstaller.Core.Models;

public sealed class UninstallerSessionInfo
{
    public InstallReceipt Receipt { get; set; } = new();

    public string ReceiptFilePath { get; set; } = string.Empty;

    public string CurrentExecutablePath { get; set; } = string.Empty;

    public string WindowTitle { get; set; } = string.Empty;

    public string SidebarAppName { get; set; } = string.Empty;

    public string SidebarVersion { get; set; } = string.Empty;

    public string WelcomeSummary { get; set; } = string.Empty;
}