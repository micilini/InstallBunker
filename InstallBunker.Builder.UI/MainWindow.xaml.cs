using InstallBunker.Builder.UI.Services.Compiler;
using InstallBunker.Common.Paths;
using InstallBunker.Compiler.Core.Models;
using InstallBunker.Compiler.Core.Paths;
using InstallBunker.Compiler.Core.Services;
using InstallBunker.Domain.Enums;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace InstallBunker.Builder.UI;

public partial class MainWindow : Window
{
    private BuilderStep _currentStep = BuilderStep.Welcome;
    private readonly BuilderWizardState _state = new();
    private readonly EmbeddedToolchainLayoutService _embeddedToolchainLayoutService = new();
    private readonly BuilderCompilationService _builderCompilationService = new();
    private bool _isBuilding;

    private bool _startupValidationFailed;
    private string? _currentProjectFilePath;
    private bool _isDirty;
    private bool _isApplyingState;

    public MainWindow()
    {
        InitializeComponent();

        if (!ValidateBundledModulesOnStartup())
        {
            _startupValidationFailed = true;
        }

        Loaded += MainWindow_Loaded;
    }

    private void EnsureDefaultOutputDirectory()
    {
        if (TxtOutputDirectory is not null && _isApplyingState && string.IsNullOrWhiteSpace(_state.OutputDirectory))
        {
            TxtOutputDirectory.Text = string.Empty;
        }
    }

    private string ConvertFullPathToRelativePathInsideSource(string sourceDirectory, string selectedFilePath, string fieldDisplayName)
    {
        var normalizedSourceDirectory = Path.GetFullPath(sourceDirectory);
        var normalizedSelectedFilePath = Path.GetFullPath(selectedFilePath);

        if (!normalizedSelectedFilePath.StartsWith(normalizedSourceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{fieldDisplayName} must be inside the selected Source Directory.");
        }

        return Path.GetRelativePath(normalizedSourceDirectory, normalizedSelectedFilePath);
    }

    private string ConvertRelativePathToDisplayPath(string sourceDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(relativePath))
        {
            return relativePath;
        }

        return Path.GetFullPath(Path.Combine(sourceDirectory, relativePath));
    }

    private EmbeddedToolchainLayoutInfo InspectEmbeddedToolchain()
    {
        return _embeddedToolchainLayoutService.Inspect(AppContext.BaseDirectory);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_startupValidationFailed)
        {
            Close();
            return;
        }

        _state.OutputDirectory = string.Empty;

        TxtAppName.Text = _state.AppName;
        TxtVersion.Text = _state.Version;
        TxtPublisher.Text = _state.Publisher;
        TxtOutputDirectory.Text = _state.OutputDirectory;

        ChkAllowPerUser.IsChecked = _state.AllowPerUser;
        ChkAllowPerMachine.IsChecked = _state.AllowPerMachine;
        CmbDefaultScope.SelectedIndex = 0;
        ChkDesktopShortcut.IsChecked = _state.DesktopShortcut;
        ChkStartMenuShortcut.IsChecked = _state.StartMenuShortcut;
        ChkGenerateDiagnosticsFile.IsChecked = _state.GenerateDiagnosticsFile;

