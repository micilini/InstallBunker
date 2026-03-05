using InstallBunker.Compiler.Core.Models;
using InstallBunker.Compiler.Core.Paths;

namespace InstallBunker.Compiler.Core.Services;

public sealed class TemplateWorkspaceBuilder
{
    public TemplateWorkspaceResult Build(string builderBaseDirectory, string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(builderBaseDirectory))
        {
            throw new InvalidOperationException("Builder base directory is required.");
        }

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new InvalidOperationException("Workspace path is required.");
        }

        var compilerTemplatesWorkspaceRoot = Path.Combine(workspacePath, "CompilerTemplates");

        var result = new TemplateWorkspaceResult
        {
            WorkspaceRootDirectory = workspacePath,
            SetupTemplateSourceDirectory = CompilerPaths.GetSetupTemplateDirectory(builderBaseDirectory),
            UninstallTemplateSourceDirectory = CompilerPaths.GetUninstallTemplateDirectory(builderBaseDirectory),
            RuntimeSupportSourceDirectory = CompilerPaths.GetRuntimeSupportRootDirectory(builderBaseDirectory),
            SetupWorkspaceDirectory = Path.Combine(compilerTemplatesWorkspaceRoot, "SetupStub"),
            UninstallWorkspaceDirectory = Path.Combine(compilerTemplatesWorkspaceRoot, "UninstallStub"),
            RuntimeSupportWorkspaceDirectory = Path.Combine(compilerTemplatesWorkspaceRoot, "RuntimeSupport")
        };

        if (!Directory.Exists(result.SetupTemplateSourceDirectory))
        {
            result.ValidationErrors.Add(
                $"Setup template directory not found: {result.SetupTemplateSourceDirectory}");
        }

        if (!Directory.Exists(result.UninstallTemplateSourceDirectory))
        {
            result.ValidationErrors.Add(
                $"Uninstall template directory not found: {result.UninstallTemplateSourceDirectory}");
        }

        if (!Directory.Exists(result.RuntimeSupportSourceDirectory))
        {
            result.ValidationErrors.Add(
                $"RuntimeSupport directory not found: {result.RuntimeSupportSourceDirectory}");
        }

        if (result.ValidationErrors.Count > 0)
        {
            return result;
        }

        CopyDirectory(result.SetupTemplateSourceDirectory, result.SetupWorkspaceDirectory);
        CopyDirectory(result.UninstallTemplateSourceDirectory, result.UninstallWorkspaceDirectory);
        CopyDirectory(result.RuntimeSupportSourceDirectory, result.RuntimeSupportWorkspaceDirectory);

        result.Logs.Add($"Setup template copied to workspace: {result.SetupWorkspaceDirectory}");
        result.Logs.Add($"Uninstall template copied to workspace: {result.UninstallWorkspaceDirectory}");
        result.Logs.Add($"RuntimeSupport copied to workspace: {result.RuntimeSupportWorkspaceDirectory}");

        result.SetupProjectFilePath = ResolveSingleProjectFile(result.SetupWorkspaceDirectory, "Setup");
        result.UninstallProjectFilePath = ResolveSingleProjectFile(result.UninstallWorkspaceDirectory, "Uninstall");

        if (string.IsNullOrWhiteSpace(result.SetupProjectFilePath))
        {
            result.ValidationErrors.Add(
                $"Could not locate a single .csproj inside setup workspace: {result.SetupWorkspaceDirectory}");
        }

        if (string.IsNullOrWhiteSpace(result.UninstallProjectFilePath))
        {
            result.ValidationErrors.Add(
                $"Could not locate a single .csproj inside uninstall workspace: {result.UninstallWorkspaceDirectory}");
        }

        return result;
    }

    private static string ResolveSingleProjectFile(string directoryPath, string templateDisplayName)
    {
        var projectFiles = Directory
            .GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories)
            .ToList();

        if (projectFiles.Count == 1)
        {
            return projectFiles[0];
        }

        if (projectFiles.Count == 0)
        {
            return string.Empty;
        }

        throw new InvalidOperationException(
            $"Expected a single .csproj inside the {templateDisplayName} template workspace, but found {projectFiles.Count}.");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        var source = new DirectoryInfo(sourceDirectory);

        if (!source.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
        }

        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in source.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file.FullName);

            if (IsTransientBuildArtifact(relativePath))
            {
                continue;
            }

            var destinationFilePath = Path.Combine(destinationDirectory, relativePath);
            var destinationFolder = Path.GetDirectoryName(destinationFilePath);

            if (!string.IsNullOrWhiteSpace(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            file.CopyTo(destinationFilePath, overwrite: true);
        }
    }

    private static bool IsTransientBuildArtifact(string relativePath)
    {
        return relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".csproj.user", StringComparison.OrdinalIgnoreCase);
    }
}