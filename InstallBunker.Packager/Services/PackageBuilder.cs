using InstallBunker.Common.Serialization;
using InstallBunker.Domain.Models;
using InstallBunker.Packager.Models;

namespace InstallBunker.Packager.Services;

public sealed class PackageBuilder
{

    public BuildPackageResult Build(BuildPackageRequest request)
    {
        ValidateRequest(request);

        var sourceDirectory = Path.GetFullPath(request.SourceDirectory);
        var outputDirectory = Path.GetFullPath(request.OutputDirectory);

        var setupFilePath = Path.Combine(outputDirectory, "Setup.exe");
        var packageFilePath = Path.Combine(outputDirectory, "Package.pkg");

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);

        // Setup.exe must remain a standalone bootstrapper beside Package.pkg
        File.Copy(request.SetupExecutablePath, setupFilePath, overwrite: true);

        // Build the manifest in-memory (it will be stored inside Package.pkg)
        var files = Directory
            .GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var manifestEntries = new List<PackageFileEntry>();

        foreach (var sourceFilePath in files)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);

            manifestEntries.Add(new PackageFileEntry
            {
                Source = relativePath,
                RelativeTarget = relativePath,
                Overwrite = true
            });
        }

        var uiOptions = new InstallerUiOptions
        {
            ShowLicensePage = false,
            LicenseFile = string.Empty,
            AllowLaunchAfterInstall = true
        };

        var licenseCopied = false;
        string? licenseFileName = null;
        byte[]? licenseBytes = null;

        if (!string.IsNullOrWhiteSpace(request.LicenseFilePath))
        {
            var fullLicensePath = Path.GetFullPath(request.LicenseFilePath);

            if (!File.Exists(fullLicensePath))
            {
                throw new FileNotFoundException("License file not found.", fullLicensePath);
            }

            licenseFileName = Path.GetFileName(fullLicensePath);
            licenseBytes = File.ReadAllBytes(fullLicensePath);

            uiOptions.ShowLicensePage = true;
            uiOptions.LicenseFile = licenseFileName;
            licenseCopied = true;
        }

        var manifest = new PackageManifest
        {
            SchemaVersion = 1,
            PackageInfo = new PackageInfo
            {
                AppName = request.AppName,
                Version = request.Version,
                Publisher = request.Publisher,
                MainExecutable = request.MainExecutableRelativePath,
                IconPath = request.IconRelativePath
            },
            InstallSettings = new InstallSettings
            {
                DefaultInstallScope = request.DefaultInstallScope,
                AllowPerUser = request.AllowPerUser,
                AllowPerMachine = request.AllowPerMachine,
                DefaultInstallDirPerUser = $@"%LocalAppData%\Programs\{request.AppName}",
                DefaultInstallDirPerMachine = $@"%ProgramFiles%\{request.AppName}"
            },
            Shortcuts = new ShortcutOptions
            {
                Desktop = request.DesktopShortcut,
                StartMenu = request.StartMenuShortcut
            },
            Ui = uiOptions,
            Branding = request.Branding,
            Files = manifestEntries
        };

        var manifestErrors = manifest.Validate();

        if (manifestErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Generated manifest is invalid: " + string.Join(" | ", manifestErrors));
        }

        // Create Package.pkg (encrypted+authenticated container)
        InstallBunker.Common.PackageContainer.PackageContainerBuilder.BuildFromFileSystem(
            packageFilePath,
            request.PackagePassword,
            zip =>
            {
                // Root entries: manifest.json, Uninstall.exe, (optional) license file
                AddBytes(zip, "manifest.json", InstallBunker.Common.Serialization.InstallBunkerJson.Serialize(manifest));

                AddFile(zip, "Uninstall.exe", request.UninstallerExecutablePath);

                if (licenseCopied && licenseBytes is not null && !string.IsNullOrWhiteSpace(licenseFileName))
                {
                    AddBytes(zip, licenseFileName!, licenseBytes);
                }

                // Payload files under payload/
                foreach (var sourceFilePath in files)
                {
                    var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath)
                        .Replace('\\', '/');

                    AddFile(zip, $"payload/{relativePath}", sourceFilePath);
                }
            });

        return new BuildPackageResult
        {
            OutputDirectory = outputDirectory,
            SetupFilePath = setupFilePath,
            PackageFilePath = packageFilePath,
            UninstallerFilePath = string.Empty,
            ManifestFilePath = string.Empty,
            PayloadDirectory = string.Empty,
            FilesCopiedCount = manifestEntries.Count,
            LicenseCopied = licenseCopied
        };
    }

    private static void AddFile(System.IO.Compression.ZipArchive zip, string entryName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Package file path is required.");
        }

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Package file not found.", fullPath);
        }

        var entry = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fileStream.CopyTo(entryStream);
    }

    private static void AddBytes(System.IO.Compression.ZipArchive zip, string entryName, string content)
    {
        AddBytes(zip, entryName, System.Text.Encoding.UTF8.GetBytes(content));
    }

    private static void AddBytes(System.IO.Compression.ZipArchive zip, string entryName, byte[] bytes)
    {
        var entry = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(bytes, 0, bytes.Length);
    }

    private static void ValidateRequest(BuildPackageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AppName))
        {
            throw new InvalidOperationException("AppName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            throw new InvalidOperationException("Version is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Publisher))
        {
            throw new InvalidOperationException("Publisher is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceDirectory))
        {
            throw new InvalidOperationException("SourceDirectory is required.");
        }

        if (!Directory.Exists(request.SourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {request.SourceDirectory}");
        }

        if (string.IsNullOrWhiteSpace(request.MainExecutableRelativePath))
        {
            throw new InvalidOperationException("MainExecutableRelativePath is required.");
        }

        var mainExecutableFullPath = Path.Combine(
            request.SourceDirectory,
            request.MainExecutableRelativePath);

        if (!File.Exists(mainExecutableFullPath))
        {
            throw new FileNotFoundException(
                "Main executable not found inside source directory.",
                mainExecutableFullPath);
        }

        if (string.IsNullOrWhiteSpace(request.IconRelativePath))
        {
            throw new InvalidOperationException("IconRelativePath is required.");
        }

        var iconFullPath = Path.Combine(request.SourceDirectory, request.IconRelativePath);

        if (!File.Exists(iconFullPath))
        {
            throw new FileNotFoundException(
                "Icon file not found inside source directory.",
                iconFullPath);
        }

        if (!string.Equals(Path.GetExtension(iconFullPath), ".ico", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "To generate Setup.exe and Uninstall.exe with a physical embedded icon, the selected Icon File must be a .ico file.");
        }

        if (!request.AllowPerUser && !request.AllowPerMachine)
        {
            throw new InvalidOperationException("At least one install scope must be allowed.");
        }

        if (request.Branding is null)
        {
            throw new InvalidOperationException("Branding is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.SetupWindowTitle))
        {
            throw new InvalidOperationException("Branding.SetupWindowTitle is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.SetupSidebarAppName))
        {
            throw new InvalidOperationException("Branding.SetupSidebarAppName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.SetupSidebarVersion))
        {
            throw new InvalidOperationException("Branding.SetupSidebarVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.UninstallWindowTitle))
        {
            throw new InvalidOperationException("Branding.UninstallWindowTitle is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.UninstallSidebarAppName))
        {
            throw new InvalidOperationException("Branding.UninstallSidebarAppName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Branding.UninstallSidebarVersion))
        {
            throw new InvalidOperationException("Branding.UninstallSidebarVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            throw new InvalidOperationException("OutputDirectory is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SetupExecutablePath))
        {
            throw new InvalidOperationException("SetupExecutablePath is required.");
        }

        if (!File.Exists(request.SetupExecutablePath))
        {
            throw new FileNotFoundException(
                "Setup executable not found. The compiler could not resolve a valid setup bootstrapper executable.",
                request.SetupExecutablePath);
        }

        if (string.IsNullOrWhiteSpace(request.UninstallerExecutablePath))
        {
            throw new InvalidOperationException("UninstallerExecutablePath is required.");
        }

        if (!File.Exists(request.UninstallerExecutablePath))
        {
            throw new FileNotFoundException(
                "Uninstaller executable not found. The compiler could not resolve a valid uninstaller bootstrapper executable.",
                request.UninstallerExecutablePath);
        }

        if (string.IsNullOrWhiteSpace(request.PackagePassword))
        {
            throw new InvalidOperationException("PackagePassword is required.");
        }
    }
}