        UpdateFilesHint();
        UpdateOptionsHint();
        SetStep(BuilderStep.Welcome);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isBuilding)
        {
            e.Cancel = true;
            return;
        }

        if (!PromptSaveChangesIfNeeded())
        {
            e.Cancel = true;
        }
    }

    private void WireDirtyTracking()
    {
        TxtAppName.TextChanged += AnyInputChanged;
        TxtVersion.TextChanged += AnyInputChanged;
        TxtPublisher.TextChanged += AnyInputChanged;
        TxtOutputDirectory.TextChanged += AnyInputChanged;
        TxtSourceDirectory.TextChanged += AnyInputChanged;
        TxtMainExecutable.TextChanged += AnyInputChanged;
        TxtIconPath.TextChanged += AnyInputChanged;
        TxtLicenseFile.TextChanged += AnyInputChanged;

        ChkAllowPerUser.Checked += AnyInputChanged;
        ChkAllowPerUser.Unchecked += AnyInputChanged;
        ChkAllowPerMachine.Checked += AnyInputChanged;
        ChkAllowPerMachine.Unchecked += AnyInputChanged;
        ChkDesktopShortcut.Checked += AnyInputChanged;
        ChkDesktopShortcut.Unchecked += AnyInputChanged;
        ChkStartMenuShortcut.Checked += AnyInputChanged;
        ChkStartMenuShortcut.Unchecked += AnyInputChanged;

        CmbDefaultScope.SelectionChanged += AnyInputChanged;

        ChkGenerateDiagnosticsFile.Checked += AnyInputChanged;
        ChkGenerateDiagnosticsFile.Unchecked += AnyInputChanged;
    }

    private void AnyInputChanged(object? sender, EventArgs e)
    {
        if (_isApplyingState)
        {
            return;
        }

        MarkDirty();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        UpdateWindowTitle();
    }

    private void MarkClean()
    {
        _isDirty = false;
        UpdateWindowTitle();
    }

    private bool PromptSaveChangesIfNeeded()
    {
        if (!_isDirty)
        {
            return true;
        }

        var result = System.Windows.MessageBox.Show(
            "You have unsaved changes in the current Builder project." + Environment.NewLine + Environment.NewLine +
            "Do you want to save before continuing?",
            "InstallBunker Builder",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            return true;
        }

        SyncStateFromInputs();

        if (string.IsNullOrWhiteSpace(_currentProjectFilePath))
        {
            return SaveProjectAsInternal();
        }

        SaveProjectToPath(_currentProjectFilePath);
        return true;
    }

    private bool EnsureProjectSavedBeforeBuild()
    {
        SyncStateFromInputs();

        if (!_isDirty && !string.IsNullOrWhiteSpace(_currentProjectFilePath))
        {
            return true;
        }

        var message = string.IsNullOrWhiteSpace(_currentProjectFilePath)
            ? "This package project has not been saved yet." + Environment.NewLine + Environment.NewLine +
              "Do you want to save it as a .ibb file before generating the package?"
            : "This project has unsaved changes." + Environment.NewLine + Environment.NewLine +
              "Do you want to save the .ibb file before generating the package?";

        var result = System.Windows.MessageBox.Show(
            message,
            "InstallBunker Builder",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_currentProjectFilePath))
        {
            return SaveProjectAsInternal();
        }

        SaveProjectToPath(_currentProjectFilePath);
        return true;
    }

    private void UpdateRecentProjectsMenu()
    {
        MenuRecentProjects.Items.Clear();

        var items = BuilderRecentProjectsStore.Load();

        if (items.Count == 0)
        {
            MenuRecentProjects.Items.Add(new MenuItem
            {
                Header = "(Empty)",
                IsEnabled = false
            });

            return;
        }

        foreach (var path in items)
        {
            var menuItem = new MenuItem
            {
                Header = Path.GetFileName(path),
                ToolTip = path,
                Tag = path
            };

            menuItem.Click += MenuRecentProjectItem_Click;
            MenuRecentProjects.Items.Add(menuItem);
        }
    }

    private void MenuRecentProjectItem_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        if (!PromptSaveChangesIfNeeded())
        {
            return;
        }

        if (sender is not MenuItem menuItem || menuItem.Tag is not string projectPath)
        {
            return;
        }

        OpenProjectFromPath(projectPath);
    }

    private void OpenProjectFromPath(string filePath)
    {
        var project = BuilderProjectFileService.Load(filePath);

        _state.AppName = project.AppName;
        _state.Version = project.Version;
        _state.Publisher = project.Publisher;
        _state.SourceDirectory = project.SourceDirectory;
        _state.MainExecutableRelativePath = project.MainExecutableRelativePath;
        _state.IconRelativePath = project.IconRelativePath;
        _state.LicenseFilePath = project.LicenseFilePath;
        _state.AllowPerUser = project.AllowPerUser;
        _state.AllowPerMachine = project.AllowPerMachine;
        _state.DefaultInstallScope = project.DefaultInstallScope;
        _state.DesktopShortcut = project.DesktopShortcut;
        _state.StartMenuShortcut = project.StartMenuShortcut;
        _state.OutputDirectory = project.OutputDirectory ?? string.Empty;
        _state.GenerateDiagnosticsFile = project.GenerateDiagnosticsFile;

        _currentProjectFilePath = filePath;

        ApplyStateToInputs();

        BuilderRecentProjectsStore.Add(filePath);
        UpdateRecentProjectsMenu();

        MarkClean();
        UpdateFilesHint();
        UpdateOptionsHint();
        SetStep(BuilderStep.PackageInfo);
    }

    private bool SaveProjectAsInternal()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save InstallBunker Builder Project",
            Filter = "InstallBunker Builder Project (*.ibb)|*.ibb|All files (*.*)|*.*",
            DefaultExt = "ibb",
            AddExtension = true,
            FileName = string.IsNullOrWhiteSpace(_state.AppName) ? "Project.ibb" : $"{_state.AppName}.ibb"
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return false;
        }

        SaveProjectToPath(dialog.FileName);
        return true;
    }

    private void MenuNewProject_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        if (!PromptSaveChangesIfNeeded())
        {
            return;
        }

        ResetStateToDefaults();
        ApplyStateToInputs();
        _currentProjectFilePath = null;
        MarkClean();
        UpdateWindowTitle();
        SetStep(BuilderStep.Welcome);
    }

    private void MenuOpenProject_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        if (!PromptSaveChangesIfNeeded())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Open InstallBunker Builder Project",
            Filter = "InstallBunker Builder Project (*.ibb)|*.ibb|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        OpenProjectFromPath(dialog.FileName);
    }

    private void MenuSaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        SyncStateFromInputs();

        if (string.IsNullOrWhiteSpace(_currentProjectFilePath))
        {
            SaveProjectAs();
            return;
        }

        SaveProjectToPath(_currentProjectFilePath);
    }

    private void MenuSaveAsProject_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        SyncStateFromInputs();
        SaveProjectAs();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        Close();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            "InstallBunker Builder (V 1.0.0) - Portal Micilini 2026" + Environment.NewLine +
            "Project builder for Setup.exe + manifest.json + payload." + Environment.NewLine + Environment.NewLine +
            "Project files use the .ibb extension.",
            "About",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SaveProjectAs()
    {
        SaveProjectAsInternal();
    }

    private void SaveProjectToPath(string filePath)
    {
        var project = new BuilderProjectFile
        {
            SchemaVersion = 1,
            AppName = _state.AppName,
            Version = _state.Version,
            Publisher = _state.Publisher,
            SourceDirectory = _state.SourceDirectory,
            MainExecutableRelativePath = _state.MainExecutableRelativePath,
            IconRelativePath = _state.IconRelativePath,
            LicenseFilePath = _state.LicenseFilePath,
            AllowPerUser = _state.AllowPerUser,
            AllowPerMachine = _state.AllowPerMachine,
            DefaultInstallScope = _state.DefaultInstallScope,
            DesktopShortcut = _state.DesktopShortcut,
            StartMenuShortcut = _state.StartMenuShortcut,
            OutputDirectory = _state.OutputDirectory,
            GenerateDiagnosticsFile = _state.GenerateDiagnosticsFile
        };

        BuilderProjectFileService.Save(filePath, project);
        _currentProjectFilePath = filePath;

        BuilderRecentProjectsStore.Add(filePath);
        UpdateRecentProjectsMenu();
        MarkClean();

        System.Windows.MessageBox.Show(
            $"Project saved successfully.{Environment.NewLine}{Environment.NewLine}{filePath}",
            "InstallBunker Builder",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ResetStateToDefaults()
    {
        _state.AppName = "My App";
        _state.Version = "1.0.0";
        _state.Publisher = "";
        _state.SourceDirectory = string.Empty;
        _state.MainExecutableRelativePath = string.Empty;
        _state.IconRelativePath = string.Empty;
        _state.LicenseFilePath = string.Empty;
        _state.AllowPerUser = true;
        _state.AllowPerMachine = true;
        _state.DefaultInstallScope = InstallScope.PerUser;
        _state.DesktopShortcut = true;
        _state.StartMenuShortcut = true;
        _state.OutputDirectory = string.Empty;
        _state.GenerateDiagnosticsFile = false;
    }

    private void ApplyStateToInputs()
    {
        _isApplyingState = true;

        try
        {
            EnsureDefaultOutputDirectory();

            TxtAppName.Text = _state.AppName;
            TxtVersion.Text = _state.Version;
            TxtPublisher.Text = _state.Publisher;
            TxtSourceDirectory.Text = _state.SourceDirectory;
            TxtMainExecutable.Text = ConvertRelativePathToDisplayPath(_state.SourceDirectory, _state.MainExecutableRelativePath);
            TxtIconPath.Text = ConvertRelativePathToDisplayPath(_state.SourceDirectory, _state.IconRelativePath);
            TxtLicenseFile.Text = _state.LicenseFilePath;
            TxtOutputDirectory.Text = _state.OutputDirectory;

            ChkAllowPerUser.IsChecked = _state.AllowPerUser;
            ChkAllowPerMachine.IsChecked = _state.AllowPerMachine;
            CmbDefaultScope.SelectedIndex = _state.DefaultInstallScope == InstallScope.PerMachine ? 1 : 0;
            ChkDesktopShortcut.IsChecked = _state.DesktopShortcut;
            ChkStartMenuShortcut.IsChecked = _state.StartMenuShortcut;
            ChkGenerateDiagnosticsFile.IsChecked = _state.GenerateDiagnosticsFile;
        }
        finally
        {
            _isApplyingState = false;
        }
    }

    private void SyncStateFromInputs()
    {
        _state.AppName = TxtAppName.Text.Trim();
        _state.Version = TxtVersion.Text.Trim();
        _state.Publisher = TxtPublisher.Text.Trim();
        _state.SourceDirectory = TxtSourceDirectory.Text.Trim();
        _state.LicenseFilePath = TxtLicenseFile.Text.Trim();
        _state.AllowPerUser = ChkAllowPerUser.IsChecked == true;
        _state.AllowPerMachine = ChkAllowPerMachine.IsChecked == true;
        _state.DefaultInstallScope = CmbDefaultScope.SelectedIndex == 1 ? InstallScope.PerMachine : InstallScope.PerUser;
        _state.DesktopShortcut = ChkDesktopShortcut.IsChecked == true;
        _state.StartMenuShortcut = ChkStartMenuShortcut.IsChecked == true;
        _state.OutputDirectory = TxtOutputDirectory.Text.Trim();
        _state.GenerateDiagnosticsFile = ChkGenerateDiagnosticsFile.IsChecked == true;

        if (!string.IsNullOrWhiteSpace(_state.SourceDirectory) && !string.IsNullOrWhiteSpace(TxtMainExecutable.Text))
        {
            _state.MainExecutableRelativePath = ConvertFullPathToRelativePathInsideSource(
                _state.SourceDirectory,
                TxtMainExecutable.Text.Trim(),
                "Main Executable");
        }
        else
        {
            _state.MainExecutableRelativePath = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(_state.SourceDirectory) && !string.IsNullOrWhiteSpace(TxtIconPath.Text))
        {
            _state.IconRelativePath = ConvertFullPathToRelativePathInsideSource(
                _state.SourceDirectory,
                TxtIconPath.Text.Trim(),
                "Icon File");
        }
        else
        {
            _state.IconRelativePath = string.Empty;
        }
    }

    private void UpdateWindowTitle()
    {
        var projectName = string.IsNullOrWhiteSpace(_currentProjectFilePath)
            ? "Untitled Project"
            : Path.GetFileName(_currentProjectFilePath);

        var dirtyMarker = _isDirty ? " *" : string.Empty;

        Title = $"InstallBunker Builder - {projectName}{dirtyMarker}";
    }

    private bool ValidateBundledModulesOnStartup()
    {
        var templatesRoot = CompilerPaths.GetBuilderTemplatesRootDirectory(AppContext.BaseDirectory);
        var setupStubDirectory = CompilerPaths.GetSetupTemplateDirectory(AppContext.BaseDirectory);
        var uninstallStubDirectory = CompilerPaths.GetUninstallTemplateDirectory(AppContext.BaseDirectory);
        var runtimeSupportRoot = CompilerPaths.GetRuntimeSupportRootDirectory(AppContext.BaseDirectory);

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
            templateDiagnostics.Add($"  • Templates root directory not found: {templatesRoot}");
        }

        if (!Directory.Exists(setupStubDirectory))
        {
            templateDiagnostics.Add($"  • Setup template directory not found: {setupStubDirectory}");
        }

        if (!Directory.Exists(uninstallStubDirectory))
        {
            templateDiagnostics.Add($"  • Uninstall template directory not found: {uninstallStubDirectory}");
        }

        if (!Directory.Exists(runtimeSupportRoot))
        {
            templateDiagnostics.Add($"  • RuntimeSupport directory not found: {runtimeSupportRoot}");
        }

        foreach (var requiredFilePath in requiredTemplateFiles)
        {
            if (!File.Exists(requiredFilePath))
            {
                templateDiagnostics.Add($"  • Required template file not found: {requiredFilePath}");
            }
        }

        if (templateDiagnostics.Count > 0)
        {
            System.Windows.MessageBox.Show(
                "InstallBunker Builder could not start because the internal compiler templates are missing or invalid." +
                Environment.NewLine + Environment.NewLine +
                "Template diagnostics:" + Environment.NewLine +
                string.Join(Environment.NewLine, templateDiagnostics) +
                Environment.NewLine + Environment.NewLine +
                "Expected editable structure:" + Environment.NewLine +
                "  • BuilderResources\\CompilerTemplates\\SetupStub\\App.xaml" + Environment.NewLine +
                "  • BuilderResources\\CompilerTemplates\\SetupStub\\MainWindow.xaml" + Environment.NewLine +
                "  • BuilderResources\\CompilerTemplates\\UninstallStub\\App.xaml" + Environment.NewLine +
                "  • BuilderResources\\CompilerTemplates\\UninstallStub\\MainWindow.xaml" + Environment.NewLine +
                "  • BuilderResources\\CompilerTemplates\\RuntimeSupport\\InstallBunker.Common" + Environment.NewLine +
                "  • BuilderResources\\CompilerTemplates\\RuntimeSupport\\InstallBunker.Domain" + Environment.NewLine +
                "  • BuilderResources\\CompilerTemplates\\RuntimeSupport\\InstallBunker.Installer.Core" + Environment.NewLine +
                "  • BuilderResources\\CompilerTemplates\\RuntimeSupport\\InstallBunker.Uninstaller.Core",
                "InstallBunker Builder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }

        var embeddedToolchain = InspectEmbeddedToolchain();

        if (!embeddedToolchain.RootDirectoryExists)
        {
            return true;
        }

        if (!embeddedToolchain.IsStructured)
        {
            var diagnostics = embeddedToolchain.MissingEntries.Count == 0
                ? "  • No embedded toolchain diagnostics available."
                : string.Join(Environment.NewLine, embeddedToolchain.MissingEntries.Select(p => $"  • {p}"));

            System.Windows.MessageBox.Show(
                "InstallBunker Builder detected an incomplete embedded toolchain layout." +
                Environment.NewLine + Environment.NewLine +
                "Embedded toolchain diagnostics:" + Environment.NewLine +
                diagnostics +
                Environment.NewLine + Environment.NewLine +
                "Expected real layout:" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\dotnet.exe" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\sdk" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\shared" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\packs" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\host" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\sdk-manifests" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\templates" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\LICENSE.txt" + Environment.NewLine +
                "  • BuilderResources\\EmbeddedToolchain\\ThirdPartyNotices.txt" +
                Environment.NewLine + Environment.NewLine +
                "The Builder can still open for now, but this folder structure must be completed before the embedded toolchain mode is fully enforced.",
                "InstallBunker Builder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        return true;
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        switch (_currentStep)
        {
            case BuilderStep.PackageInfo:
                SetStep(BuilderStep.Welcome);
                break;
            case BuilderStep.Files:
                SetStep(BuilderStep.PackageInfo);
                break;
            case BuilderStep.Options:
                SetStep(BuilderStep.Files);
                break;
            case BuilderStep.Build:
                SetStep(BuilderStep.Options);
                break;
        }
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        switch (_currentStep)
        {
            case BuilderStep.Welcome:
                SetStep(BuilderStep.PackageInfo);
                return;

            case BuilderStep.PackageInfo:
                if (!CapturePackageInfo())
                {
                    return;
                }

                SetStep(BuilderStep.Files);
                return;

            case BuilderStep.Files:
                if (!CaptureFilesAndAssets())
                {
                    return;
                }

                SetStep(BuilderStep.Options);
                return;

            case BuilderStep.Options:
                if (!CaptureOptions())
                {
                    return;
                }

                SetStep(BuilderStep.Build);
                return;

            case BuilderStep.Build:
                if ((BtnNext.Content?.ToString() ?? string.Empty).Equals("Finish", StringComparison.OrdinalIgnoreCase))
                {
                    Close();
                    return;
                }

                if (!EnsureProjectSavedBeforeBuild())
                {
                    return;
                }

                await BuildPackageAsync();
                return;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isBuilding)
        {
            return;
        }

        Close();
    }

    private void BtnBrowseOutputDirectory_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the output folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(TxtOutputDirectory.Text) && Directory.Exists(TxtOutputDirectory.Text))
        {
            dialog.InitialDirectory = TxtOutputDirectory.Text;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtOutputDirectory.Text = dialog.SelectedPath;
        }
    }

    private void BtnBrowseMainExecutable_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSourceDirectory.Text) || !Directory.Exists(TxtSourceDirectory.Text))
        {
            ShowValidation("Choose a valid Source Directory before selecting the Main Executable.");
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Choose the main executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Multiselect = false,
            InitialDirectory = TxtSourceDirectory.Text.Trim()
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtMainExecutable.Text = dialog.FileName;
        }
    }

    private void BtnBrowseIconFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtSourceDirectory.Text) || !Directory.Exists(TxtSourceDirectory.Text))
        {
            ShowValidation("Choose a valid Source Directory before selecting the Icon File.");
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Choose the icon file",
            Filter = "Icon files (*.ico)|*.ico|All files (*.*)|*.*",
            Multiselect = false,
            InitialDirectory = TxtSourceDirectory.Text.Trim()
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtIconPath.Text = dialog.FileName;
        }
    }

    private void BtnBrowseSourceDirectory_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the source application folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrWhiteSpace(TxtSourceDirectory.Text) && Directory.Exists(TxtSourceDirectory.Text))
        {
            dialog.InitialDirectory = TxtSourceDirectory.Text;
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtSourceDirectory.Text = dialog.SelectedPath;
            AutoFillMainExecutable(dialog.SelectedPath);
            UpdateFilesHint();
        }
    }

    private void BtnBrowseLicenseFile_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the license file",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtLicenseFile.Text = dialog.FileName;
        }
    }

    private void InstallOptionChanged(object sender, RoutedEventArgs e)
    {
        UpdateOptionsHint();
    }

    private bool CapturePackageInfo()
    {
        if (string.IsNullOrWhiteSpace(TxtAppName.Text))
        {
            ShowValidation("App Name is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtVersion.Text))
        {
            ShowValidation("Version is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtPublisher.Text))
        {
            ShowValidation("Publisher is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtOutputDirectory.Text))
        {
            ShowValidation("Output Directory is required.");
            return false;
        }

        _state.AppName = TxtAppName.Text.Trim();
        _state.Version = TxtVersion.Text.Trim();
        _state.Publisher = TxtPublisher.Text.Trim();
        _state.OutputDirectory = TxtOutputDirectory.Text.Trim();

        return true;
    }

    private bool CaptureFilesAndAssets()
    {
        if (string.IsNullOrWhiteSpace(TxtSourceDirectory.Text))
        {
            ShowValidation("Source Directory is required.");
            return false;
        }

        if (!Directory.Exists(TxtSourceDirectory.Text))
        {
            ShowValidation("Source Directory was not found.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtMainExecutable.Text))
        {
            ShowValidation("Main Executable is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtIconPath.Text))
        {
            ShowValidation("Icon File is required.");
            return false;
        }

        var sourceDirectory = TxtSourceDirectory.Text.Trim();
        var mainExeFullPath = TxtMainExecutable.Text.Trim();
        var iconFullPath = TxtIconPath.Text.Trim();

        if (!File.Exists(mainExeFullPath))
        {
            ShowValidation("The selected Main Executable full path was not found.");
            return false;
        }

        if (!File.Exists(iconFullPath))
        {
            ShowValidation("The selected Icon File full path was not found.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(iconFullPath), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            ShowValidation("The Icon File must be a .ico file so the Builder can embed the physical icon into Setup.exe and Uninstall.exe.");
            return false;
        }

        try
        {
            _state.MainExecutableRelativePath = ConvertFullPathToRelativePathInsideSource(
                sourceDirectory,
                mainExeFullPath,
                "Main Executable");

            _state.IconRelativePath = ConvertFullPathToRelativePathInsideSource(
                sourceDirectory,
                iconFullPath,
                "Icon File");
        }
        catch (Exception ex)
        {
            ShowValidation(ex.Message);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(TxtLicenseFile.Text) && !File.Exists(TxtLicenseFile.Text.Trim()))
        {
            ShowValidation("The selected License File was not found.");
            return false;
        }

        _state.SourceDirectory = sourceDirectory;
        _state.LicenseFilePath = TxtLicenseFile.Text.Trim();

        return true;
    }

    private bool CaptureOptions()
    {
        var allowPerUser = ChkAllowPerUser.IsChecked == true;
        var allowPerMachine = ChkAllowPerMachine.IsChecked == true;

        if (!allowPerUser && !allowPerMachine)
        {
            ShowValidation("At least one install scope must be allowed.");
            return false;
        }

        var defaultScope = CmbDefaultScope.SelectedIndex == 1
            ? InstallScope.PerMachine
            : InstallScope.PerUser;

        if (defaultScope == InstallScope.PerMachine && !allowPerMachine)
        {
            ShowValidation("Default scope cannot be Per-machine if Per-machine is not allowed.");
            return false;
        }

        if (defaultScope == InstallScope.PerUser && !allowPerUser)
        {
            ShowValidation("Default scope cannot be Per-user if Per-user is not allowed.");
            return false;
        }

        _state.AllowPerUser = allowPerUser;
        _state.AllowPerMachine = allowPerMachine;
        _state.DefaultInstallScope = defaultScope;
        _state.DesktopShortcut = ChkDesktopShortcut.IsChecked == true;
        _state.StartMenuShortcut = ChkStartMenuShortcut.IsChecked == true;
        _state.GenerateDiagnosticsFile = ChkGenerateDiagnosticsFile.IsChecked == true;

        return true;
    }

    private CompilerBrandingOptions CreateCompilerBranding()
    {
        return new CompilerBrandingOptions
        {
            AppName = _state.AppName,
            Publisher = _state.Publisher,
            Version = _state.Version,
            IconRelativePath = _state.IconRelativePath,
            SetupWindowTitle = $"{_state.AppName} Setup",
            SetupSidebarAppName = _state.AppName,
            SetupSidebarVersion = $"Version {_state.Version}",
            SetupWelcomeSummary =
                $"Application: {_state.AppName}{Environment.NewLine}" +
                $"Version: {_state.Version}{Environment.NewLine}" +
                $"Publisher: {_state.Publisher}{Environment.NewLine}" +
                $"Files will be installed from the generated package payload.",
            UninstallWindowTitle = $"{_state.AppName} Uninstall",
            UninstallSidebarAppName = _state.AppName,
            UninstallSidebarVersion = $"Version {_state.Version}",
            UninstallWelcomeSummary =
                $"Application: {_state.AppName}{Environment.NewLine}" +
                $"Version: {_state.Version}{Environment.NewLine}" +
                $"Publisher: {_state.Publisher}{Environment.NewLine}" +
                $"The uninstall flow will remove files tracked in install.receipt.json."
        };
    }

    private CompilerRequest CreateCompilerRequest()
    {
#if DEBUG
        const bool isDevelopmentMode = true;
#else
    const bool isDevelopmentMode = false;
#endif

        return new CompilerRequest
        {
            AppName = _state.AppName,
            Version = _state.Version,
            Publisher = _state.Publisher,
            SourceDirectory = _state.SourceDirectory,
            MainExecutableRelativePath = _state.MainExecutableRelativePath,
            IconRelativePath = _state.IconRelativePath,
            LicenseFilePath = string.IsNullOrWhiteSpace(_state.LicenseFilePath)
                ? null
                : _state.LicenseFilePath,
            AllowPerUser = _state.AllowPerUser,
            AllowPerMachine = _state.AllowPerMachine,
            DefaultInstallScope = _state.DefaultInstallScope,
            DesktopShortcut = _state.DesktopShortcut,
            StartMenuShortcut = _state.StartMenuShortcut,
            GenerateDiagnosticsFile = _state.GenerateDiagnosticsFile,
            Branding = CreateCompilerBranding(),
            Toolchain = new CompilerToolchainOptions
            {
                Configuration = "Release",
                RuntimeIdentifier = "win-x64",
                PublishSingleFile = true,
                SelfContained = true,
                IncludeNativeLibrariesForSelfExtract = true,
                PreferEmbeddedToolchain = true,
                IsDevelopmentMode = isDevelopmentMode,
                AllowSystemDotNetFallback = isDevelopmentMode,
                RequireEmbeddedToolchainInProductMode = true,
                KeepWorkspaceOnSuccess = false,
                KeepWorkspaceOnFailure = isDevelopmentMode
            },
            OutputDirectory = _state.OutputDirectory,
            PackagePassword = App.ModulePackagePassword
        };
    }

    private void AppendCompilationMessages(BuilderCompilationResult result)
    {
        foreach (var status in result.StatusMessages)
        {
            AppendBuildStatus(status);
        }

        foreach (var log in result.LogMessages)
        {
            AppendBuildLog(log);
        }
    }

    private string CreateBuildSummary(CompilerResult result)
    {
        return
            $"Application: {_state.AppName}{Environment.NewLine}" +
            $"Version: {_state.Version}{Environment.NewLine}" +
            $"Publisher: {_state.Publisher}{Environment.NewLine}" +
            $"Source: {_state.SourceDirectory}{Environment.NewLine}" +
            $"Output: {result.OutputDirectory}{Environment.NewLine}" +
            $"Setup: {result.SetupFilePath}{Environment.NewLine}" +
            $"Package: {result.PackageFilePath}{Environment.NewLine}" +
            $"Files copied: {result.FilesCopiedCount}{Environment.NewLine}" +
            $"System fallback used: {(result.UsedSystemDotNetFallback ? "Yes" : "No")}{Environment.NewLine}" +
            $"Diagnostics file: {(string.IsNullOrWhiteSpace(result.BuildLogFilePath) ? "Disabled / not generated" : result.BuildLogFilePath)}{Environment.NewLine}" +
            $"Workspace cleanup: {(result.WorkspaceDeleted ? "Deleted automatically" : "Retained")}" +
            (string.IsNullOrWhiteSpace(result.WorkspaceRetentionReason)
                ? string.Empty
                : $"{Environment.NewLine}Workspace note: {result.WorkspaceRetentionReason}");
    }

    private void HandleCompilerTelemetry(CompilerTelemetryEvent telemetry)
    {
        if (telemetry.ProgressPercent.HasValue)
        {
            BuildProgressBar.Value = Math.Max(0, Math.Min(100, telemetry.ProgressPercent.Value));
        }

        if (!string.IsNullOrWhiteSpace(telemetry.StageDisplayName))
        {
            TxtFooterHint.Text = telemetry.StageDisplayName;
        }

        if (telemetry.IsStatus && !string.IsNullOrWhiteSpace(telemetry.Message))
        {
            AppendBuildStatus(telemetry.Message);
        }

        if (telemetry.IsLog && !string.IsNullOrWhiteSpace(telemetry.Message))
        {
            AppendBuildLog(telemetry.Message);
        }
    }

    private async Task BuildPackageAsync()
    {
        _isBuilding = true;
        EnterBuildingMode();

        BtnNext.IsEnabled = false;
        BtnBack.IsEnabled = false;
        BtnCancel.IsEnabled = false;

        TxtBuildLog.Clear();
        TxtBuildStatus.Clear();
        TxtBuildSummary.Text = "Building your wizard...";
        BuildProgressBar.Value = 0;

        try
        {
            var compilerRequest = CreateCompilerRequest();
            var progress = new Progress<CompilerTelemetryEvent>(HandleCompilerTelemetry);

            AppendBuildStatus("Starting package build...");
            AppendBuildLog("CompilerRequest created successfully.");
            BuildProgressBar.Value = 1;
            TxtFooterHint.Text = "Starting build...";

            var compilationResult = await _builderCompilationService.CompileAsync(
                compilerRequest,
                AppContext.BaseDirectory,
                progress);

            var result = compilationResult.CompilerResult;

            BuildProgressBar.Value = 100;
            TxtBuildSummary.Text = CreateBuildSummary(result);

            EnterBuildFinishedMode();
            TxtBuildStatus.Visibility = Visibility.Collapsed;
            txtFinishBuildingStatus.Visibility = Visibility.Visible;

            BtnNext.Content = "Finish";
            TxtFooterHint.Text = "Package created successfully.";
        }
        catch (Exception ex)
        {
            BuildProgressBar.Value = 0;

            EnterBuildFinishedMode();

            AppendBuildStatus("Build failed.");
            AppendBuildStatus(ex.Message);

            AppendBuildLog("Build failed.");
            AppendBuildLog(ex.Message);

            TxtFooterHint.Text = "Build failed.";

            System.Windows.MessageBox.Show(
                ex.Message,
                "InstallBunker Builder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isBuilding = false;
            BtnNext.IsEnabled = true;
            BtnBack.IsEnabled = true;
            BtnCancel.IsEnabled = true;

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private void AutoFillMainExecutable(string sourceDirectory)
    {
        try
        {
            var exeFiles = Directory
                .GetFiles(sourceDirectory, "*.exe", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (exeFiles.Count == 0)
            {
                return;
            }

            var preferred = exeFiles.FirstOrDefault(x =>
                !Path.GetFileName(x).Contains("setup", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(x).Contains("uninstall", StringComparison.OrdinalIgnoreCase));

            var selected = preferred ?? exeFiles[0];

            if (string.IsNullOrWhiteSpace(TxtMainExecutable.Text))
            {
                TxtMainExecutable.Text = selected;
            }
        }
        catch
        {
        }
    }

    private void UpdateFilesHint()
    {
        TxtFilesHint.Text =
            "Tip: choose the compiled application folder first. The Builder can auto-detect the main executable, while the icon and license file can be selected manually.";
    }

    private void UpdateOptionsHint()
    {
        var allowPerUser = ChkAllowPerUser?.IsChecked == true;
        var allowPerMachine = ChkAllowPerMachine?.IsChecked == true;

        TxtOptionsHint.Text =
            $"Per-user allowed: {(allowPerUser ? "Yes" : "No")}{Environment.NewLine}" +
            $"Per-machine allowed: {(allowPerMachine ? "Yes" : "No")}";
    }

    private void SetStep(BuilderStep step)
    {
        _currentStep = step;

        StepWelcomePanel.Visibility = step == BuilderStep.Welcome ? Visibility.Visible : Visibility.Collapsed;
        StepPackageInfoPanel.Visibility = step == BuilderStep.PackageInfo ? Visibility.Visible : Visibility.Collapsed;
        StepFilesPanel.Visibility = step == BuilderStep.Files ? Visibility.Visible : Visibility.Collapsed;
        StepOptionsPanel.Visibility = step == BuilderStep.Options ? Visibility.Visible : Visibility.Collapsed;
        StepBuildPanel.Visibility = step == BuilderStep.Build ? Visibility.Visible : Visibility.Collapsed;

        BtnBack.IsEnabled = step is BuilderStep.PackageInfo or BuilderStep.Files or BuilderStep.Options or BuilderStep.Build;

        switch (step)
        {
            case BuilderStep.Welcome:
                BtnNext.Content = "Next >";
                TxtFooterHint.Text = "Click Next to start configuring the package.";
                break;

            case BuilderStep.PackageInfo:
                BtnNext.Content = "Next >";
                TxtFooterHint.Text = "Fill in the application identity and output folder.";
                break;

            case BuilderStep.Files:
                BtnNext.Content = "Next >";
                TxtFooterHint.Text = "Select the source files and package assets.";
                break;

            case BuilderStep.Options:
                BtnNext.Content = "Next >";
                TxtFooterHint.Text = "Choose installation scope and default shortcuts.";
                break;

            case BuilderStep.Build:
                BtnNext.Content = "Build Package";
                TxtFooterHint.Text = "Review and generate the package.";
                TxtBuildSummary.Text =
                    $"Application: {TxtAppName.Text}{Environment.NewLine}" +
                    $"Version: {TxtVersion.Text}{Environment.NewLine}" +
                    $"Publisher: {TxtPublisher.Text}{Environment.NewLine}" +
                    $"Source: {TxtSourceDirectory.Text}{Environment.NewLine}" +
                    $"Main EXE: {TxtMainExecutable.Text}{Environment.NewLine}" +
                    $"Icon: {TxtIconPath.Text}{Environment.NewLine}" +
                    $"License: {(string.IsNullOrWhiteSpace(TxtLicenseFile.Text) ? "None" : TxtLicenseFile.Text)}{Environment.NewLine}" +
                    $"Output: {TxtOutputDirectory.Text}";

                EnterPreBuildMode();
                break;
        }
    }

    private void AppendBuildStatus(string message)
    {
        if (!string.IsNullOrWhiteSpace(TxtBuildStatus.Text))
        {
            TxtBuildStatus.AppendText(Environment.NewLine);
        }

        TxtBuildStatus.AppendText(message);
        TxtBuildStatus.ScrollToEnd();
    }

    private void AppendBuildLog(string message)
    {
        if (!string.IsNullOrWhiteSpace(TxtBuildStatus.Text))
        {
            TxtBuildStatus.AppendText(Environment.NewLine);
        }

        TxtBuildStatus.AppendText($"• {message}");
        TxtBuildStatus.ScrollToEnd();
    }

    private static void ShowValidation(string message)
    {
        System.Windows.MessageBox.Show(
            message,
            "InstallBunker Builder",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void EnterPreBuildMode()
    {
        if (BuildSummaryCard != null)
            BuildSummaryCard.Visibility = Visibility.Visible;

        TxtBuildStatus.Height = 97;
        TxtBuildStatus.VerticalAlignment = VerticalAlignment.Top;
    }

    private void EnterBuildingMode()
    {
        if (BuildSummaryCard != null)
            BuildSummaryCard.Visibility = Visibility.Visible;

        TxtBuildStatus.Height = double.NaN; // Auto
        TxtBuildStatus.VerticalAlignment = VerticalAlignment.Stretch;
    }

    private void EnterBuildFinishedMode()
    {
        if (BuildSummaryCard != null)
            BuildSummaryCard.Visibility = Visibility.Visible;

        TxtBuildStatus.Height = 97;
        TxtBuildStatus.VerticalAlignment = VerticalAlignment.Top;
    }
}

public enum BuilderStep
{
    Welcome = 0,
    PackageInfo = 1,
    Files = 2,
    Options = 3,
    Build = 4
}