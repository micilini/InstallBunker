# Compiler Templates

This folder now represents a visible and editable part of the InstallBunker product.

## Structure

- SetupStub/
  - InstallBunker.Installer.UI.csproj
  - App.xaml
  - App.xaml.cs
  - MainWindow.xaml
  - MainWindow.xaml.cs
  - AssemblyInfo.cs

- UninstallStub/
  - InstallBunker.Uninstaller.UI.csproj
  - App.xaml
  - App.xaml.cs
  - MainWindow.xaml
  - MainWindow.xaml.cs
  - AssemblyInfo.cs

- RuntimeSupport/
  - InstallBunker.Common/
  - InstallBunker.Domain/
  - InstallBunker.Installer.Core/
  - InstallBunker.Uninstaller.Core/

## Goal

Allow a developer to inspect and edit the WPF templates that the compiler materializes into the temporary workspace before publish.

## Important

The Setup and Uninstall templates are the visual shell.
The critical runtime logic should continue to live in RuntimeSupport projects.

## Status

- FASE 9.4: editable WPF templates become real files inside BuilderResources
- FASE 9.5: runtime logic is kept in stable support libraries
- FASE 9.6+: compiler copies everything into a temporary workspace before publish