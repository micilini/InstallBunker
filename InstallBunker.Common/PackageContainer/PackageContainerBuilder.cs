using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InstallBunker.Common.PackageContainer;

public static class PackageContainerBuilder
{
    public static void BuildFromFileSystem(
        string outputPkgPath,
        string password,
        Action<ZipArchive> writeZipEntries)
    {
        if (string.IsNullOrWhiteSpace(outputPkgPath))
            throw new InvalidOperationException("Output package path is required.");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Package password is required.");

        if (writeZipEntries is null)
            throw new InvalidOperationException("ZIP writer callback is required.");

        // 1) Build inner ZIP in memory
        byte[] zipBytes;
        using (var zipMs = new MemoryStream())
        {
            using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
            {
                writeZipEntries(zip);
            }

            zipBytes = zipMs.ToArray();
        }

        // 2) Compute integrity hash
        var sha256 = SHA256.HashData(zipBytes);
        var innerShaB64 = Convert.ToBase64String(sha256);

        // 3) Create salt + nonce, derive key via PBKDF2
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12); // recommended size for AES-GCM
        var key = DeriveKey(password, salt, iterations: 100_000, keyBytes: 32);

        // 4) Encrypt ZIP bytes using AES-GCM
        var ciphertext = new byte[zipBytes.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(key, tagSizeInBytes: tag.Length))
        {
            aes.Encrypt(nonce, zipBytes, ciphertext, tag);
        }

        var header = new PackageContainerHeader
        {
            FormatVersion = PackageContainerFormat.CurrentVersion,
            InnerFormat = "zip",
            CryptoAlgorithm = "AES-GCM",
            KdfAlgorithm = "PBKDF2-SHA256",
            KdfIterations = 100_000,
            SaltBase64 = Convert.ToBase64String(salt),
            NonceBase64 = Convert.ToBase64String(nonce),
            TagBase64 = Convert.ToBase64String(tag),
            InnerSha256Base64 = innerShaB64,
            InnerSizeBytes = zipBytes.LongLength,
            CreatedUtc = DateTime.UtcNow
        };

        var headerJson = JsonSerializer.Serialize(header, new JsonSerializerOptions { WriteIndented = false });
        var headerBytes = Encoding.UTF8.GetBytes(headerJson);

        // 5) Write file format:
        // [8 bytes magic][4 bytes header length][header json][ciphertext]
        using var fs = new FileStream(outputPkgPath, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(PackageContainerConstants.Magic, 0, PackageContainerConstants.Magic.Length);

        var headerLenBytes = BitConverter.GetBytes(headerBytes.Length);
        if (!BitConverter.IsLittleEndian) Array.Reverse(headerLenBytes);

        fs.Write(headerLenBytes, 0, headerLenBytes.Length);
        fs.Write(headerBytes, 0, headerBytes.Length);
        fs.Write(ciphertext, 0, ciphertext.Length);
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations, int keyBytes)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(keyBytes);
    }
}