using InstallBunker.Domain.Enums;

namespace InstallBunker.Compiler.Core.Models;

public sealed class CompilerRequest
{
    public string AppName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Publisher { get; set; } = string.Empty;

    public string SourceDirectory { get; set; } = string.Empty;

    public string MainExecutableRelativePath { get; set; } = string.Empty;

    public string IconRelativePath { get; set; } = string.Empty;

    public string? LicenseFilePath { get; set; }

    public bool AllowPerUser { get; set; } = true;

    public bool AllowPerMachine { get; set; } = true;

    public InstallScope DefaultInstallScope { get; set; } = InstallScope.PerUser;

    public bool DesktopShortcut { get; set; } = true;

    public bool StartMenuShortcut { get; set; } = true;

    public CompilerBrandingOptions Branding { get; set; } = new();

    public CompilerToolchainOptions Toolchain { get; set; } = new();

    public string OutputDirectory { get; set; } = string.Empty;

    public string PackagePassword { get; set; } = string.Empty;

    public bool GenerateDiagnosticsFile { get; set; }
}