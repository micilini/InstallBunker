using InstallBunker.Domain.Enums;

namespace InstallBunker.Domain.Models;

public sealed class PackageManifest
{
    public int SchemaVersion { get; set; } = 1;

    public PackageInfo PackageInfo { get; set; } = new();

    public InstallSettings InstallSettings { get; set; } = new();

    public ShortcutOptions Shortcuts { get; set; } = new();

    public InstallerUiOptions Ui { get; set; } = new();

    public PackageBrandingOptions Branding { get; set; } = new();

    public List<PackageFileEntry> Files { get; set; } = new();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (SchemaVersion <= 0)
        {
            errors.Add("SchemaVersion must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(PackageInfo.AppName))
        {
            errors.Add("PackageInfo.AppName is required.");
        }

        if (string.IsNullOrWhiteSpace(PackageInfo.Version))
        {
            errors.Add("PackageInfo.Version is required.");
        }

        if (string.IsNullOrWhiteSpace(PackageInfo.Publisher))
        {
            errors.Add("PackageInfo.Publisher is required.");
        }

        if (string.IsNullOrWhiteSpace(PackageInfo.MainExecutable))
        {
            errors.Add("PackageInfo.MainExecutable is required.");
        }

        if (!InstallSettings.AllowPerUser && !InstallSettings.AllowPerMachine)
        {
            errors.Add("At least one install scope must be allowed.");
        }

        if (InstallSettings.DefaultInstallScope == InstallScope.PerUser && !InstallSettings.AllowPerUser)
        {
            errors.Add("DefaultInstallScope is PerUser, but AllowPerUser is false.");
        }

        if (InstallSettings.DefaultInstallScope == InstallScope.PerMachine && !InstallSettings.AllowPerMachine)
        {
            errors.Add("DefaultInstallScope is PerMachine, but AllowPerMachine is false.");
        }

        if (string.IsNullOrWhiteSpace(InstallSettings.DefaultInstallDirPerUser))
        {
            errors.Add("InstallSettings.DefaultInstallDirPerUser is required.");
        }

        if (string.IsNullOrWhiteSpace(InstallSettings.DefaultInstallDirPerMachine))
        {
            errors.Add("InstallSettings.DefaultInstallDirPerMachine is required.");
        }

        if (Files.Count == 0)
        {
            errors.Add("At least one file entry is required.");
        }

        for (int i = 0; i < Files.Count; i++)
        {
            var file = Files[i];

            if (string.IsNullOrWhiteSpace(file.Source))
            {
                errors.Add($"Files[{i}].Source is required.");
            }

            if (string.IsNullOrWhiteSpace(file.RelativeTarget))
            {
                errors.Add($"Files[{i}].RelativeTarget is required.");
            }
        }

        if (Ui.ShowLicensePage && string.IsNullOrWhiteSpace(Ui.LicenseFile))
        {
            errors.Add("Ui.LicenseFile is required when Ui.ShowLicensePage is true.");
        }

        if (string.IsNullOrWhiteSpace(Branding.SetupWindowTitle))
        {
            errors.Add("Branding.SetupWindowTitle is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.SetupSidebarAppName))
        {
            errors.Add("Branding.SetupSidebarAppName is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.SetupSidebarVersion))
        {
            errors.Add("Branding.SetupSidebarVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.SetupWelcomeSummary))
        {
            errors.Add("Branding.SetupWelcomeSummary is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.UninstallWindowTitle))
        {
            errors.Add("Branding.UninstallWindowTitle is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.UninstallSidebarAppName))
        {
            errors.Add("Branding.UninstallSidebarAppName is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.UninstallSidebarVersion))
        {
            errors.Add("Branding.UninstallSidebarVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.UninstallWelcomeSummary))
        {
            errors.Add("Branding.UninstallWelcomeSummary is required.");
        }

        return errors;
    }
}