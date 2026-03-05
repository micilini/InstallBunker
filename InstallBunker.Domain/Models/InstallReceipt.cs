using InstallBunker.Domain.Enums;

namespace InstallBunker.Domain.Models;

public sealed class InstallReceipt
{
    public string InstallId { get; set; } = Guid.NewGuid().ToString("D");

    public string AppName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public InstallScope InstallScope { get; set; } = InstallScope.PerUser;

    public string InstallDirectory { get; set; } = string.Empty;

    public PackageBrandingOptions Branding { get; set; } = new();

    public List<InstalledFileRecord> InstalledFiles { get; set; } = new();

    public List<InstalledShortcutRecord> CreatedShortcuts { get; set; } = new();

    public List<InstalledRegistryRecord> RegistryKeys { get; set; } = new();

    public DateTime InstalledAtUtc { get; set; } = DateTime.UtcNow;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(InstallId))
        {
            errors.Add("InstallId is required.");
        }

        if (string.IsNullOrWhiteSpace(AppName))
        {
            errors.Add("AppName is required.");
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            errors.Add("Version is required.");
        }

        if (string.IsNullOrWhiteSpace(InstallDirectory))
        {
            errors.Add("InstallDirectory is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.SetupWindowTitle))
        {
            errors.Add("Branding.SetupWindowTitle is required.");
        }

        if (string.IsNullOrWhiteSpace(Branding.UninstallWindowTitle))
        {
            errors.Add("Branding.UninstallWindowTitle is required.");
        }

        return errors;
    }
}