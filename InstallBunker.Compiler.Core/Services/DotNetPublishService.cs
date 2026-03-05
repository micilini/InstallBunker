using InstallBunker.Compiler.Core.Models;
using System.Diagnostics;
using System.Text;

namespace InstallBunker.Compiler.Core.Services;

public sealed class DotNetPublishService
{
    private readonly CompilerToolchainResolver _toolchainResolver = new();
    private readonly EmbeddedToolchainLayoutService _embeddedToolchainLayoutService = new();

    public async Task<BootstrapperPublishResult> PublishBootstrappersAsync(
    CompilerRequest request,
    BootstrapperProjectTemplateResult templates,
    string builderBaseDirectory,
    string workspacePath,
    IProgress<CompilerTelemetryEvent>? progress = null,
    CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new InvalidOperationException("CompilerRequest is required.");
        }

        if (templates is null)
        {
            throw new InvalidOperationException("Bootstrapper project templates are required.");
        }

        if (!templates.IsValid)
        {
            var diagnostics = templates.ValidationErrors.Count == 0
                ? "No bootstrapper template diagnostics available."
                : string.Join(Environment.NewLine, templates.ValidationErrors);

            throw new InvalidOperationException(
                "The compiler could not resolve valid bootstrapper templates." +
                Environment.NewLine + Environment.NewLine +
                diagnostics);
        }

