namespace InstallBunker.Compiler.Core.Models;

public sealed class TemplateWorkspaceResult
{
    public string WorkspaceRootDirectory { get; set; } = string.Empty;

    public string SetupTemplateSourceDirectory { get; set; } = string.Empty;

    public string UninstallTemplateSourceDirectory { get; set; } = string.Empty;

    public string RuntimeSupportSourceDirectory { get; set; } = string.Empty;

    public string SetupWorkspaceDirectory { get; set; } = string.Empty;

    public string UninstallWorkspaceDirectory { get; set; } = string.Empty;

    public string RuntimeSupportWorkspaceDirectory { get; set; } = string.Empty;

    public string SetupProjectFilePath { get; set; } = string.Empty;

    public string UninstallProjectFilePath { get; set; } = string.Empty;

    public List<string> Logs { get; set; } = new();

    public List<string> ValidationErrors { get; set; } = new();

    public bool IsValid =>
        ValidationErrors.Count == 0 &&
        !string.IsNullOrWhiteSpace(SetupWorkspaceDirectory) &&
        !string.IsNullOrWhiteSpace(UninstallWorkspaceDirectory) &&
        !string.IsNullOrWhiteSpace(RuntimeSupportWorkspaceDirectory) &&
        !string.IsNullOrWhiteSpace(SetupProjectFilePath) &&
        !string.IsNullOrWhiteSpace(UninstallProjectFilePath);
}