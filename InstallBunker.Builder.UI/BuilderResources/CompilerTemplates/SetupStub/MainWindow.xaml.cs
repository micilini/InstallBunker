using InstallBunker.Common.PackageContainer;
using InstallBunker.Compiler.Generated;
using InstallBunker.Domain.Enums;
using InstallBunker.Installer.Core.Models;
using InstallBunker.Installer.Core.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace InstallBunker.Installer.UI;

public partial class MainWindow : Window
{
    private WizardStep _currentStep = WizardStep.Welcome;
    private readonly InstallerSessionService _sessionService = new();
    private readonly ExtractedPackageValidator _extractedPackageValidator = new();
    private InstallerSessionInfo? _session;
    private string? _packageWorkspacePath;
    private InstallPackageResult? _installResult;
    private bool _isInstalling;
    private bool _installDirectoryTouchedManually;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadPackage();
            SetStep(WizardStep.Welcome);
        }
        catch (PackageContainerReadException ex)
        {
            System.Windows.MessageBox.Show(
                GetFriendlyPackageErrorMessage(ex),
                "InstallBunker Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message + Environment.NewLine + Environment.NewLine +
                "Place Setup.exe beside Package.pkg.",
                "InstallBunker Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Close();
        }
    }

    private static string GetFriendlyPackageErrorMessage(PackageContainerReadException ex)
    {
        return ex.Kind switch
        {
            PackageContainerErrorKind.PackageNotFound =>
                "Package.pkg was not found beside Setup.exe.",

            PackageContainerErrorKind.InvalidMagic or
            PackageContainerErrorKind.InvalidHeaderLength or
            PackageContainerErrorKind.HeaderParseFailed =>
                "This Package.pkg is invalid or corrupted.",

            PackageContainerErrorKind.UnsupportedVersion =>
                "This Package.pkg format version is not supported by this Setup.exe.",

            PackageContainerErrorKind.AuthenticationFailed =>
                "This Package.pkg could not be authenticated. It may be corrupted or modified.",

            PackageContainerErrorKind.IntegrityCheckFailed =>
                "This Package.pkg failed an integrity check. It may be corrupted or modified.",

            PackageContainerErrorKind.InvalidEntryPath or PackageContainerErrorKind.InvalidEntryName =>
                "This Package.pkg contains invalid or unsafe file paths and was blocked for security reasons.",

            PackageContainerErrorKind.LimitsExceeded =>
                "This Package.pkg exceeds safety limits and was blocked to protect your system.",

            PackageContainerErrorKind.InvalidCiphertextLength or
            PackageContainerErrorKind.UnexpectedEndOfStream =>
                "This Package.pkg appears incomplete or corrupted.",

            _ =>
                "An error occurred while reading Package.pkg."
        };
    }

    private void LoadPackage()
    {
        // Package.pkg must sit beside Setup.exe
        var packagePath = Path.Combine(AppContext.BaseDirectory, "Package.pkg");

        _packageWorkspacePath = Path.Combine(
            Path.GetTempPath(),
            "InstallBunker",
            "PackageWorkspace",
            Guid.NewGuid().ToString("N"));

        // Extract (and authenticate) container into temp workspace
        PackageContainerReader.ExtractToDirectory(
            packagePath,
            SetupGeneratedCompilerBranding.PackagePassword,
            _packageWorkspacePath);

        var validationResult = _extractedPackageValidator.Validate(_packageWorkspacePath);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                "The internal Package.pkg structure is invalid:" + Environment.NewLine +
                string.Join(
                    Environment.NewLine,
                    validationResult.Errors.Select(error => $"• {error}")));
        }

        _session = _sessionService.Load(_packageWorkspacePath);

        Title = _session.WindowTitle;
        TxtSidebarAppName.Text = _session.SidebarAppName;
        TxtSidebarVersion.Text = _session.SidebarVersion;
        TxtWelcomeSummary.Text = _session.WelcomeSummary;
        TxtLicenseContent.Text = _session.LicenseText;

        TryLoadSidebarIconFromCurrentExecutable();

        RbPerUser.IsEnabled = _session.AllowPerUser;
        RbPerMachine.IsEnabled = _session.AllowPerMachine;

        if (_session.DefaultInstallScope == InstallScope.PerMachine && _session.AllowPerMachine)
        {
            RbPerMachine.IsChecked = true;
        }
        else
        {
            RbPerUser.IsChecked = true;
        }

        UpdateInstallDirectoryFromSelectedScope(force: true);
        _installDirectoryTouchedManually = false;

        ChkDesktopShortcut.IsChecked = _session.DefaultDesktopShortcut;
        ChkStartMenuShortcut.IsChecked = _session.DefaultStartMenuShortcut;
        ChkLaunchApp.IsChecked = _session.AllowLaunchAfterInstall;
        ChkLaunchApp.Visibility = _session.AllowLaunchAfterInstall
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdateDirectoryHint();
        UpdateOptionsSummary();
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalling)
        {
            return;
        }

        switch (_currentStep)
        {
            case WizardStep.License:
                SetStep(WizardStep.Welcome);
                break;

            case WizardStep.Directory:
                SetStep(ShouldShowLicenseStep() ? WizardStep.License : WizardStep.Welcome);
                break;

            case WizardStep.Options:
                SetStep(WizardStep.Directory);
                break;

            case WizardStep.Completed:
                break;
        }
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || _isInstalling)
        {
            return;
        }

        switch (_currentStep)
        {
            case WizardStep.Welcome:
                SetStep(ShouldShowLicenseStep() ? WizardStep.License : WizardStep.Directory);
                return;

            case WizardStep.License:
                if (ChkAcceptLicense.IsChecked != true)
                {
                    System.Windows.MessageBox.Show(
                        "You must accept the license agreement before continuing.",
                        "InstallBunker Setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                SetStep(WizardStep.Directory);
                return;

            case WizardStep.Directory:
                if (string.IsNullOrWhiteSpace(TxtInstallDirectory.Text))
                {
                    System.Windows.MessageBox.Show(
                        "Please choose an installation directory.",
                        "InstallBunker Setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                SetStep(WizardStep.Options);
                return;

            case WizardStep.Options:
                await StartInstallationAsync();
                return;

            case WizardStep.Completed:
                FinishSetup();
                return;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalling)
        {
            return;
        }

        Close();
    }

    private void InstallScope_Checked(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        UpdateInstallDirectoryFromSelectedScope(force: !_installDirectoryTouchedManually);
        UpdateDirectoryHint();
        UpdateOptionsSummary();
    }

    private void BtnBrowseInstallDirectory_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the installation folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(TxtInstallDirectory.Text) &&
            Directory.Exists(TxtInstallDirectory.Text))
        {
            dialog.InitialDirectory = TxtInstallDirectory.Text;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtInstallDirectory.Text = dialog.SelectedPath;
            _installDirectoryTouchedManually = true;
            UpdateOptionsSummary();
        }
    }

    private async Task StartInstallationAsync()
    {
        if (_session is null)
        {
            return;
        }

        var selectedScope = GetSelectedScope();

        if (selectedScope == InstallScope.PerMachine && !IsRunningAsAdministrator())
        {
            System.Windows.MessageBox.Show(
                "Per-machine installation requires Administrator privileges." + Environment.NewLine +
                "Please reopen Setup.exe as Administrator or choose Per-user.",
                "InstallBunker Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _isInstalling = true;
        InstallProgressBar.Value = 0;
        TxtInstallLog.Clear();
        TxtProgressStatus.Text = "Preparing installation...";
        SetStep(WizardStep.Installing);

        var request = new InstallPackageRequest
        {
            PackageRootDirectory = _session.PackageRootDirectory,
            InstallScopeOverride = selectedScope,
            OverrideInstallDirectory = TxtInstallDirectory.Text.Trim(),
            CreateDesktopShortcutOverride = ChkDesktopShortcut.IsChecked == true,
            CreateStartMenuShortcutOverride = ChkStartMenuShortcut.IsChecked == true
        };

        var progress = new Progress<InstallProgressInfo>(info =>
        {
            InstallProgressBar.Value = info.Percentage;
            TxtProgressStatus.Text = info.Message;
            AppendLog(info.Message);
        });

        try
        {
            var installer = new PackageInstaller();

            _installResult = await Task.Run(() => installer.Install(request, progress));

            TxtCompletedTitle.Text = "Installation Complete";
            TxtCompletedDescription.Text = "Setup completed successfully.";
            TxtCompletedSummary.Text = _sessionService.BuildCompletedSummary(_session, _installResult);

            SetStep(WizardStep.Completed);
        }
        catch (Exception ex)
        {
            AppendLog("Installation failed.");
            AppendLog(ex.Message);

            System.Windows.MessageBox.Show(
                ex.Message,
                "InstallBunker Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            SetStep(WizardStep.Options);
        }
        finally
        {
            _isInstalling = false;
        }
    }

    private void FinishSetup()
    {
        if (ChkLaunchApp.IsChecked == true &&
            _installResult is not null &&
            File.Exists(_installResult.MainExecutablePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _installResult.MainExecutablePath,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        Close();
    }

    private bool ShouldShowLicenseStep()
    {
        return _session?.ShowLicensePage == true;
    }

    private InstallScope GetSelectedScope()
    {
        return RbPerMachine.IsChecked == true
            ? InstallScope.PerMachine
            : InstallScope.PerUser;
    }

    private void UpdateInstallDirectoryFromSelectedScope(bool force)
    {
        if (_session is null || !force)
        {
            return;
        }

        TxtInstallDirectory.Text = _sessionService.ResolveInstallDirectory(
            _session,
            GetSelectedScope());
    }

    private void UpdateDirectoryHint()
    {
        if (_session is null)
        {
            return;
        }

        TxtDirectoryHint.Text = _sessionService.BuildDirectoryHint(GetSelectedScope());
    }

    private void UpdateOptionsSummary()
    {
        if (_session is null)
        {
            return;
        }

        var snapshot = new InstallOptionsSnapshot
        {
            InstallScope = GetSelectedScope(),
            InstallDirectory = TxtInstallDirectory.Text,
            DesktopShortcut = ChkDesktopShortcut.IsChecked == true,
            StartMenuShortcut = ChkStartMenuShortcut.IsChecked == true
        };

        TxtOptionsSummary.Text = _sessionService.BuildOptionsSummary(snapshot);
    }

    private void SetStep(WizardStep step)
    {
        _currentStep = step;

        StepWelcomePanel.Visibility = step == WizardStep.Welcome ? Visibility.Visible : Visibility.Collapsed;
        StepLicensePanel.Visibility = step == WizardStep.License ? Visibility.Visible : Visibility.Collapsed;
        StepDirectoryPanel.Visibility = step == WizardStep.Directory ? Visibility.Visible : Visibility.Collapsed;
        StepOptionsPanel.Visibility = step == WizardStep.Options ? Visibility.Visible : Visibility.Collapsed;
        StepInstallingPanel.Visibility = step == WizardStep.Installing ? Visibility.Visible : Visibility.Collapsed;
        StepCompletedPanel.Visibility = step == WizardStep.Completed ? Visibility.Visible : Visibility.Collapsed;

        BtnBack.IsEnabled = step is WizardStep.License or WizardStep.Directory or WizardStep.Options;
        BtnCancel.IsEnabled = step != WizardStep.Installing;

        switch (step)
        {
            case WizardStep.Welcome:
                BtnNext.Content = "Next >";
                TxtFooterHint.Text = "Click Next to continue.";
                break;

            case WizardStep.License:
                BtnNext.Content = "Next >";
                TxtFooterHint.Text = "Review the license agreement.";
                break;

            case WizardStep.Directory:
                BtnNext.Content = "Next >";
                TxtFooterHint.Text = "Choose where the application should be installed.";
                break;

            case WizardStep.Options:
                BtnNext.Content = "Install";
                TxtFooterHint.Text = "Review the selected installation options.";
                UpdateOptionsSummary();
                break;

            case WizardStep.Installing:
                BtnBack.IsEnabled = false;
                BtnNext.IsEnabled = false;
                TxtFooterHint.Text = "Installing files. Please wait...";
                break;

            case WizardStep.Completed:
                BtnBack.IsEnabled = false;
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Finish";
                TxtFooterHint.Text = "Setup has finished.";
                break;
        }

        if (step != WizardStep.Installing)
        {
            BtnNext.IsEnabled = true;
        }
    }

    private void AppendLog(string message)
    {
        TxtInstallLog.AppendText($"• {message}{Environment.NewLine}");
        TxtInstallLog.ScrollToEnd();
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        TryCleanupPackageWorkspace();
    }

    private void TryCleanupPackageWorkspace()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_packageWorkspacePath) && Directory.Exists(_packageWorkspacePath))
            {
                Directory.Delete(_packageWorkspacePath, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private void TryLoadSidebarIconFromCurrentExecutable()
    {
        IntPtr hIcon = IntPtr.Zero;

        try
        {
            var executablePath = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return;
            }

            var fileInfo = new SHFILEINFO();
            hIcon = SHGetFileInfo(
                executablePath,
                0,
                out fileInfo,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI_ICON | SHGFI_LARGEICON);

            if (fileInfo.hIcon == IntPtr.Zero)
            {
                return;
            }

            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                fileInfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(64, 64));

            bitmapSource.Freeze();

            SidebarAppIcon.Source = bitmapSource;
            SidebarIconImageBorder.Visibility = Visibility.Visible;
            SidebarIconFallbackBorder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            // keep fallback "IB"
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
            {
                DestroyIcon(hIcon);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
}

public enum WizardStep
{
    Welcome = 0,
    License = 1,
    Directory = 2,
    Options = 3,
    Installing = 4,
    Completed = 5
}