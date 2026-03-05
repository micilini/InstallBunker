namespace InstallBunker.Compiler.Core.Models;

public sealed class EmbeddedToolchainLayoutInfo
{
    public string BuilderBaseDirectory { get; set; } = string.Empty;

    public string RootDirectory { get; set; } = string.Empty;

    public string DotNetDirectory { get; set; } = string.Empty;

    public string DotNetHostPath { get; set; } = string.Empty;

    public string SdkDirectory { get; set; } = string.Empty;

    public string PacksDirectory { get; set; } = string.Empty;

    public string SharedDirectory { get; set; } = string.Empty;

    public string HostDirectory { get; set; } = string.Empty;

    public string SdkManifestsDirectory { get; set; } = string.Empty;

    public string TemplatesDirectory { get; set; } = string.Empty;

    public string LicenseFilePath { get; set; } = string.Empty;

    public string ThirdPartyNoticesFilePath { get; set; } = string.Empty;

    public string SelectedSdkVersion { get; set; } = string.Empty;

    public string SelectedSdkDirectory { get; set; } = string.Empty;

    public string SelectedSdkSdksDirectory { get; set; } = string.Empty;

    public bool RootDirectoryExists { get; set; }

    public bool DotNetDirectoryExists { get; set; }

    public bool DotNetHostExists { get; set; }

    public bool SdkDirectoryExists { get; set; }

    public bool PacksDirectoryExists { get; set; }

    public bool SharedDirectoryExists { get; set; }

    public bool HostDirectoryExists { get; set; }

    public bool SdkManifestsDirectoryExists { get; set; }

    public bool TemplatesDirectoryExists { get; set; }

    public bool LicenseFileExists { get; set; }

    public bool ThirdPartyNoticesFileExists { get; set; }

    public bool SelectedSdkDirectoryExists { get; set; }

    public bool SelectedSdkSdksDirectoryExists { get; set; }

    public bool IsStructured =>
        RootDirectoryExists &&
        DotNetDirectoryExists &&
        DotNetHostExists &&
        SdkDirectoryExists &&
        PacksDirectoryExists &&
        SharedDirectoryExists &&
        HostDirectoryExists &&
        SdkManifestsDirectoryExists &&
        TemplatesDirectoryExists &&
        SelectedSdkDirectoryExists &&
        SelectedSdkSdksDirectoryExists;

    public List<string> MissingEntries { get; set; } = new();
}