namespace InstallBunker.Compiler.Core.Models;

public sealed class CompilerResult
{
    public string OutputDirectory { get; set; } = string.Empty;

    public string SetupFilePath { get; set; } = string.Empty;

    public string PackageFilePath { get; set; } = string.Empty;

    public string UninstallFilePath { get; set; } = string.Empty;

    public string ManifestFilePath { get; set; } = string.Empty;

    public string PayloadDirectory { get; set; } = string.Empty;

    public string? TemporaryWorkspacePath { get; set; }

    public string? SetupPublishDirectory { get; set; }

    public string? UninstallPublishDirectory { get; set; }

    public string? BuildLogFilePath { get; set; }

    public int FilesCopiedCount { get; set; }

    public bool LicenseCopied { get; set; }

    public bool BootstrapperExecutablesGeneratedByToolchain { get; set; }

    public string ToolchainSourceKind { get; set; } = string.Empty;

    public string ToolchainDisplayName { get; set; } = string.Empty;

    public bool UsedSystemDotNetFallback { get; set; }

    public bool WorkspaceDeleted { get; set; }

    public string? WorkspaceRetentionReason { get; set; }

    public List<string> Logs { get; set; } = new();
}