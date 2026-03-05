namespace InstallBunker.Compiler.Core.Models;

public sealed class EmbeddedToolchainInfo
{
    public string SourceKind { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DotNetCommand { get; set; } = string.Empty;

    public string? RootDirectory { get; set; }

    public string? SdkDirectory { get; set; }

    public string? PacksDirectory { get; set; }

    public string? SharedDirectory { get; set; }

    public string? SelectedSdkVersion { get; set; }

    public string? SelectedSdkDirectory { get; set; }

    public string? SelectedSdkSdksDirectory { get; set; }

    public bool IsEmbedded { get; set; }

    public bool IsExplicit { get; set; }

    public bool IsSystemFallback { get; set; }
}