using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InstallBunker.Common.PackageContainer;

public static class PackageContainerReader
{
    public static PackageContainerHeader ExtractToDirectory(
        string packagePath,
        string password,
        string destinationDirectory)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new InvalidOperationException("Package path is required.");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Package password is required.");

        if (string.IsNullOrWhiteSpace(destinationDirectory))
            throw new InvalidOperationException("Destination directory is required.");

        if (!File.Exists(packagePath))
            throw new PackageContainerReadException(
                PackageContainerErrorKind.PackageNotFound,
                "Package.pkg not found.");

        Directory.CreateDirectory(destinationDirectory);

        using var fs = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var magic = new byte[PackageContainerConstants.Magic.Length];
        ReadExactly(fs, magic);

        if (!magic.SequenceEqual(PackageContainerConstants.Magic))
            throw new PackageContainerReadException(
                PackageContainerErrorKind.InvalidMagic,
                "Invalid Package.pkg header (magic mismatch).");

        var headerLenBytes = new byte[PackageContainerConstants.HeaderLengthSize];
        ReadExactly(fs, headerLenBytes);

        if (!BitConverter.IsLittleEndian) Array.Reverse(headerLenBytes);

        var headerLen = BitConverter.ToInt32(headerLenBytes, 0);
        if (headerLen <= 0 || headerLen > PackageContainerLimits.MaxHeaderBytes)
            throw new PackageContainerReadException(
                PackageContainerErrorKind.InvalidHeaderLength,
                "Invalid Package.pkg header length.");

        var headerBytes = new byte[headerLen];
        ReadExactly(fs, headerBytes);

        var headerJson = Encoding.UTF8.GetString(headerBytes);

        PackageContainerHeader header;

        try
        {
            header = JsonSerializer.Deserialize<PackageContainerHeader>(headerJson)
                     ?? throw new PackageContainerReadException(
                         PackageContainerErrorKind.HeaderParseFailed,
                         "Package.pkg header could not be parsed.");
        }
        catch (PackageContainerReadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PackageContainerReadException(
                PackageContainerErrorKind.HeaderParseFailed,
                "Package.pkg header could not be parsed.",
                ex);
        }

        if (header.FormatVersion < PackageContainerFormat.MinSupportedVersion ||
            header.FormatVersion > PackageContainerFormat.MaxSupportedVersion)
        {
            throw new PackageContainerReadException(
                PackageContainerErrorKind.UnsupportedVersion,
                $"Unsupported package format version: {header.FormatVersion}. " +
                $"Supported range: {PackageContainerFormat.MinSupportedVersion}..{PackageContainerFormat.MaxSupportedVersion}.");
        }

        var ciphertextLen = fs.Length - fs.Position;
        if (ciphertextLen <= 0 || ciphertextLen > PackageContainerLimits.MaxCiphertextBytes || ciphertextLen > int.MaxValue)
            throw new PackageContainerReadException(
                PackageContainerErrorKind.InvalidCiphertextLength,
                "Invalid Package.pkg ciphertext length.");

        var ciphertext = new byte[(int)ciphertextLen];
        ReadExactly(fs, ciphertext);

        var salt = Convert.FromBase64String(header.SaltBase64);
        var nonce = Convert.FromBase64String(header.NonceBase64);
        var tag = Convert.FromBase64String(header.TagBase64);

        var key = DeriveKey(password, salt, header.KdfIterations, 32);
        var plaintext = new byte[ciphertext.Length];

        if (ciphertext.Length > PackageContainerLimits.MaxPlaintextBytes)
        {
            throw new PackageContainerReadException(
                PackageContainerErrorKind.LimitsExceeded,
                "Package payload exceeds the maximum allowed size.");
        }

        try
        {
            using var aes = new AesGcm(key, tagSizeInBytes: tag.Length);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new PackageContainerReadException(
                PackageContainerErrorKind.AuthenticationFailed,
                "Package authentication failed (wrong password or tampered package).",
                ex);
        }

        var sha256 = SHA256.HashData(plaintext);
        var shaB64 = Convert.ToBase64String(sha256);

        if (!shaB64.Equals(header.InnerSha256Base64, StringComparison.Ordinal))
            throw new PackageContainerReadException(
                PackageContainerErrorKind.IntegrityCheckFailed,
                "Package integrity check failed (ZIP hash mismatch).");

        using var zipMs = new MemoryStream(plaintext);
        using var zip = new ZipArchive(zipMs, ZipArchiveMode.Read);

        var extractedCount = 0;
        long extractedTotalBytes = 0;

        foreach (var entry in zip.Entries)
        {
            // ignora diretórios
            if (string.IsNullOrWhiteSpace(entry.FullName))
                continue;

            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.EndsWith("/", StringComparison.Ordinal))
                continue;

            extractedCount++;

            if (extractedCount > PackageContainerLimits.MaxEntries)
            {
                throw new PackageContainerReadException(
                    PackageContainerErrorKind.LimitsExceeded,
                    "Package contains too many files.");
            }

            // entry.Length é o tamanho descompactado
            if (entry.Length < 0 || entry.Length > PackageContainerLimits.MaxEntryBytes)
            {
                throw new PackageContainerReadException(
                    PackageContainerErrorKind.LimitsExceeded,
                    "A file inside the package exceeds the maximum allowed size.");
            }

            var destPath = PackageContainerPathGuard.GetSafeDestinationPath(destinationDirectory, entry.FullName);

            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            try
            {
                using var entryStream = entry.Open();
                using var outStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

                CopyToWithLimits(entryStream, outStream, entry.Length, ref extractedTotalBytes);
            }
            catch (PackageContainerReadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PackageContainerReadException(
                    PackageContainerErrorKind.ExtractionFailed,
                    $"Failed to extract entry: {entry.FullName}",
                    ex);
            }
        }

        return header;
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read <= 0)
                throw new PackageContainerReadException(
                    PackageContainerErrorKind.UnexpectedEndOfStream,
                    "Unexpected end of stream while reading Package.pkg.");
            offset += read;
        }
    }

    private static void CopyToWithLimits(Stream input, Stream output, long expectedBytes, ref long totalExtractedBytes)
    {
        var buffer = new byte[PackageContainerLimits.CopyBufferBytes];
        long entryWritten = 0;

        while (true)
        {
            var read = input.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            entryWritten += read;
            totalExtractedBytes += read;

            if (entryWritten > expectedBytes)
            {
                throw new PackageContainerReadException(
                    PackageContainerErrorKind.LimitsExceeded,
                    "Extraction exceeded expected entry size.");
            }

            if (totalExtractedBytes > PackageContainerLimits.MaxTotalExtractBytes)
            {
                throw new PackageContainerReadException(
                    PackageContainerErrorKind.LimitsExceeded,
                    "Package extraction exceeds the maximum total allowed size.");
            }

            output.Write(buffer, 0, read);
        }

        if (entryWritten != expectedBytes)
        {
            throw new PackageContainerReadException(
                PackageContainerErrorKind.ExtractionFailed,
                "Extracted entry size does not match expected size.");
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations, int keyBytes)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(keyBytes);
    }
}