using InstallBunker.Common.Serialization;
using InstallBunker.Domain.Enums;
using InstallBunker.Installer.Core.Models;
using System.Text;

namespace InstallBunker.Installer.Core.Services;

public sealed class InstallerSessionService
{
    public InstallerSessionInfo Load(string packageRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageRootDirectory))
        {
            throw new InvalidOperationException("Package root directory is required.");
        }

        if (!Directory.Exists(packageRootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Package root directory not found: {packageRootDirectory}");
        }

        var manifestPath = Path.Combine(packageRootDirectory, "manifest.json");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("manifest.json was not found beside Setup.exe.", manifestPath);
        }

        var manifest = JsonFileStore.Load<Domain.Models.PackageManifest>(manifestPath);
        var errors = manifest.Validate();

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The package manifest is invalid:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors));
        }

        var licenseText = "No license file was provided for this package.";

        if (manifest.Ui.ShowLicensePage)
        {
            var licensePath = Path.Combine(packageRootDirectory, manifest.Ui.LicenseFile);

            if (!File.Exists(licensePath))
            {
                throw new FileNotFoundException(
                    "License file declared in manifest was not found.",
                    licensePath);
            }

            licenseText = File.ReadAllText(licensePath, Encoding.UTF8);
        }

        return new InstallerSessionInfo
        {
            PackageRootDirectory = packageRootDirectory,
            Manifest = manifest,
            WindowTitle = manifest.Branding.SetupWindowTitle,
            SidebarAppName = manifest.Branding.SetupSidebarAppName,
            SidebarVersion = manifest.Branding.SetupSidebarVersion,
            WelcomeSummary = manifest.Branding.SetupWelcomeSummary,
            LicenseText = licenseText,
            ShowLicensePage = manifest.Ui.ShowLicensePage,
            AllowPerUser = manifest.InstallSettings.AllowPerUser,
            AllowPerMachine = manifest.InstallSettings.AllowPerMachine,
            DefaultInstallScope = manifest.InstallSettings.DefaultInstallScope,
            DefaultDesktopShortcut = manifest.Shortcuts.Desktop,
            DefaultStartMenuShortcut = manifest.Shortcuts.StartMenu,
            AllowLaunchAfterInstall = manifest.Ui.AllowLaunchAfterInstall
        };
    }

    public string ResolveInstallDirectory(InstallerSessionInfo session, InstallScope installScope)
    {
        if (session is null)
        {
            throw new InvalidOperationException("InstallerSessionInfo is required.");
        }

        var rawPath = installScope == InstallScope.PerMachine
            ? session.Manifest.InstallSettings.DefaultInstallDirPerMachine
            : session.Manifest.InstallSettings.DefaultInstallDirPerUser;

        return Environment.ExpandEnvironmentVariables(rawPath);
    }

    public string BuildDirectoryHint(InstallScope installScope)
    {
        return installScope == InstallScope.PerMachine
            ? "Per-machine installs are visible to all users and usually require Administrator privileges."
            : "Per-user installs stay inside your user profile and usually do not require Administrator privileges.";
    }

    public string BuildOptionsSummary(InstallOptionsSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new InvalidOperationException("InstallOptionsSnapshot is required.");
        }

        var scopeText = snapshot.InstallScope == InstallScope.PerMachine ? "Per-machine" : "Per-user";
        var desktopText = snapshot.DesktopShortcut ? "Yes" : "No";
        var startMenuText = snapshot.StartMenuShortcut ? "Yes" : "No";

        return
            $"Install scope: {scopeText}{Environment.NewLine}" +
            $"Install folder: {snapshot.InstallDirectory}{Environment.NewLine}" +
            $"Desktop shortcut: {desktopText}{Environment.NewLine}" +
            $"Start Menu shortcut: {startMenuText}";
    }

    public string BuildCompletedSummary(InstallerSessionInfo session, InstallPackageResult result)
    {
        if (session is null)
        {
            throw new InvalidOperationException("InstallerSessionInfo is required.");
        }

        if (result is null)
        {
            throw new InvalidOperationException("InstallPackageResult is required.");
        }

        return
            $"Application: {session.Manifest.PackageInfo.AppName}{Environment.NewLine}" +
            $"Installed to: {result.InstallDirectory}{Environment.NewLine}" +
            $"Files installed: {result.InstalledFilesCount}{Environment.NewLine}" +
            $"Shortcuts created: {result.CreatedShortcutsCount}";
    }
}