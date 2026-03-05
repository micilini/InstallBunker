namespace InstallBunker.Common.PackageContainer;

public static class PackageContainerLimits
{
    public const int MaxHeaderBytes = 256 * 1024;

    public const long MaxCiphertextBytes = 512L * 1024 * 1024;

    public const long MaxPlaintextBytes = 512L * 1024 * 1024;

    public const int MaxEntries = 10_000;
    public const long MaxEntryBytes = 256L * 1024 * 1024;
    public const long MaxTotalExtractBytes = 2L * 1024 * 1024 * 1024;

    public const int CopyBufferBytes = 80 * 1024;
}