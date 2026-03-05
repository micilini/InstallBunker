using InstallBunker.Domain.Enums;

namespace InstallBunker.Builder.UI;

public sealed class BuilderWizardState
{
    public string AppName { get; set; } = "My App";

    public string Version { get; set; } = "1.0.0";

    public string Publisher { get; set; } = "";

    public string SourceDirectory { get; set; } = string.Empty;

    public string MainExecutableRelativePath { get; set; } = string.Empty;

    public string IconRelativePath { get; set; } = string.Empty;

    public string LicenseFilePath { get; set; } = string.Empty;

    public bool AllowPerUser { get; set; } = true;

    public bool AllowPerMachine { get; set; } = true;

    public InstallScope DefaultInstallScope { get; set; } = InstallScope.PerUser;

    public bool DesktopShortcut { get; set; } = true;

    public bool StartMenuShortcut { get; set; } = true;

    public string OutputDirectory { get; set; } = string.Empty;

    public bool GenerateDiagnosticsFile { get; set; }
}