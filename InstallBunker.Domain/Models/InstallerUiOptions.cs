namespace InstallBunker.Domain.Models;

public sealed class InstallerUiOptions
{
    public bool ShowLicensePage { get; set; } = false;

    public string LicenseFile { get; set; } = string.Empty;

    public bool AllowLaunchAfterInstall { get; set; } = true;
}