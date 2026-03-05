using InstallBunker.Compiler.Core.Models;
using InstallBunker.Compiler.Core.Paths;

namespace InstallBunker.Compiler.Core.Services;

public sealed class EmbeddedToolchainLayoutService
{
    public EmbeddedToolchainLayoutInfo Inspect(string builderBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(builderBaseDirectory))
        {
            throw new InvalidOperationException("Builder base directory is required.");
        }

        var info = new EmbeddedToolchainLayoutInfo
        {
            BuilderBaseDirectory = builderBaseDirectory,
            RootDirectory = CompilerPaths.GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
            DotNetDirectory = CompilerPaths.GetEmbeddedToolchainDotNetDirectory(builderBaseDirectory),
            DotNetHostPath = CompilerPaths.GetEmbeddedToolchainDotNetHostPath(builderBaseDirectory),
            SdkDirectory = CompilerPaths.GetEmbeddedToolchainSdkDirectory(builderBaseDirectory),
            PacksDirectory = CompilerPaths.GetEmbeddedToolchainPacksDirectory(builderBaseDirectory),
            SharedDirectory = CompilerPaths.GetEmbeddedToolchainSharedDirectory(builderBaseDirectory),
            HostDirectory = Path.Combine(
                CompilerPaths.GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
                "host"),
            SdkManifestsDirectory = Path.Combine(
                CompilerPaths.GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
                "sdk-manifests"),
            TemplatesDirectory = Path.Combine(
                CompilerPaths.GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
                "templates"),
            LicenseFilePath = Path.Combine(
                CompilerPaths.GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
                "LICENSE.txt"),
            ThirdPartyNoticesFilePath = Path.Combine(
                CompilerPaths.GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
                "ThirdPartyNotices.txt")
        };

        info.RootDirectoryExists = Directory.Exists(info.RootDirectory);
        info.DotNetDirectoryExists = Directory.Exists(info.DotNetDirectory);
        info.DotNetHostExists = File.Exists(info.DotNetHostPath);
        info.SdkDirectoryExists = Directory.Exists(info.SdkDirectory);
        info.PacksDirectoryExists = Directory.Exists(info.PacksDirectory);
        info.SharedDirectoryExists = Directory.Exists(info.SharedDirectory);
        info.HostDirectoryExists = Directory.Exists(info.HostDirectory);
        info.SdkManifestsDirectoryExists = Directory.Exists(info.SdkManifestsDirectory);
        info.TemplatesDirectoryExists = Directory.Exists(info.TemplatesDirectory);
        info.LicenseFileExists = File.Exists(info.LicenseFilePath);
        info.ThirdPartyNoticesFileExists = File.Exists(info.ThirdPartyNoticesFilePath);

        if (info.SdkDirectoryExists)
        {
            var sdkCandidates = Directory
                .GetDirectories(info.SdkDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(path => new
                {
                    Path = path,
                    Name = Path.GetFileName(path),
                    Version = TryParseVersion(Path.GetFileName(path))
                })
                .Where(item => item.Version is not null)
                .OrderByDescending(item => item.Version)
                .ToList();

            var selected = sdkCandidates.FirstOrDefault();

            if (selected is not null)
            {
                info.SelectedSdkVersion = selected.Name;
                info.SelectedSdkDirectory = selected.Path;
                info.SelectedSdkSdksDirectory = Path.Combine(selected.Path, "Sdks");
            }
        }

        info.SelectedSdkDirectoryExists =
            !string.IsNullOrWhiteSpace(info.SelectedSdkDirectory) &&
            Directory.Exists(info.SelectedSdkDirectory);

        info.SelectedSdkSdksDirectoryExists =
            !string.IsNullOrWhiteSpace(info.SelectedSdkSdksDirectory) &&
            Directory.Exists(info.SelectedSdkSdksDirectory);

        if (!info.RootDirectoryExists)
        {
            info.MissingEntries.Add($"Embedded toolchain root directory not found: {info.RootDirectory}");
        }

        if (!info.DotNetDirectoryExists)
        {
            info.MissingEntries.Add($"Embedded toolchain dotnet root directory not found: {info.DotNetDirectory}");
        }

        if (!info.DotNetHostExists)
        {
            info.MissingEntries.Add($"Embedded toolchain host executable not found: {info.DotNetHostPath}");
        }

        if (!info.SdkDirectoryExists)
        {
            info.MissingEntries.Add($"Embedded toolchain sdk directory not found: {info.SdkDirectory}");
        }

        if (!info.PacksDirectoryExists)
        {
            info.MissingEntries.Add($"Embedded toolchain packs directory not found: {info.PacksDirectory}");
        }

        if (!info.SharedDirectoryExists)
        {
            info.MissingEntries.Add($"Embedded toolchain shared directory not found: {info.SharedDirectory}");
        }

        if (!info.HostDirectoryExists)
        {
            info.MissingEntries.Add($"Embedded toolchain host directory not found: {info.HostDirectory}");
        }

        if (!info.SdkManifestsDirectoryExists)
        {
            info.MissingEntries.Add($"Embedded toolchain sdk-manifests directory not found: {info.SdkManifestsDirectory}");
        }

        if (!info.TemplatesDirectoryExists)
        {
            info.MissingEntries.Add($"Embedded toolchain templates directory not found: {info.TemplatesDirectory}");
        }

        if (!info.LicenseFileExists)
        {
            info.MissingEntries.Add($"Embedded toolchain license file not found: {info.LicenseFilePath}");
        }

        if (!info.ThirdPartyNoticesFileExists)
        {
            info.MissingEntries.Add($"Embedded toolchain ThirdPartyNotices file not found: {info.ThirdPartyNoticesFilePath}");
        }

        if (!info.SelectedSdkDirectoryExists)
        {
            info.MissingEntries.Add(
                $"Embedded toolchain could not resolve a concrete SDK version directory inside: {info.SdkDirectory}");
        }

        if (!info.SelectedSdkSdksDirectoryExists)
        {
            info.MissingEntries.Add(
                $"Embedded toolchain could not resolve the Sdks directory for the selected SDK: {info.SelectedSdkSdksDirectory}");
        }

        return info;
    }

    private static Version? TryParseVersion(string value)
    {
        return Version.TryParse(value, out var version)
            ? version
            : null;
    }
}