        if (string.IsNullOrWhiteSpace(builderBaseDirectory))
        {
            throw new InvalidOperationException("Builder base directory is required.");
        }

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new InvalidOperationException("Workspace path is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var logs = new List<string>();
        var iconFullPath = Path.GetFullPath(Path.Combine(request.SourceDirectory, request.IconRelativePath));

        if (!File.Exists(iconFullPath))
        {
            throw new FileNotFoundException("Icon file not found for bootstrapper publish.", iconFullPath);
        }

        ReportStatus(progress, "toolchain", "Resolving Toolchain", "Resolving .NET toolchain...", 35);
        logs.Add("Resolving .NET toolchain...");

        var toolchainInfo = _toolchainResolver.Resolve(
            request.Toolchain,
            builderBaseDirectory);

        logs.Add($"Resolved toolchain source: {toolchainInfo.SourceKind}");
        logs.Add($"Resolved toolchain display name: {toolchainInfo.DisplayName}");
        logs.Add($"Resolved dotnet command: {toolchainInfo.DotNetCommand}");

        if (!string.IsNullOrWhiteSpace(toolchainInfo.RootDirectory))
        {
            logs.Add($"Resolved toolchain root: {toolchainInfo.RootDirectory}");
        }

        if (!string.IsNullOrWhiteSpace(toolchainInfo.SelectedSdkVersion))
        {
            logs.Add($"Resolved embedded SDK version: {toolchainInfo.SelectedSdkVersion}");
        }

        if (!string.IsNullOrWhiteSpace(toolchainInfo.SelectedSdkSdksDirectory))
        {
            logs.Add($"Resolved MSBuild SDKs path: {toolchainInfo.SelectedSdkSdksDirectory}");
        }

        ReportStatus(
            progress,
            "toolchain",
            "Resolving Toolchain",
            $"Toolchain selected: {toolchainInfo.DisplayName} ({toolchainInfo.SourceKind})",
            40);

        var processEnvironment = BuildProcessEnvironment(
            toolchainInfo,
            builderBaseDirectory,
            workspacePath);

        var buildServerShutdownRequested = false;

        logs.Add("Controlled process environment prepared.");

        foreach (var pair in processEnvironment.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            logs.Add($"ENV {pair.Key} = {pair.Value}");
        }

        string dotnetVersion;

        try
        {
            dotnetVersion = await ExecuteProcessAsync(
                fileName: toolchainInfo.DotNetCommand,
                arguments: "--version",
                workingDirectory: workspacePath,
                environmentVariables: processEnvironment,
                onOutputLine: line =>
                {
                    logs.Add($"[dotnet-version] {line}");
                    ReportLog(progress, "toolchain", "Resolving Toolchain", $"[dotnet-version] {line}");
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "The compiler resolved a .NET toolchain, but failed to start it." +
                Environment.NewLine + Environment.NewLine +
                $"Toolchain source: {toolchainInfo.SourceKind}" + Environment.NewLine +
                $"Toolchain command: {toolchainInfo.DotNetCommand}" + Environment.NewLine +
                $"Toolchain root: {toolchainInfo.RootDirectory}" + Environment.NewLine +
                $"SDK version: {toolchainInfo.SelectedSdkVersion}" + Environment.NewLine +
                $"MSBuildSDKsPath: {toolchainInfo.SelectedSdkSdksDirectory}" + Environment.NewLine +
                $"Reason: {ex.Message}",
                ex);
        }

        logs.Add(".NET toolchain detected successfully.");
        logs.Add($"dotnet version output: {dotnetVersion.Trim()}");

        var toolchainRoot = Path.Combine(workspacePath, "toolchain");
        var setupPublishDirectory = Path.Combine(toolchainRoot, "setup-publish");
        var uninstallPublishDirectory = Path.Combine(toolchainRoot, "uninstall-publish");

        Directory.CreateDirectory(setupPublishDirectory);
        Directory.CreateDirectory(uninstallPublishDirectory);

        try
        {
            var setupArguments = BuildPublishArguments(
                projectFilePath: templates.SetupProjectFilePath,
                outputDirectory: setupPublishDirectory,
                runtimeIdentifier: request.Toolchain.RuntimeIdentifier,
                configuration: request.Toolchain.Configuration,
                iconFilePath: iconFullPath,
                publishSingleFile: request.Toolchain.PublishSingleFile,
                selfContained: request.Toolchain.SelfContained,
                includeNativeLibrariesForSelfExtract: request.Toolchain.IncludeNativeLibrariesForSelfExtract);

            ReportStatus(progress, "publish-setup", "Publishing Setup", "Publishing Setup.exe through dotnet publish...", 52);
            logs.Add("Publishing Setup.exe through dotnet publish...");

            await ExecuteProcessAsync(
                fileName: toolchainInfo.DotNetCommand,
                arguments: setupArguments,
                workingDirectory: Path.GetDirectoryName(templates.SetupProjectFilePath) ?? workspacePath,
                environmentVariables: processEnvironment,
                onOutputLine: line =>
                {
                    logs.Add($"[setup] {line}");
                    ReportLog(progress, "publish-setup", "Publishing Setup", $"[setup] {line}");
                },
                cancellationToken: cancellationToken);

            var uninstallArguments = BuildPublishArguments(
                projectFilePath: templates.UninstallProjectFilePath,
                outputDirectory: uninstallPublishDirectory,
                runtimeIdentifier: request.Toolchain.RuntimeIdentifier,
                configuration: request.Toolchain.Configuration,
                iconFilePath: iconFullPath,
                publishSingleFile: request.Toolchain.PublishSingleFile,
                selfContained: request.Toolchain.SelfContained,
                includeNativeLibrariesForSelfExtract: request.Toolchain.IncludeNativeLibrariesForSelfExtract);

            ReportStatus(progress, "publish-uninstall", "Publishing Uninstall", "Publishing Uninstall.exe through dotnet publish...", 68);
            logs.Add("Publishing Uninstall.exe through dotnet publish...");

            await ExecuteProcessAsync(
                fileName: toolchainInfo.DotNetCommand,
                arguments: uninstallArguments,
                workingDirectory: Path.GetDirectoryName(templates.UninstallProjectFilePath) ?? workspacePath,
                environmentVariables: processEnvironment,
                onOutputLine: line =>
                {
                    logs.Add($"[uninstall] {line}");
                    ReportLog(progress, "publish-uninstall", "Publishing Uninstall", $"[uninstall] {line}");
                },
                cancellationToken: cancellationToken);

            var setupExecutablePath = ResolveSinglePublishedExecutable(
                setupPublishDirectory,
                "InstallBunker.Installer.UI.exe");

            var uninstallExecutablePath = ResolveSinglePublishedExecutable(
                uninstallPublishDirectory,
                "InstallBunker.Uninstaller.UI.exe");

            logs.Add($"Generated Setup.exe: {setupExecutablePath}");
            logs.Add($"Generated Uninstall.exe: {uninstallExecutablePath}");

            ReportStatus(progress, "publish-complete", "Publishing Bootstrappers", "Setup.exe and Uninstall.exe published successfully.", 78);

            await TryShutdownBuildServersAsync(
                toolchainInfo.DotNetCommand,
                workspacePath,
                processEnvironment,
                logs,
                progress,
                cancellationToken);

            buildServerShutdownRequested = true;

            return new BootstrapperPublishResult
            {
                SetupPublishDirectory = setupPublishDirectory,
                UninstallPublishDirectory = uninstallPublishDirectory,
                SetupExecutablePath = setupExecutablePath,
                UninstallExecutablePath = uninstallExecutablePath,
                ToolchainSourceKind = toolchainInfo.SourceKind,
                ToolchainDisplayName = toolchainInfo.DisplayName,
                ToolchainCommand = toolchainInfo.DotNetCommand,
                ToolchainRootDirectory = toolchainInfo.RootDirectory,
                SelectedSdkVersion = toolchainInfo.SelectedSdkVersion,
                UsedSystemDotNetFallback = toolchainInfo.IsSystemFallback,
                Logs = logs
            };
        }
        catch
        {
            if (!buildServerShutdownRequested)
            {
                await TryShutdownBuildServersAsync(
                    toolchainInfo.DotNetCommand,
                    workspacePath,
                    processEnvironment,
                    logs,
                    progress,
                    cancellationToken);

                buildServerShutdownRequested = true;
            }

            throw;
        }
    }

    private async Task TryShutdownBuildServersAsync(
    string dotnetCommand,
    string workingDirectory,
    IReadOnlyDictionary<string, string> environmentVariables,
    List<string> logs,
    IProgress<CompilerTelemetryEvent>? progress,
    CancellationToken cancellationToken)
    {
        try
        {
            logs.Add("Requesting dotnet build-server shutdown...");
            ReportLog(progress, "cleanup", "Cleaning Toolchain", "Requesting dotnet build-server shutdown...");

            await ExecuteProcessAsync(
                fileName: dotnetCommand,
                arguments: "build-server shutdown",
                workingDirectory: workingDirectory,
                environmentVariables: environmentVariables,
                onOutputLine: line =>
                {
                    logs.Add($"[build-server-shutdown] {line}");
                    ReportLog(progress, "cleanup", "Cleaning Toolchain", $"[build-server-shutdown] {line}");
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logs.Add($"Build-server shutdown failed or was not necessary: {ex.Message}");
            ReportLog(progress, "cleanup", "Cleaning Toolchain", $"Build-server shutdown failed or was not necessary: {ex.Message}");
        }
    }

    private static string BuildPublishArguments(
        string projectFilePath,
        string outputDirectory,
        string runtimeIdentifier,
        string configuration,
        string iconFilePath,
        bool publishSingleFile,
        bool selfContained,
        bool includeNativeLibrariesForSelfExtract)
    {
        return
            $"publish {Quote(projectFilePath)} " +
            $"-c {Quote(configuration)} " +
            $"-r {Quote(runtimeIdentifier)} " +
            $"--self-contained {selfContained.ToString().ToLowerInvariant()} " +
            $"-o {Quote(outputDirectory)} " +
            $"-p:PublishSingleFile={publishSingleFile.ToString().ToLowerInvariant()} " +
            $"-p:ApplicationIcon={Quote(iconFilePath)} " +
            $"-p:DebugType=None " +
            $"-p:DebugSymbols=false " +
            $"-p:IncludeNativeLibrariesForSelfExtract={includeNativeLibrariesForSelfExtract.ToString().ToLowerInvariant()} " +
            $"-p:PublishTrimmed=false " +
            $"-p:CompilerMutationEnabled=true " +
            $"-p:UseSharedCompilation=false " +
            $"-nodeReuse:false " +
            "-nologo";
    }

    private IReadOnlyDictionary<string, string> BuildProcessEnvironment(
        EmbeddedToolchainInfo toolchainInfo,
        string builderBaseDirectory,
        string workspacePath)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var cliHome = Path.Combine(workspacePath, ".dotnet-cli-home");
        var nugetPackages = Path.Combine(workspacePath, ".nuget", "packages");

        Directory.CreateDirectory(cliHome);
        Directory.CreateDirectory(nugetPackages);

        environment["DOTNET_NOLOGO"] = "1";
        environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        environment["DOTNET_CLI_HOME"] = cliHome;
        environment["NUGET_PACKAGES"] = nugetPackages;
        environment["MSBUILDDISABLENODEREUSE"] = "1";
        environment["UseSharedCompilation"] = "false";

        if (toolchainInfo.IsEmbedded)
        {
            var layout = _embeddedToolchainLayoutService.Inspect(builderBaseDirectory);

            if (!layout.IsStructured)
            {
                var diagnostics = layout.MissingEntries.Count == 0
                    ? "No embedded toolchain diagnostics available."
                    : string.Join(Environment.NewLine, layout.MissingEntries);

                throw new InvalidOperationException(
                    "The embedded toolchain was selected, but its layout is incomplete." +
                    Environment.NewLine + Environment.NewLine +
                    diagnostics);
            }

            environment["DOTNET_ROOT"] = layout.RootDirectory;
            environment["DOTNET_ROOT(x86)"] = layout.RootDirectory;
            environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";

            environment["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] = layout.RootDirectory;
            environment["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = layout.SelectedSdkSdksDirectory;

            environment["MSBuildSDKsPath"] = layout.SelectedSdkSdksDirectory;
            environment["PATH"] = CombinePath(layout.RootDirectory, currentPath);

            return environment;
        }

        environment["PATH"] = currentPath;
        return environment;
    }

    private static string CombinePath(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return first + Path.PathSeparator + second;
    }

    private static async Task<string> ExecuteProcessAsync(
    string fileName,
    string arguments,
    string workingDirectory,
    IReadOnlyDictionary<string, string> environmentVariables,
    Action<string>? onOutputLine,
    CancellationToken cancellationToken)
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var pair in environmentVariables)
        {
            process.StartInfo.Environment[pair.Key] = pair.Value;
        }

        var lines = new List<string>();
        var gate = new object();

        void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            lock (gate)
            {
                lines.Add(line);
            }

            onOutputLine?.Invoke(line);
        }

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {fileName}");
            }

            CompilerChildProcessRegistry.Register(process);

            var stdOutTask = PumpReaderAsync(process.StandardOutput, HandleLine, cancellationToken);
            var stdErrTask = PumpReaderAsync(process.StandardError, HandleLine, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdOutTask, stdErrTask);

            string combinedOutput;

            lock (gate)
            {
                combinedOutput = string.Join(Environment.NewLine, lines);
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{fileName} exited with code {process.ExitCode}.{Environment.NewLine}{combinedOutput}");
            }

            return combinedOutput;
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch
            {
                // Intentionally swallow cleanup exceptions here.
            }

            try
            {
                process.CancelOutputRead();
            }
            catch
            {
                // ignored
            }

            try
            {
                process.CancelErrorRead();
            }
            catch
            {
                // ignored
            }

            try
            {
                process.Close();
            }
            catch
            {
                // ignored
            }

            CompilerChildProcessRegistry.Unregister(process);
        }
    }

    private static async Task PumpReaderAsync(
        StreamReader reader,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                break;
            }

            onLine(line);
        }
    }

    private static string ResolveSinglePublishedExecutable(
        string publishDirectory,
        string preferredFileName)
    {
        var preferredPath = Path.Combine(publishDirectory, preferredFileName);

        if (File.Exists(preferredPath))
        {
            return preferredPath;
        }

        var executables = Directory
            .GetFiles(publishDirectory, "*.exe", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                "createdump.exe",
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (executables.Count == 1)
        {
            return executables[0];
        }

        if (executables.Count == 0)
        {
            throw new FileNotFoundException(
                $"dotnet publish finished, but no executable was found in: {publishDirectory}");
        }

        throw new InvalidOperationException(
            "dotnet publish generated multiple executable candidates and the compiler could not decide which one to use:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, executables));
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
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
}