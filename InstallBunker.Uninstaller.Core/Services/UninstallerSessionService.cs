using InstallBunker.Common.Serialization;
using InstallBunker.Uninstaller.Core.Models;

namespace InstallBunker.Uninstaller.Core.Services;

public sealed class UninstallerSessionService
{
    public UninstallerSessionInfo Load(string? receiptPathOverride = null, string? currentExecutablePath = null)
    {
        var resolvedExecutablePath = currentExecutablePath
            ?? Environment.ProcessPath
            ?? AppContext.BaseDirectory;

        var resolvedReceiptPath = ResolveReceiptPath(receiptPathOverride);

        if (!File.Exists(resolvedReceiptPath))
        {
            throw new FileNotFoundException(
                "install.receipt.json was not found for this uninstall session.",
                resolvedReceiptPath);
        }

        var receipt = JsonFileStore.Load<Domain.Models.InstallReceipt>(resolvedReceiptPath);
        var errors = receipt.Validate();

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The install receipt is invalid:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors));
        }

        return new UninstallerSessionInfo
        {
            Receipt = receipt,
            ReceiptFilePath = resolvedReceiptPath,
            CurrentExecutablePath = resolvedExecutablePath,
            WindowTitle = receipt.Branding.UninstallWindowTitle,
            SidebarAppName = receipt.Branding.UninstallSidebarAppName,
            SidebarVersion = receipt.Branding.UninstallSidebarVersion,
            WelcomeSummary = receipt.Branding.UninstallWelcomeSummary
        };
    }

    public string BuildCompletedSummary(UninstallerSessionInfo session, UninstallResult result)
    {
        if (session is null)
        {
            throw new InvalidOperationException("UninstallerSessionInfo is required.");
        }

        if (result is null)
        {
            throw new InvalidOperationException("UninstallResult is required.");
        }

        return
            $"Application: {session.Receipt.AppName}{Environment.NewLine}" +
            $"Install folder: {session.Receipt.InstallDirectory}{Environment.NewLine}" +
            $"Files removed: {result.RemovedFilesCount}{Environment.NewLine}" +
            $"Shortcuts removed: {result.RemovedShortcutsCount}{Environment.NewLine}" +
            $"Registry keys removed: {result.RemovedRegistryKeysCount}{Environment.NewLine}" +
            $"File removal failures: {result.FailedFileRemovalsCount}{Environment.NewLine}" +
            $"Shortcut removal failures: {result.FailedShortcutRemovalsCount}{Environment.NewLine}" +
            $"Registry removal failures: {result.FailedRegistryRemovalsCount}";
    }

    private static string ResolveReceiptPath(string? receiptPathOverride)
    {
        if (!string.IsNullOrWhiteSpace(receiptPathOverride))
        {
            return Path.GetFullPath(receiptPathOverride);
        }

        var commandLineArgs = Environment.GetCommandLineArgs();

        if (commandLineArgs.Length > 1 && File.Exists(commandLineArgs[1]))
        {
            return Path.GetFullPath(commandLineArgs[1]);
        }

        return Path.Combine(AppContext.BaseDirectory, "install.receipt.json");
    }
}