using InstallBunker.Compiler.Core.Models;
using InstallBunker.Compiler.Core.Paths;
using InstallBunker.Domain.Enums;
using InstallBunker.Packager.Models;
using InstallBunker.Packager.Services;
using System.Text;

namespace InstallBunker.Compiler.Core.Services;

public sealed class CompilerEngine
{
    private readonly BootstrapperProjectTemplateService _bootstrapperProjectTemplateService = new();
    private readonly DotNetPublishService _dotNetPublishService = new();
    private readonly PackageBuilder _packageBuilder = new();
    private readonly EmbeddedToolchainLayoutService _embeddedToolchainLayoutService = new();

    public async Task<CompilerResult> CompileAsync(
        CompilerRequest request,
        string builderBaseDirectory,
        IProgress<CompilerTelemetryEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var logs = new List<string>();
        var result = new CompilerResult();
        string? workspacePath = null;
        string currentStage = "Initializing";
        var buildSucceeded = false;

        try
        {
            currentStage = "Validating request";
            ReportStatus(progress, "validate-request", "Validating Request", "Validating compiler request...", 5);
            ValidateRequest(request);
            logs.Add("CompilerRequest validated successfully.");

            currentStage = "Validating internal environment";
            ReportStatus(progress, "validate-environment", "Validating Environment", "Checking templates and toolchain integrity...", 10);
            ValidateInternalEnvironment(builderBaseDirectory, request.Toolchain, logs, progress);

            workspacePath = CompilerPaths.CreateCompilationWorkspace();
            result.TemporaryWorkspacePath = workspacePath;

            logs.Add("Compiler V1 started.");
            logs.Add($"Workspace created: {workspacePath}");
            ReportLog(progress, "workspace", "Preparing Workspace", $"Workspace created: {workspacePath}");
            ReportStatus(progress, "workspace", "Preparing Workspace", "Preparing temporary workspace...", 15);

            currentStage = "Materializing internal templates";
            logs.Add("Materializing internal compiler templates into workspace...");
            ReportStatus(progress, "templates", "Copying Templates", "Copying internal compiler templates into workspace...", 20);

            var projectTemplates = _bootstrapperProjectTemplateService.Resolve(
                builderBaseDirectory,
                workspacePath,
                request);

            if (!projectTemplates.IsValid)
            {
                var diagnostics = projectTemplates.ValidationErrors.Count == 0
                    ? "No project template diagnostics available."
                    : string.Join(Environment.NewLine, projectTemplates.ValidationErrors);

                throw new InvalidOperationException(
                    "The compiler could not materialize valid internal bootstrapper templates." +
                    Environment.NewLine + Environment.NewLine +
                    diagnostics);
            }

            foreach (var log in projectTemplates.Logs)
            {
                logs.Add(log);
                ReportLog(progress, "templates", "Copying Templates", log);
            }

            logs.Add($"Bootstrapper template source kind: {projectTemplates.SourceKind}");
            logs.Add($"Setup workspace directory: {projectTemplates.SetupWorkspaceDirectory}");
            logs.Add($"Uninstall workspace directory: {projectTemplates.UninstallWorkspaceDirectory}");
            logs.Add($"Setup project template: {projectTemplates.SetupProjectFilePath}");
            logs.Add($"Uninstall project template: {projectTemplates.UninstallProjectFilePath}");
            logs.Add($"Setup branding json: {projectTemplates.SetupBrandingJsonPath}");
            logs.Add($"Uninstall branding json: {projectTemplates.UninstallBrandingJsonPath}");
            logs.Add($"Setup generated props: {projectTemplates.SetupGeneratedPropsPath}");
            logs.Add($"Uninstall generated props: {projectTemplates.UninstallGeneratedPropsPath}");
            logs.Add($"Setup generated branding code: {projectTemplates.SetupGeneratedBrandingCodePath}");
            logs.Add($"Uninstall generated branding code: {projectTemplates.UninstallGeneratedBrandingCodePath}");

            ReportStatus(progress, "branding", "Applying Branding", "Applying branding and generated props...", 28);

            currentStage = "Publishing bootstrappers";
            logs.Add("Generating single-file bootstrappers through dotnet publish...");
            ReportStatus(progress, "publish", "Publishing Bootstrappers", "Generating Setup.exe and Uninstall.exe through the internal toolchain...", 35);

            var publishResult = await _dotNetPublishService.PublishBootstrappersAsync(
                request,
                projectTemplates,
                builderBaseDirectory,
                workspacePath,
                progress,
                cancellationToken);

            foreach (var log in publishResult.Logs)
            {
                logs.Add(log);
            }

            logs.Add("Internal workspace templates published successfully.");
            logs.Add("The compiler is no longer publishing Installer.UI / Uninstaller.UI directly from the solution tree.");

            result.SetupPublishDirectory = publishResult.SetupPublishDirectory;
            result.UninstallPublishDirectory = publishResult.UninstallPublishDirectory;
            result.ToolchainSourceKind = publishResult.ToolchainSourceKind;
            result.ToolchainDisplayName = publishResult.ToolchainDisplayName;
            result.UsedSystemDotNetFallback = publishResult.UsedSystemDotNetFallback;

            ReportStatus(
                progress,
                "publish-finished",
                "Publishing Bootstrappers",
                $"Bootstrappers generated using toolchain: {publishResult.ToolchainDisplayName} ({publishResult.ToolchainSourceKind}).",
                80);

            currentStage = "Packaging final output";
            ReportStatus(progress, "package", "Packing Output", "Building manifest and payload files...", 84);

            var buildRequest = new BuildPackageRequest
            {
                AppName = request.AppName,
                Version = request.Version,
                Publisher = request.Publisher,
                SourceDirectory = request.SourceDirectory,
                MainExecutableRelativePath = request.MainExecutableRelativePath,
                IconRelativePath = request.IconRelativePath,
                LicenseFilePath = request.LicenseFilePath,
                AllowPerUser = request.AllowPerUser,
                AllowPerMachine = request.AllowPerMachine,
                DefaultInstallScope = request.DefaultInstallScope,
                DesktopShortcut = request.DesktopShortcut,
                StartMenuShortcut = request.StartMenuShortcut,
                Branding = new InstallBunker.Domain.Models.PackageBrandingOptions
                {
                    AppName = request.Branding.AppName,
                    Publisher = request.Branding.Publisher,
                    Version = request.Branding.Version,
                    IconRelativePath = request.Branding.IconRelativePath,
                    SetupWindowTitle = request.Branding.SetupWindowTitle,
                    SetupSidebarAppName = request.Branding.SetupSidebarAppName,
                    SetupSidebarVersion = request.Branding.SetupSidebarVersion,
                    SetupWelcomeSummary = request.Branding.SetupWelcomeSummary,
                    UninstallWindowTitle = request.Branding.UninstallWindowTitle,
                    UninstallSidebarAppName = request.Branding.UninstallSidebarAppName,
                    UninstallSidebarVersion = request.Branding.UninstallSidebarVersion,
                    UninstallWelcomeSummary = request.Branding.UninstallWelcomeSummary
                },
                OutputDirectory = request.OutputDirectory,
                PackagePassword = request.PackagePassword,
                SetupExecutablePath = publishResult.SetupExecutablePath,
                UninstallerExecutablePath = publishResult.UninstallExecutablePath
            };

            logs.Add("Calling PackageBuilder...");
            ReportLog(progress, "package", "Packing Output", "Calling PackageBuilder...");

            var buildResult = await Task.Run(() => _packageBuilder.Build(buildRequest), cancellationToken);

            logs.Add("PackageBuilder finished successfully.");
            logs.Add($"Output directory: {buildResult.OutputDirectory}");
            logs.Add($"Setup file: {buildResult.SetupFilePath}");
            logs.Add($"Uninstall file: {buildResult.UninstallerFilePath}");
            logs.Add($"Manifest file: {buildResult.ManifestFilePath}");
            logs.Add($"Payload directory: {buildResult.PayloadDirectory}");
            logs.Add($"Files copied: {buildResult.FilesCopiedCount}");

            result.OutputDirectory = buildResult.OutputDirectory;
            result.SetupFilePath = buildResult.SetupFilePath;
            result.PackageFilePath = buildResult.PackageFilePath;
            result.UninstallFilePath = buildResult.UninstallerFilePath;
            result.ManifestFilePath = buildResult.ManifestFilePath;
            result.PayloadDirectory = buildResult.PayloadDirectory;
            result.FilesCopiedCount = buildResult.FilesCopiedCount;
            result.LicenseCopied = buildResult.LicenseCopied;
            result.BootstrapperExecutablesGeneratedByToolchain = true;

            currentStage = "Writing build report";
            ReportStatus(progress, "report", "Writing Build Report", "Writing build diagnostics report...", 91);

            if (request.GenerateDiagnosticsFile)
            {
                result.BuildLogFilePath = TryWriteBuildLogFile(buildResult.OutputDirectory, logs);

                if (!string.IsNullOrWhiteSpace(result.BuildLogFilePath))
                {
                    logs.Add($"Build log exported: {result.BuildLogFilePath}");
                    ReportLog(progress, "report", "Writing Build Report", $"Build log exported: {result.BuildLogFilePath}");
                }
            }
            else
            {
                result.BuildLogFilePath = null;
                logs.Add("Build diagnostics file generation is disabled for this project.");
                ReportLog(progress, "report", "Writing Build Report", "Build diagnostics file generation is disabled for this project.");
            }

            buildSucceeded = true;
            logs.Add("Compiler pipeline finished successfully.");
            ReportStatus(progress, "complete", "Build Completed", "Package generated successfully.", 100);

            return result;
        }
        catch (OperationCanceledException)
        {
            logs.Add("Build cancelled.");
            ReportWarning(progress, "cancelled", "Build Cancelled", "Build cancelled by request.", 0);
            throw;
        }
        catch (Exception ex)
        {
            logs.Add($"Build failed during stage: {currentStage}");
            logs.Add(ex.Message);

            ReportError(progress, "failed", "Build Failed", $"Build failed during stage: {currentStage}.", 0);
            ReportLog(progress, "failed", "Build Failed", ex.Message);

            var diagnosticMessage = new StringBuilder();
            diagnosticMessage.AppendLine($"The compiler failed during stage: {currentStage}");
            diagnosticMessage.AppendLine();
            diagnosticMessage.AppendLine(ex.Message);

            if (!string.IsNullOrWhiteSpace(workspacePath))
            {
                diagnosticMessage.AppendLine();
                diagnosticMessage.AppendLine($"Temporary workspace: {workspacePath}");
            }

            throw new InvalidOperationException(diagnosticMessage.ToString().Trim(), ex);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(workspacePath) && Directory.Exists(workspacePath))
            {
                var keepWorkspace = buildSucceeded
                    ? request.Toolchain.KeepWorkspaceOnSuccess
                    : request.Toolchain.KeepWorkspaceOnFailure;

                if (keepWorkspace)
                {
                    result.WorkspaceDeleted = false;
                    result.WorkspaceRetentionReason = buildSucceeded
                        ? "Configured to keep workspace on success."
                        : "Configured to keep workspace on failure.";

                    logs.Add($"Workspace retained: {workspacePath}");
                    ReportWarning(
                        progress,
                        "cleanup",
                        "Cleaning Workspace",
                        $"Workspace retained: {workspacePath}",
                        buildSucceeded ? 96 : 0);
                }
                else
                {
                    var deleted = TryDeleteDirectory(workspacePath);

                    result.WorkspaceDeleted = deleted;
                    result.WorkspaceRetentionReason = deleted
                        ? null
                        : "Automatic workspace cleanup failed.";

                    if (deleted)
                    {
                        logs.Add($"Workspace deleted successfully: {workspacePath}");
                        ReportLog(progress, "cleanup", "Cleaning Workspace", $"Workspace deleted successfully: {workspacePath}");
                    }
                    else
                    {
                        logs.Add($"Workspace cleanup failed and the directory was kept: {workspacePath}");
                        ReportWarning(
                            progress,
                            "cleanup",
                            "Cleaning Workspace",
                            $"Workspace cleanup failed and the directory was kept: {workspacePath}",
                            buildSucceeded ? 96 : 0);
                    }
                }
            }

            result.Logs = logs;
        }
    }

    public void ValidateContract(CompilerRequest request)
    {
        ValidateRequest(request);
    }

    private void ValidateInternalEnvironment(
        string builderBaseDirectory,
        CompilerToolchainOptions toolchain,
        List<string> logs,
        IProgress<CompilerTelemetryEvent>? progress)
    {
        if (string.IsNullOrWhiteSpace(builderBaseDirectory))
        {
            throw new InvalidOperationException("Builder base directory is required.");
        }

        var templatesRoot = CompilerPaths.GetBuilderTemplatesRootDirectory(builderBaseDirectory);
        var setupStubDirectory = CompilerPaths.GetSetupTemplateDirectory(builderBaseDirectory);
        var uninstallStubDirectory = CompilerPaths.GetUninstallTemplateDirectory(builderBaseDirectory);
        var runtimeSupportRoot = CompilerPaths.GetRuntimeSupportRootDirectory(builderBaseDirectory);

        var requiredTemplateFiles = new[]
        {
            Path.Combine(setupStubDirectory, "InstallBunker.Installer.UI.csproj"),
            Path.Combine(setupStubDirectory, "App.xaml"),
            Path.Combine(setupStubDirectory, "App.xaml.cs"),
            Path.Combine(setupStubDirectory, "MainWindow.xaml"),
            Path.Combine(setupStubDirectory, "MainWindow.xaml.cs"),
            Path.Combine(setupStubDirectory, "AssemblyInfo.cs"),

            Path.Combine(uninstallStubDirectory, "InstallBunker.Uninstaller.UI.csproj"),
            Path.Combine(uninstallStubDirectory, "App.xaml"),
            Path.Combine(uninstallStubDirectory, "App.xaml.cs"),
            Path.Combine(uninstallStubDirectory, "MainWindow.xaml"),
            Path.Combine(uninstallStubDirectory, "MainWindow.xaml.cs"),
            Path.Combine(uninstallStubDirectory, "AssemblyInfo.cs"),

            Path.Combine(runtimeSupportRoot, "InstallBunker.Common", "InstallBunker.Common.csproj"),
            Path.Combine(runtimeSupportRoot, "InstallBunker.Domain", "InstallBunker.Domain.csproj"),
            Path.Combine(runtimeSupportRoot, "InstallBunker.Installer.Core", "InstallBunker.Installer.Core.csproj"),
            Path.Combine(runtimeSupportRoot, "InstallBunker.Uninstaller.Core", "InstallBunker.Uninstaller.Core.csproj")
        };

        var templateDiagnostics = new List<string>();

        if (!Directory.Exists(templatesRoot))
        {
            templateDiagnostics.Add($"Templates root directory not found: {templatesRoot}");
        }

        if (!Directory.Exists(setupStubDirectory))
        {
            templateDiagnostics.Add($"Setup template directory not found: {setupStubDirectory}");
        }

        if (!Directory.Exists(uninstallStubDirectory))
        {
            templateDiagnostics.Add($"Uninstall template directory not found: {uninstallStubDirectory}");
        }

        if (!Directory.Exists(runtimeSupportRoot))
        {
            templateDiagnostics.Add($"RuntimeSupport directory not found: {runtimeSupportRoot}");
        }

        foreach (var requiredFilePath in requiredTemplateFiles)
        {
            if (!File.Exists(requiredFilePath))
            {
                templateDiagnostics.Add($"Required template file not found: {requiredFilePath}");
            }
        }

        if (templateDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                "The internal compiler templates are missing or invalid." +
                Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, templateDiagnostics));
        }

        logs.Add("Internal compiler templates validated successfully.");
        ReportLog(progress, "validate-environment", "Validating Environment", "Internal compiler templates validated successfully.");

        if (!toolchain.PreferEmbeddedToolchain)
        {
            return;
        }

        var embeddedToolchain = _embeddedToolchainLayoutService.Inspect(builderBaseDirectory);

        if (embeddedToolchain.IsStructured)
        {
            logs.Add("Embedded toolchain layout validated successfully.");
            ReportLog(progress, "validate-environment", "Validating Environment", "Embedded toolchain layout validated successfully.");
            return;
        }

        var diagnostics = embeddedToolchain.MissingEntries.Count == 0
            ? "No embedded toolchain diagnostics available."
            : string.Join(Environment.NewLine, embeddedToolchain.MissingEntries);

        if (toolchain.RequireEmbeddedToolchainInProductMode && !toolchain.IsDevelopmentMode)
        {
            throw new InvalidOperationException(
                "Product mode requires a valid embedded toolchain, but the bundled layout is missing or incomplete." +
                Environment.NewLine + Environment.NewLine +
                diagnostics);
        }

        logs.Add("Embedded toolchain is incomplete, but development mode allows continuing.");
        logs.Add(diagnostics);
        ReportWarning(
            progress,
            "validate-environment",
            "Validating Environment",
            "Embedded toolchain is incomplete, but development mode allows continuing.",
            10);
    }

    private static bool TryDeleteDirectory(string directoryPath)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    return true;
                }

                Directory.Delete(directoryPath, recursive: true);

                if (!Directory.Exists(directoryPath))
                {
                    return true;
                }
            }
            catch
            {
                if (attempt == 3)
                {
                    return false;
                }

                Thread.Sleep(150);
            }
        }

        return !Directory.Exists(directoryPath);
    }

    private static string? TryWriteBuildLogFile(string outputDirectory, List<string> logs)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                return null;
            }

            var path = Path.Combine(outputDirectory, "compiler.build.log.txt");
            File.WriteAllLines(path, logs, Encoding.UTF8);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static void ReportStatus(
        IProgress<CompilerTelemetryEvent>? progress,
        string stageKey,
        string stageDisplayName,
        string message,
        int progressPercent)
    {
        progress?.Report(CompilerTelemetryEvent.Status(stageKey, stageDisplayName, message, progressPercent));
    }

    private static void ReportLog(
        IProgress<CompilerTelemetryEvent>? progress,
        string stageKey,
        string stageDisplayName,
        string message)
    {
        progress?.Report(CompilerTelemetryEvent.Log(stageKey, stageDisplayName, message));
    }

    private static void ReportWarning(
        IProgress<CompilerTelemetryEvent>? progress,
        string stageKey,
        string stageDisplayName,
        string message,
        int progressPercent)
    {
        progress?.Report(CompilerTelemetryEvent.Warning(stageKey, stageDisplayName, message, progressPercent));
    }

    private static void ReportError(
        IProgress<CompilerTelemetryEvent>? progress,
        string stageKey,
        string stageDisplayName,
        string message,
        int progressPercent)
    {
        progress?.Report(CompilerTelemetryEvent.Error(stageKey, stageDisplayName, message, progressPercent));
    }

    private static void ValidateRequest(CompilerRequest request)
    {
        if (request is null)
        {
            throw new InvalidOperationException("CompilerRequest is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AppName))
        {
            throw new InvalidOperationException("AppName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            throw new InvalidOperationException("Version is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Publisher))
        {
            throw new InvalidOperationException("Publisher is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceDirectory))
        {
            throw new InvalidOperationException("SourceDirectory is required.");
        }

        if (!Directory.Exists(request.SourceDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Source directory not found: {request.SourceDirectory}");
        }

        if (string.IsNullOrWhiteSpace(request.MainExecutableRelativePath))
        {
            throw new InvalidOperationException("MainExecutableRelativePath is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IconRelativePath))
        {
            throw new InvalidOperationException("IconRelativePath is required.");
        }

        if (!request.AllowPerUser && !request.AllowPerMachine)
        {
            throw new InvalidOperationException("At least one install scope must be allowed.");
        }

        if (request.DefaultInstallScope == InstallScope.PerMachine && !request.AllowPerMachine)
        {
            throw new InvalidOperationException("Default scope cannot be PerMachine when PerMachine is not allowed.");
        }

        if (request.DefaultInstallScope == InstallScope.PerUser && !request.AllowPerUser)
        {
            throw new InvalidOperationException("Default scope cannot be PerUser when PerUser is not allowed.");
        }

        if (request.Branding is null)
        {
            throw new InvalidOperationException("Branding is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.SetupWindowTitle))
        {
            throw new InvalidOperationException("Branding.SetupWindowTitle is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.SetupSidebarAppName))
        {
            throw new InvalidOperationException("Branding.SetupSidebarAppName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.SetupSidebarVersion))
        {
            throw new InvalidOperationException("Branding.SetupSidebarVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.SetupWelcomeSummary))
        {
            throw new InvalidOperationException("Branding.SetupWelcomeSummary is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.UninstallWindowTitle))
        {
            throw new InvalidOperationException("Branding.UninstallWindowTitle is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.UninstallSidebarAppName))
        {
            throw new InvalidOperationException("Branding.UninstallSidebarAppName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.UninstallSidebarVersion))
        {
            throw new InvalidOperationException("Branding.UninstallSidebarVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.UninstallWelcomeSummary))
        {
            throw new InvalidOperationException("Branding.UninstallWelcomeSummary is required.");
        }

        if (request.Toolchain is null)
        {
            throw new InvalidOperationException("Toolchain is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Toolchain.Configuration))
        {
            throw new InvalidOperationException("Toolchain.Configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Toolchain.RuntimeIdentifier))
        {
            throw new InvalidOperationException("Toolchain.RuntimeIdentifier is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            throw new InvalidOperationException("OutputDirectory is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PackagePassword))
        {
            throw new InvalidOperationException("PackagePassword is required.");
        }
    }
}