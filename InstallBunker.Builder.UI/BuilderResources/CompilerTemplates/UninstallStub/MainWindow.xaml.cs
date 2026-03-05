using InstallBunker.Uninstaller.Core.Models;
using InstallBunker.Uninstaller.Core.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace InstallBunker.Uninstaller.UI;

public partial class MainWindow : Window
{
    private UninstallWizardStep _currentStep = UninstallWizardStep.Welcome;
    private readonly UninstallerSessionService _sessionService = new();
    private UninstallerSessionInfo? _session;
    private UninstallResult? _uninstallResult;
    private bool _isUninstalling;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadInstalledApplication();
            SetStep(UninstallWizardStep.Welcome);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message + Environment.NewLine + Environment.NewLine +
                "Place Uninstall.exe beside install.receipt.json or pass the receipt path as the first command-line argument.",
                "InstallBunker Uninstaller",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Close();
        }
    }

    private void LoadInstalledApplication()
    {
        _session = _sessionService.Load();

        Title = _session.WindowTitle;
        TxtSidebarAppName.Text = _session.SidebarAppName;
        TxtSidebarVersion.Text = _session.SidebarVersion;
        TxtWelcomeSummary.Text = _session.WelcomeSummary;
        TxtInstalledSummary.Text = _session.WelcomeSummary;

        TryLoadSidebarIconFromCurrentExecutable();
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_isUninstalling)
        {
            return;
        }

        switch (_currentStep)
        {
            case UninstallWizardStep.Details:
                SetStep(UninstallWizardStep.Welcome);
                break;
        }
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_isUninstalling || _session is null)
        {
            return;
        }

        switch (_currentStep)
        {
            case UninstallWizardStep.Welcome:
                SetStep(UninstallWizardStep.Details);
                return;

            case UninstallWizardStep.Details:
                await StartUninstallAsync();
                return;

            case UninstallWizardStep.Completed:
                FinishAndCleanup();
                return;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isUninstalling)
        {
            return;
        }

        Close();
    }

    private async Task StartUninstallAsync()
    {
        if (_session is null)
        {
            return;
        }

        _isUninstalling = true;
        UninstallProgressBar.Value = 0;
        TxtUninstallLog.Clear();
        TxtProgressStatus.Text = "Preparing uninstall...";
        SetStep(UninstallWizardStep.Uninstalling);

        var request = new UninstallRequest
        {
            ReceiptFilePath = _session.ReceiptFilePath,
            RemoveInstallDirectoryIfEmpty = true
        };

        var progress = new Progress<UninstallProgressInfo>(info =>
        {
            UninstallProgressBar.Value = info.Percentage;
            TxtProgressStatus.Text = info.Message;
            AppendLog(info.Message);
        });

        try
        {
            var uninstaller = new PackageUninstaller();
            _uninstallResult = await Task.Run(() => uninstaller.Uninstall(request, progress));

            TxtCompletedTitle.Text = "Uninstall Complete";
            TxtCompletedDescription.Text = "The application was removed successfully.";
            TxtCompletedSummary.Text = _sessionService.BuildCompletedSummary(_session, _uninstallResult);

            SetStep(UninstallWizardStep.Completed);
        }
        catch (Exception ex)
        {
            AppendLog("Uninstall failed.");
            AppendLog(ex.Message);

            MessageBox.Show(
                ex.Message,
                "InstallBunker Uninstaller",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            SetStep(UninstallWizardStep.Details);
        }
        finally
        {
            _isUninstalling = false;
        }
    }

    private void FinishAndCleanup()
    {
        TryScheduleSelfDelete();
        Close();
    }

    private void SetStep(UninstallWizardStep step)
    {
        _currentStep = step;

        StepWelcomePanel.Visibility = step == UninstallWizardStep.Welcome ? Visibility.Visible : Visibility.Collapsed;
        StepDetailsPanel.Visibility = step == UninstallWizardStep.Details ? Visibility.Visible : Visibility.Collapsed;
        StepUninstallingPanel.Visibility = step == UninstallWizardStep.Uninstalling ? Visibility.Visible : Visibility.Collapsed;
        StepCompletedPanel.Visibility = step == UninstallWizardStep.Completed ? Visibility.Visible : Visibility.Collapsed;

        BtnBack.IsEnabled = step == UninstallWizardStep.Details;
        BtnCancel.IsEnabled = step != UninstallWizardStep.Uninstalling;

        switch (step)
        {
            case UninstallWizardStep.Welcome:
                BtnNext.Content = "Next >";
                BtnNext.IsEnabled = true;
                TxtFooterHint.Text = "Click Next to continue.";
                break;

            case UninstallWizardStep.Details:
                BtnNext.Content = "Uninstall";
                BtnNext.IsEnabled = true;
                TxtFooterHint.Text = "Review the installed application before uninstalling.";
                break;

            case UninstallWizardStep.Uninstalling:
                BtnBack.IsEnabled = false;
                BtnNext.IsEnabled = false;
                TxtFooterHint.Text = "Removing installed files. Please wait...";
                break;

            case UninstallWizardStep.Completed:
                BtnBack.IsEnabled = false;
                BtnNext.IsEnabled = true;
                BtnNext.Content = "Finish";
                TxtFooterHint.Text = "Uninstall has finished.";
                break;
        }
    }

    private void AppendLog(string message)
    {
        TxtUninstallLog.AppendText($"• {message}{Environment.NewLine}");
        TxtUninstallLog.ScrollToEnd();
    }

    private void TryScheduleSelfDelete()
    {
        try
        {
            var executablePath = _session?.CurrentExecutablePath;

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return;
            }

            var installDirectory = Path.GetDirectoryName(executablePath);

            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                return;
            }

            var command =
                $"/c ping 127.0.0.1 -n 2 > nul & del /f /q \"{executablePath}\" & rmdir /q \"{installDirectory}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = command,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch
        {
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

public enum UninstallWizardStep
{
    Welcome = 0,
    Details = 1,
    Uninstalling = 2,
    Completed = 3
}