using InstallBunker.Compiler.Core.Models;
using InstallBunker.Compiler.Core.Paths;

namespace InstallBunker.Compiler.Core.Services;

public sealed class CompilerToolchainResolver
{
    private readonly EmbeddedToolchainLayoutService _embeddedToolchainLayoutService = new();

    public EmbeddedToolchainInfo Resolve(
    CompilerToolchainOptions options,
    string builderBaseDirectory)
    {
        if (options is null)
        {
            throw new InvalidOperationException("CompilerToolchainOptions is required.");
        }

        if (string.IsNullOrWhiteSpace(builderBaseDirectory))
        {
            throw new InvalidOperationException("Builder base directory is required.");
        }

        var diagnostics = new List<string>();

        if (options.PreferEmbeddedToolchain)
        {
            var embeddedLayout = _embeddedToolchainLayoutService.Inspect(builderBaseDirectory);

            if (embeddedLayout.IsStructured)
            {
                return new EmbeddedToolchainInfo
                {
                    SourceKind = "embedded",
                    DisplayName = "embedded toolchain",
                    DotNetCommand = embeddedLayout.DotNetHostPath,
                    RootDirectory = embeddedLayout.RootDirectory,
                    SdkDirectory = embeddedLayout.SdkDirectory,
                    PacksDirectory = embeddedLayout.PacksDirectory,
                    SharedDirectory = embeddedLayout.SharedDirectory,
                    SelectedSdkVersion = embeddedLayout.SelectedSdkVersion,
                    SelectedSdkDirectory = embeddedLayout.SelectedSdkDirectory,
                    SelectedSdkSdksDirectory = embeddedLayout.SelectedSdkSdksDirectory,
                    IsEmbedded = true
                };
            }

            if (embeddedLayout.RootDirectoryExists || embeddedLayout.MissingEntries.Count > 0)
            {
                diagnostics.Add("Embedded toolchain diagnostics:");
                foreach (var entry in embeddedLayout.MissingEntries)
                {
                    diagnostics.Add($"  • {entry}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(options.ExplicitDotNetHostPath))
        {
            var explicitPath = Path.GetFullPath(options.ExplicitDotNetHostPath);

            if (File.Exists(explicitPath))
            {
                return new EmbeddedToolchainInfo
                {
                    SourceKind = "explicit",
                    DisplayName = "explicitly configured toolchain",
                    DotNetCommand = explicitPath,
                    RootDirectory = Path.GetDirectoryName(explicitPath),
                    IsExplicit = true
                };
            }

            diagnostics.Add($"Explicit dotnet host path not found: {explicitPath}");
        }

        if (options.AllowSystemDotNetFallback && options.IsDevelopmentMode)
        {
            return new EmbeddedToolchainInfo
            {
                SourceKind = "system",
                DisplayName = "system dotnet fallback",
                DotNetCommand = "dotnet",
                RootDirectory = null,
                IsSystemFallback = true
            };
        }

        if (options.AllowSystemDotNetFallback && !options.IsDevelopmentMode)
        {
            diagnostics.Add(
                "System dotnet fallback was requested, but it is disabled because the compiler is running in product mode.");
        }

        var message =
            "The compiler could not resolve a usable .NET toolchain." +
            Environment.NewLine + Environment.NewLine +
            "Resolution order:" + Environment.NewLine +
            "  1. Embedded toolchain" + Environment.NewLine +
            "  2. Explicitly configured dotnet host" + Environment.NewLine +
            "  3. System dotnet fallback (development mode only)" + Environment.NewLine + Environment.NewLine +
            (diagnostics.Count == 0
                ? "No additional toolchain diagnostics were produced."
                : string.Join(Environment.NewLine, diagnostics));

        throw new InvalidOperationException(message);
    }
}