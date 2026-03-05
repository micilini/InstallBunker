using System.Text.Json.Serialization;

namespace InstallBunker.Common.PackageContainer;

/// <summary>
/// Header stored in cleartext at the beginning of Package.pkg.
/// Ciphertext contains a ZIP payload (manifest.json, Uninstall.exe, payload/*, optional license file).
/// </summary>
public sealed class PackageContainerHeader
{
    public int FormatVersion { get; set; } = PackageContainerFormat.CurrentVersion;

    public string InnerFormat { get; set; } = "zip";

    public string CryptoAlgorithm { get; set; } = "AES-GCM";

    public string KdfAlgorithm { get; set; } = "PBKDF2-SHA256";

    public int KdfIterations { get; set; } = 100_000;

    public string SaltBase64 { get; set; } = string.Empty;

    public string NonceBase64 { get; set; } = string.Empty;

    public string TagBase64 { get; set; } = string.Empty;

    public string InnerSha256Base64 { get; set; } = string.Empty;

    public long InnerSizeBytes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}