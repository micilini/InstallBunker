namespace InstallBunker.Compiler.Core.Models;

public sealed class BootstrapperPublishResult
{
    public string SetupPublishDirectory { get; set; } = string.Empty;

    public string UninstallPublishDirectory { get; set; } = string.Empty;

    public string SetupExecutablePath { get; set; } = string.Empty;

    public string UninstallExecutablePath { get; set; } = string.Empty;

    public string ToolchainSourceKind { get; set; } = string.Empty;

    public string ToolchainDisplayName { get; set; } = string.Empty;

    public string ToolchainCommand { get; set; } = string.Empty;

    public string? ToolchainRootDirectory { get; set; }

    public string? SelectedSdkVersion { get; set; }

    public bool UsedSystemDotNetFallback { get; set; }

    public List<string> Logs { get; set; } = new();
}