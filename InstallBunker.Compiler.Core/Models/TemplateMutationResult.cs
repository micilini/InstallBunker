namespace InstallBunker.Compiler.Core.Models;

public sealed class TemplateMutationResult
{
    public string TemplateDisplayName { get; set; } = string.Empty;

    public string ProjectFilePath { get; set; } = string.Empty;

    public string ProjectDirectory { get; set; } = string.Empty;

    public string GeneratedBrandingJsonPath { get; set; } = string.Empty;

    public string GeneratedBrandingCodePath { get; set; } = string.Empty;

    public string GeneratedPropsPath { get; set; } = string.Empty;

    public string GeneratedIconPath { get; set; } = string.Empty;

    public List<string> Logs { get; set; } = new();

    public List<string> ValidationErrors { get; set; } = new();

    public bool IsValid =>
        ValidationErrors.Count == 0 &&
        !string.IsNullOrWhiteSpace(ProjectFilePath) &&
        !string.IsNullOrWhiteSpace(ProjectDirectory);
}