namespace InstallBunker.Common.PackageContainer;

public sealed class PackageContainerReadException : Exception
{
    public PackageContainerErrorKind Kind { get; }

    public PackageContainerReadException(
        PackageContainerErrorKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }
}