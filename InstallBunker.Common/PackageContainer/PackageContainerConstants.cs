namespace InstallBunker.Common.PackageContainer;

internal static class PackageContainerConstants
{
    // "IBPKG2\0\0" (8 bytes) - helps detect wrong file quickly.
    public static readonly byte[] Magic = { (byte)'I', (byte)'B', (byte)'P', (byte)'K', (byte)'G', (byte)'2', 0, 0 };

    public const int HeaderLengthSize = 4; // int32 little-endian
}