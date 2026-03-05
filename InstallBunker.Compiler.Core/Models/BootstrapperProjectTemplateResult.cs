namespace InstallBunker.Compiler.Core.Models;

public sealed class BootstrapperProjectTemplateResult
{
    public string SourceKind { get; set; } = string.Empty;

    public string SourceRootDirectory { get; set; } = string.Empty;

    public string SetupProjectFilePath { get; set; } = string.Empty;

    public string UninstallProjectFilePath { get; set; } = string.Empty;

    public string SetupWorkspaceDirectory { get; set; } = string.Empty;

    public string UninstallWorkspaceDirectory { get; set; } = string.Empty;

    public string SetupBrandingJsonPath { get; set; } = string.Empty;

    public string UninstallBrandingJsonPath { get; set; } = string.Empty;

    public string SetupGeneratedPropsPath { get; set; } = string.Empty;

    public string UninstallGeneratedPropsPath { get; set; } = string.Empty;

    public string SetupGeneratedBrandingCodePath { get; set; } = string.Empty;

    public string UninstallGeneratedBrandingCodePath { get; set; } = string.Empty;

    public List<string> Logs { get; set; } = new();

    public List<string> ValidationErrors { get; set; } = new();

    public bool IsValid =>
        ValidationErrors.Count == 0 &&
        !string.IsNullOrWhiteSpace(SetupProjectFilePath) &&
        !string.IsNullOrWhiteSpace(UninstallProjectFilePath);
}