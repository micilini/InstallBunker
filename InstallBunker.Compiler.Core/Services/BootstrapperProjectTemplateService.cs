using InstallBunker.Compiler.Core.Models;

namespace InstallBunker.Compiler.Core.Services;

public sealed class BootstrapperProjectTemplateService
{
    private readonly TemplateWorkspaceBuilder _templateWorkspaceBuilder = new();
    private readonly TemplateMutationService _templateMutationService = new();

    public BootstrapperProjectTemplateResult Resolve(
        string builderBaseDirectory,
        string workspacePath,
        CompilerRequest request)
    {
        if (string.IsNullOrWhiteSpace(builderBaseDirectory))
        {
            throw new InvalidOperationException("Builder base directory is required.");
        }

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new InvalidOperationException("Workspace path is required.");
        }

        if (request is null)
        {
            throw new InvalidOperationException("CompilerRequest is required.");
        }

        var workspaceResult = _templateWorkspaceBuilder.Build(builderBaseDirectory, workspacePath);

        var result = new BootstrapperProjectTemplateResult
        {
            SourceKind = "workspace-template",
            SourceRootDirectory = workspaceResult.WorkspaceRootDirectory,
            SetupWorkspaceDirectory = workspaceResult.SetupWorkspaceDirectory,
            UninstallWorkspaceDirectory = workspaceResult.UninstallWorkspaceDirectory,
            SetupProjectFilePath = workspaceResult.SetupProjectFilePath,
            UninstallProjectFilePath = workspaceResult.UninstallProjectFilePath
        };

        foreach (var log in workspaceResult.Logs)
        {
            result.Logs.Add(log);
        }

        foreach (var error in workspaceResult.ValidationErrors)
        {
            result.ValidationErrors.Add(error);
        }

        if (workspaceResult.ValidationErrors.Count > 0)
        {
            return result;
        }

        var setupMutation = _templateMutationService.Mutate(
            "Setup",
            workspaceResult.SetupProjectFilePath,
            request);

        var uninstallMutation = _templateMutationService.Mutate(
            "Uninstall",
            workspaceResult.UninstallProjectFilePath,
            request);

        foreach (var log in setupMutation.Logs)
        {
            result.Logs.Add(log);
        }

        foreach (var log in uninstallMutation.Logs)
        {
            result.Logs.Add(log);
        }

        foreach (var error in setupMutation.ValidationErrors)
        {
            result.ValidationErrors.Add(error);
        }

        foreach (var error in uninstallMutation.ValidationErrors)
        {
            result.ValidationErrors.Add(error);
        }

        result.SetupBrandingJsonPath = setupMutation.GeneratedBrandingJsonPath;
        result.UninstallBrandingJsonPath = uninstallMutation.GeneratedBrandingJsonPath;
        result.SetupGeneratedPropsPath = setupMutation.GeneratedPropsPath;
        result.UninstallGeneratedPropsPath = uninstallMutation.GeneratedPropsPath;
        result.SetupGeneratedBrandingCodePath = setupMutation.GeneratedBrandingCodePath;
        result.UninstallGeneratedBrandingCodePath = uninstallMutation.GeneratedBrandingCodePath;

        if (!result.IsValid && result.ValidationErrors.Count == 0)
        {
            result.ValidationErrors.Add(
                "The template workspace builder did not produce valid setup/uninstall project files.");
        }

        return result;
    }
}