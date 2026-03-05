namespace InstallBunker.Compiler.Core.Models;

public sealed class CompilerBrandingOptions
{
    public string AppName { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string IconRelativePath { get; set; } = string.Empty;

    public string SetupWindowTitle { get; set; } = string.Empty;

    public string SetupSidebarAppName { get; set; } = string.Empty;

    public string SetupSidebarVersion { get; set; } = string.Empty;

    public string SetupWelcomeSummary { get; set; } = string.Empty;

    public string UninstallWindowTitle { get; set; } = string.Empty;

    public string UninstallSidebarAppName { get; set; } = string.Empty;

    public string UninstallSidebarVersion { get; set; } = string.Empty;

    public string UninstallWelcomeSummary { get; set; } = string.Empty;
}