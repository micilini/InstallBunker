using InstallBunker.Common.Paths;

namespace InstallBunker.Compiler.Core.Paths;

public static class CompilerPaths
{
    public static string GetCompilerRootDirectory()
    {
        return Path.Combine(
            InstallBunkerPaths.GetBuilderDataDirectory(),
            "Compiler");
    }

    public static string GetCompilerTempDirectory()
    {
        return Path.Combine(
            GetCompilerRootDirectory(),
            "Temp");
    }

    public static string CreateCompilationWorkspace()
    {
        var workspacePath = Path.Combine(
            GetCompilerTempDirectory(),
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(workspacePath);
        return workspacePath;
    }

    public static string GetBuilderResourcesRootDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            builderBaseDirectory,
            "BuilderResources");
    }

    public static string GetBuilderTemplatesRootDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetBuilderResourcesRootDirectory(builderBaseDirectory),
            "CompilerTemplates");
    }

    public static string GetSetupTemplateDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetBuilderTemplatesRootDirectory(builderBaseDirectory),
            "SetupStub");
    }

    public static string GetUninstallTemplateDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetBuilderTemplatesRootDirectory(builderBaseDirectory),
            "UninstallStub");
    }

    public static string GetRuntimeSupportRootDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetBuilderTemplatesRootDirectory(builderBaseDirectory),
            "RuntimeSupport");
    }

    public static string GetRuntimeSupportCommonDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetRuntimeSupportRootDirectory(builderBaseDirectory),
            "InstallBunker.Common");
    }

    public static string GetRuntimeSupportDomainDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetRuntimeSupportRootDirectory(builderBaseDirectory),
            "InstallBunker.Domain");
    }

    public static string GetRuntimeSupportInstallerCoreDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetRuntimeSupportRootDirectory(builderBaseDirectory),
            "InstallBunker.Installer.Core");
    }

    public static string GetRuntimeSupportUninstallerCoreDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetRuntimeSupportRootDirectory(builderBaseDirectory),
            "InstallBunker.Uninstaller.Core");
    }

    public static string GetSetupTemplateExecutablePath(string builderBaseDirectory)
    {
        return Path.Combine(
            GetSetupTemplateDirectory(builderBaseDirectory),
            "Setup.exe");
    }

    public static string GetUninstallTemplateExecutablePath(string builderBaseDirectory)
    {
        return Path.Combine(
            GetUninstallTemplateDirectory(builderBaseDirectory),
            "Uninstall.exe");
    }

    public static string GetEmbeddedToolchainRootDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetBuilderResourcesRootDirectory(builderBaseDirectory),
            "EmbeddedToolchain");
    }

    public static string GetEmbeddedToolchainDotNetDirectory(string builderBaseDirectory)
    {
        return GetEmbeddedToolchainRootDirectory(builderBaseDirectory);
    }

    public static string GetEmbeddedToolchainDotNetHostPath(string builderBaseDirectory)
    {
        return Path.Combine(
            GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
            "dotnet.exe");
    }

    public static string GetEmbeddedToolchainSdkDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
            "sdk");
    }

    public static string GetEmbeddedToolchainPacksDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
            "packs");
    }

    public static string GetEmbeddedToolchainSharedDirectory(string builderBaseDirectory)
    {
        return Path.Combine(
            GetEmbeddedToolchainRootDirectory(builderBaseDirectory),
            "shared");
    }
}